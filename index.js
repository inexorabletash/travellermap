window.addEventListener('DOMContentLoaded', function() {
  'use strict';

  //////////////////////////////////////////////////////////////////////
  //
  // Utilities
  //
  //////////////////////////////////////////////////////////////////////

  // IE8: document.querySelector can't use bind()
  var $ = function(s) { return document.querySelector(s); };

  //////////////////////////////////////////////////////////////////////
  //
  // Main Page Logic
  //
  //////////////////////////////////////////////////////////////////////

  var mapElement = $('#dragContainer');
  var map = new Map(mapElement);

  // Export
  window.map = map;


  //////////////////////////////////////////////////////////////////////
  //
  // Parameters and Style
  //
  //////////////////////////////////////////////////////////////////////

  // Tweak defaults
  map.SetOptions(map.GetOptions() | MapOptions.NamesMinor | MapOptions.ForceHexes);
  map.SetScale(mapElement.offsetWidth <= 640 ? 1 : 2);
  map.CenterAtSectorHex(0, 0, Astrometrics.ReferenceHexX, Astrometrics.ReferenceHexY);
  var defaults = {
    x: map.GetX(),
    y: map.GetY(),
    scale: map.GetScale(),
    options: map.GetOptions(),
    routes: 1,
    style: map.GetStyle()
  };
  var home = {
    x: defaults.x,
    y: defaults.y,
    scale: defaults.scale
  };

  function setOptions(mask, flags) {
    map.SetOptions((map.GetOptions() & ~mask) | flags);
  }

  map.OnScaleChanged = function() {
    updatePermalink();
    updateSectorLinks();
  };

  var optionObservers = [];
  map.OnOptionsChanged = function(options) {
    optionObservers.forEach(function(o) { o(options); });
    $('#legendBox').classList[(options & MapOptions.WorldColors) ? 'add' : 'remove']('world_colors');
    updatePermalink();
    updateSectorLinks();
  };

  bindCheckedToOption('#ShowSectorGrid', MapOptions.GridMask);
  bindCheckedToOption('#ShowSectorNames', MapOptions.SectorsMask);
  bindEnabled('#ShowSelectedSectorNames', function(o) { return o & MapOptions.SectorsMask; });
  bindChecked('#ShowSelectedSectorNames',
              function(o) { return o & MapOptions.SectorsSelected; },
              function(c) { setOptions(MapOptions.SectorsMask, c ? MapOptions.SectorsSelected : 0); });
  bindEnabled('#ShowAllSectorNames', function(o) { return o & MapOptions.SectorsMask; });
  bindChecked('#ShowAllSectorNames',
              function(o) { return o & MapOptions.SectorsAll; },
              function(c) { setOptions(MapOptions.SectorsMask, c ? MapOptions.SectorsAll : 0); });
  bindCheckedToOption('#ShowGovernmentBorders', MapOptions.BordersMask);
  bindCheckedToNamedOption('#ShowRoutes', 'routes');
  bindCheckedToOption('#ShowGovernmentNames', MapOptions.NamesMask);
  bindCheckedToOption('#ShowImportantWorlds', MapOptions.WorldsMask);
  bindCheckedToOption('#cbForceHexes', MapOptions.ForceHexes);
  bindCheckedToOption('#cbWorldColors', MapOptions.WorldColors);
  bindCheckedToOption('#cbFilledBorders',MapOptions.FilledBorders);

  function bindControl(selector, property, onChange, event, onEvent) {
    var element = $(selector);
    optionObservers.push(function(o) { element[property] = onChange(o); });
    element.addEventListener(event, function() { onEvent(element); });
  }
  function bindChecked(selector, onChange, onEvent) {
    bindControl(selector, 'checked', onChange, 'click', function(e) { onEvent(e.checked); });
  }
  function bindEnabled(selector, onChange) {
    var element = $(selector);
    optionObservers.push(function(o) { element.disabled = !onChange(o); });
  }
  function bindCheckedToOption(selector, bitmask) {
    bindChecked(selector,
                function(o) { return (o & bitmask); },
                function(c) { setOptions(bitmask, c ? bitmask : 0); });
  }
  function bindCheckedToNamedOption(selector, name) {
    bindChecked(selector,
                function() { var v = map.GetNamedOption(name);
                             return v === undefined ? defaults[name] : v; },
                function(c) { if (c === defaults[name]) map.ClearNamedOption(name);
                              else map.SetNamedOption(name, c ? 1 : 0); });
  }

  map.OnStyleChanged = function(style) {
    ['poster', 'atlas', 'print', 'candy'].forEach(function(s) {
      document.body.classList[s === style ? 'add' : 'remove']('style-' + s);
    });
    updatePermalink();
    updateSectorLinks();
  };

  map.OnDisplayChanged = function() {
    updatePermalink();
    showCredits(map.GetHexX(), map.GetHexY());
  };

  map.OnClick = function(hex) {
    showCredits(hex.x, hex.y, /*immediate*/true);
    if (window.parent !== window && 'postMessage' in window.parent) {
      window.parent.postMessage({
        source: 'travellermap',
        type: 'click',
        location: hex
      }, '*');
    }
  };

  map.OnDoubleClick = function(hex) {
    showCredits(hex.x, hex.y, /*immediate*/true);
    if (window.parent !== window && 'postMessage' in window.parent) {
      window.parent.postMessage({
        source: 'travellermap',
        type: 'doubleclick',
        location: hex
      }, '*');
    }
  };

  //
  // Pull in options from URL - from permalinks
  //
  // Call this AFTER data binding is hooked up so UI is synchronized
  //
  var urlParams = map.ApplyURLParameters();

  // Force UI to synchronize in case URL parameters didn't do it
  map.OnOptionsChanged(map.GetOptions());

  // TODO: Generalize URLParam<->Control and URLParam<->Style binding
  $('#ShowGalacticDirections').checked = true;
  document.body.classList.add('show-directions');
  $('#ShowGalacticDirections').addEventListener('click', function() {
    document.body.classList[this.checked ? 'add' : 'remove']('show-directions');
    updatePermalink();
  });
  if ('galdir' in urlParams) {
    var show = Boolean(Number(urlParams.galdir));
    document.body.classList[show ? 'add' : 'remove']('show-directions');
    $('#ShowGalacticDirections').checked = show;
    updatePermalink();
  }

  if ('q' in urlParams) {
    $('#searchBox').value = urlParams.q;
    search(urlParams.q);
  }

  function goHome() {
    if (['sx', 'sy', 'hx', 'hy'].every(function(p) { return ('yah_' + p) in urlParams; })) {
      map.ScaleCenterAtSectorHex(64,
                                 urlParams.yah_sx|0,
                                 urlParams.yah_sy|0,
                                 urlParams.yah_hx|0,
                                 urlParams.yah_hy|0);
      return;
    }
    map.SetScale(home.scale);
    map.SetPosition(home.x, home.y);
  }

  $('#homeBtn').addEventListener('click', goHome);

  mapElement.addEventListener('keydown', function(e) {
    if (e.ctrlKey || e.altKey || e.metaKey)
      return;
    var VK_H = 72;
    if (e.keyCode === VK_H) {
      e.preventDefault();
      e.stopPropagation();
      goHome();
      return;
    }
  });

  //////////////////////////////////////////////////////////////////////
  //
  // Permalink
  //
  //////////////////////////////////////////////////////////////////////

  var permalinkTimeout = 0;
  var lastPageURL = null;
  function updatePermalink() {
    var PERMALINK_REFRESH_DELAY_MS = 500;
    if (permalinkTimeout)
      clearTimeout(permalinkTimeout);
    permalinkTimeout = setTimeout(function() {

      function round(n, d) {
        d = 1 / d; // Avoid twitchy IEEE754 rounding.
        return Math.round(n * d) / d;
      }

      urlParams.x = round(map.GetX(), 1/1000);
      urlParams.y = round(map.GetY(), 1/1000);
      urlParams.scale = round(map.GetScale(), 1/128);
      urlParams.options = map.GetOptions();
      urlParams.style = map.GetStyle();

      map.GetNamedOptionNames().forEach(function(name) {
        urlParams[name] = map.GetNamedOption(name);
      });

      delete urlParams.sector;
      delete urlParams.hex;
      ['x', 'y', 'options', 'scale', 'style', 'routes'].forEach(function(p) {
        if (urlParams[p] === defaults[p]) delete urlParams[p];
      });

      if (document.body.classList.contains('show-directions'))
        delete urlParams.galdir;
      else
        urlParams.galdir = 0;

      var pageURL = makeURL(document.location, urlParams);

      if (pageURL === lastPageURL)
        return;

      if ('history' in window && 'replaceState' in window.history && document.location.href !== pageURL)
          window.history.replaceState(null, document.title, pageURL);

      $('#share-url').value = pageURL;
      $('#share-embed').value = '<iframe width=400 height=300 src="' + pageURL + '">';

      var snapshotParams = (function() {
        var map_center_x = map.GetX(),
            map_center_y = map.GetY(),
            scale = map.GetScale(),
            rect = mapElement.getBoundingClientRect(),
            width = rect.width,
            height = rect.height,
            x = ( map_center_x * scale - ( width / 2 ) ) / width,
            y = ( -map_center_y * scale - ( height / 2 ) ) / height;
        return { x: x, y: y, w: width, h: height, scale: scale };
      }());
      snapshotParams.x = round(snapshotParams.x, 1/1000);
      snapshotParams.y = round(snapshotParams.y, 1/1000);
      snapshotParams.scale = round(snapshotParams.scale, 1/128);
      snapshotParams.options = map.GetOptions();
      snapshotParams.style = map.GetStyle();
      snapshotParams.routes = urlParams.routes;
      var snapshotURL = makeURL(SERVICE_BASE + '/api/tile', snapshotParams);
      $('a#download-snapshot').href = snapshotURL;
      snapshotParams.accept = 'application/pdf';
      snapshotURL = makeURL(SERVICE_BASE + '/api/tile', snapshotParams);
      $('a#download-snapshot-pdf').href = snapshotURL;

    }, PERMALINK_REFRESH_DELAY_MS);
  }


  //////////////////////////////////////////////////////////////////////
  //
  // Metadata
  //
  //////////////////////////////////////////////////////////////////////

  var commonMetadataTemplate = Handlebars.compile($('#CommonMetadataTemplate').innerHTML);
  var statusMetadataTemplate = Handlebars.compile($('#StatusMetadataTemplate').innerHTML);
  var worldMetadataTemplate = Handlebars.compile($('#WorldMetadataTemplate').innerHTML);
  var sectorMetadataTemplate = Handlebars.compile($('#SectorMetadataTemplate').innerHTML);

  var dataRequest = null;
  var dataTimeout = 0;
  var lastX, lastY;
  var selectedSector = null;
  var selectedWorld = null;

  function showCredits(hexX, hexY, immediate) {
    var DATA_REQUEST_DELAY_MS = 500;
    if (lastX === hexX && lastY === hexY)
      return;

    if (dataRequest) {
      dataRequest.abort();
      dataRequest = null;
    }

    if (dataTimeout)
      window.clearTimeout(dataTimeout);

    dataTimeout = setTimeout(function() {
      lastX = hexX;
      lastY = hexY;

      dataRequest = MapService.credits(hexX, hexY, function(data) {
        dataRequest = null;
        displayResults(data);
      }, function() {
      });

    }, immediate ? 0 : DATA_REQUEST_DELAY_MS);

    function displayResults(data) {
      if ('SectorTags' in data) {
        var tags =  String(data.SectorTags).split(/\s+/);
        if (tags.indexOf('Official') !== -1) data.Official = true;
        else if (tags.indexOf('Unreviewed') !== -1) data.Unreviewed = true;
        else if (tags.indexOf('Preserve') !== -1) data.Preserve = true;
        else data.Unofficial = true;
      } else {
        data.Unmapped = true;
      }

      data.Attribution = (function() {
        var r = [];
        ['SectorAuthor', 'SectorSource', 'SectorPublisher'].forEach(function(p) {
          if (p in data) { r.push(data[p]); }
        });
        return r.join(', ');
      }());

      // Other UI
      if ('SectorName' in data && 'SectorTags' in data) {
        selectedSector = data.SectorName;
        selectedWorld = (map.GetScale() >= 16 && 'WorldHex' in data) ? { name: data.WorldName, hex: data.WorldHex } : null;
        updateSectorLinks();
        $('#downloadBox').classList.add('sector-selected');
        $('#downloadBox').classList[selectedWorld ? 'add' : 'remove']('world-selected');
      } else {
        selectedSector = null;
        selectedWorld = null;
        $('#downloadBox').classList.remove('sector-selected');
        $('#downloadBox').classList.remove('world-selected');
      }

      var template = selectedWorld ? worldMetadataTemplate : sectorMetadataTemplate;
      $('#MetadataDisplay').innerHTML =
        template(data) + commonMetadataTemplate(data) + statusMetadataTemplate(data);
    }
  }

  function updateSectorLinks() {
    if (!selectedSector)
      return;

    var bookletURL = SERVICE_BASE +
          '/data/' + encodeURIComponent(selectedSector) + '/booklet';
    var posterURL = makeURL(SERVICE_BASE + '/api/poster', {
      sector: selectedSector, accept: 'application/pdf', style: map.GetStyle()});
    var dataURL = makeURL(SERVICE_BASE + '/api/sec', {
      sector: selectedSector, type: 'SecondSurvey' });

    var worldURL = makeURL('world.html', {
      sector: selectedSector,
      hex: selectedWorld ? selectedWorld.hex : ''
    });

    var title = selectedSector.replace(/ Sector$/, '') + ' Sector';
    $('#downloadBox #sector-name').innerHTML = escapeHtml(title);
    $('#downloadBox a#download-booklet').href = bookletURL;
    $('#downloadBox a#download-poster').href = posterURL;
    $('#downloadBox a#download-data').href = dataURL;

    if (selectedWorld) {
      $('#downloadBox a#world-data-sheet').href = worldURL;
      $('#downloadBox a#world-data-sheet').innerHTML = 'Data Sheet: ' +
        selectedWorld.name + ' (' + selectedWorld.hex + ')';
    }
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Search
  //
  //////////////////////////////////////////////////////////////////////

  var searchTemplate = Handlebars.compile($('#SearchResultsTemplate').innerHTML);

  var searchRequest = null;
  var lastQuery = null;

  function search(query, typed) {
    if (query === '')
      return;

    if (query === lastQuery) {
      if (!document.body.classList.contains('search-progress') && !typed)
        document.body.classList.add('search-results');
      return;
    }
    lastQuery = query;

    if (!typed) {
      // IE stops animated images when submitting a form - restart it
      if (document.images) {
        var progressImage = $('#ProgressImage');
        progressImage.src = progressImage.src;
      }

      document.body.classList.add('search-progress');
      document.body.classList.remove('search-results');
    }

    if (searchRequest)
      searchRequest.abort();

    searchRequest = MapService.search(query, function(data) {
      searchRequest = null;
      displayResults(data);
      document.body.classList.remove('search-progress');
      document.body.classList.add('search-results');
    }, function() {
      searchRequest = null;
      $('#resultsContainer').innerHTML = '<i>Error fetching results.</i>';
      document.body.classList.remove('search-progress');
      document.body.classList.add('search-results');
    });

    // Transform the search results into clickable links
    function displayResults(data) {
      var base_url = document.location.href.replace(/\?.*/, '');

      function applyTags(item) {
        if ('SectorTags' in item) {
          var tags = String(item.SectorTags).split(/\s+/);
          if (tags.indexOf('Official') !== -1) item.Official = true;
          else if (tags.indexOf('Unreviewed') !== -1) item.Unreviewed = true;
          else if (tags.indexOf('Preserve') !== -1) item.Unofficial = true;
          else item.Unofficial = true;
        }
      }

      function pad2(n) {
        return ('00' + n).slice(-2);
      }

      // Pre-process the data
      for (var i = 0; i < data.Results.Items.length; ++i) {

        var item = data.Results.Items[i];
        var sx, sy, hx, hy, scale;

        if (item.Subsector) {
          var subsector = item.Subsector,
            index = subsector.Index || 'A',
            n = (index.charCodeAt(0) - 'A'.charCodeAt(0));
          sx = subsector.SectorX|0;
          sy = subsector.SectorY|0;
          hx = (((n % 4) | 0) + 0.5) * (Astrometrics.SectorWidth / 4);
          hy = (((n / 4) | 0) + 0.5) * (Astrometrics.SectorHeight / 4);
          scale = subsector.Scale || 32;
          subsector.href = makeURL(base_url, {scale: scale, sx: sx, sy: sy, hx: hx, hy: hy});
          applyTags(subsector);
        } else if (item.Sector) {
          var sector = item.Sector;
          sx = sector.SectorX|0;
          sy = sector.SectorY|0;
          hx = (Astrometrics.SectorWidth / 2);
          hy = (Astrometrics.SectorHeight / 2);
          scale = sector.Scale || 8;
          sector.href = makeURL(base_url, {scale: scale, sx: sx, sy: sy, hx: hx, hy: hy});
          applyTags(sector);
        } else if (item.World) {
          var world = item.World;
          world.Name = world.Name || '(Unnamed)';
          sx = world.SectorX|0;
          sy = world.SectorY|0;
          hx = world.HexX|0;
          hy = world.HexY|0;
          world.Hex = pad2(hx) + pad2(hy);
          scale = world.Scale || 64;
          world.href = makeURL(base_url, {scale: scale, sx: sx, sy: sy, hx: hx, hy: hy});
          applyTags(world);
        }
      }

      $('#resultsContainer').innerHTML = searchTemplate(data);

      [].forEach.call(document.querySelectorAll('#resultsContainer a'), function(a) {
        a.addEventListener('click', function(e) {
          e.preventDefault();
          var params = window.parseURLQuery(e.target);
          map.ScaleCenterAtSectorHex(params.scale|0, params.sx|0, params.sy|0, params.hx|0, params.hy|0);
          if (mapElement.offsetWidth < 640)
            document.body.classList.remove('search-results');
        });
      });

      var first = $('#resultsContainer a');
      if (first && !typed)
        setTimeout(function() { first.focus(); }, 0);
    }
  }

  // Export
  window.search = search;


  //////////////////////////////////////////////////////////////////////
  //
  // Final setup
  //
  //////////////////////////////////////////////////////////////////////

  mapElement.focus();
});
