// Externs
var Traveller, Util, Handlebars;

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
  var map = new Traveller.Map(mapElement);

  // Export
  window.map = map;

  var isIframe = (window != window.top); // != for IE
  var isSmallScreen = mapElement.offsetWidth <= 640; // Arbitrary


  //////////////////////////////////////////////////////////////////////
  //
  // Parameters and Style
  //
  //////////////////////////////////////////////////////////////////////

  // Tweak defaults
  map.SetOptions(map.GetOptions() | Traveller.MapOptions.NamesMinor | Traveller.MapOptions.ForceHexes);
  map.SetScale(isSmallScreen ? 1 : 2);
  map.CenterAtSectorHex(0, 0, Traveller.Astrometrics.ReferenceHexX, Traveller.Astrometrics.ReferenceHexY);
  var defaults = {
    x: map.GetX(),
    y: map.GetY(),
    scale: map.GetScale(),
    options: map.GetOptions(),
    routes: 1,
    dimunofficial: 0,
    style: map.GetStyle()
  };
  var home = {
    x: defaults.x,
    y: defaults.y,
    scale: defaults.scale
  };

  var SAVE_PREFERENCES_DELAY_MS = 500;
  var savePreferences = isIframe ? function() {} : Util.debounce(function() {
    function maybeSave(test, key, data) {
      if (test)
        localStorage.setItem(key, JSON.stringify(data));
      else
        localStorage.removeItem(key);
    }

    var prefs = {
      style: map.GetStyle(),
      options: map.GetOptions(),
      routes: map.GetNamedOption('routes'),
      dimunofficial: map.GetNamedOption('dimunofficial')
    };
      PARAM_OPTIONS.forEach(function(option) {
        prefs[option.param] = document.body.classList.contains(option.className);
      });
    maybeSave($('#cbSavePreferences').checked, 'preferences', prefs);
    maybeSave($('#cbSaveLocation').checked, 'location', {
      position: { x: map.GetX(), y: map.GetY() },
      scale: map.GetScale()
    });
  }, SAVE_PREFERENCES_DELAY_MS);


  //////////////////////////////////////////////////////////////////////
  //
  // Permalink
  //
  //////////////////////////////////////////////////////////////////////

  var PERMALINK_REFRESH_DELAY_MS = 500;
  var lastPageURL = null;
  var updatePermalink = Util.debounce(function() {
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
    ['x', 'y', 'options', 'scale', 'style', 'routes', 'dimunofficial'].forEach(function(p) {
      if (urlParams[p] === defaults[p]) delete urlParams[p];
    });

    PARAM_OPTIONS.forEach(function(option) {
      if (document.body.classList.contains(option.className) === option['default'])
        delete urlParams[option.param];
      else
        urlParams[option.param] = 1;
    });

    var pageURL = Util.makeURL(document.location, urlParams);

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
          width = Math.round(rect.width),
          height = Math.round(rect.height),
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
    snapshotParams.dimunofficial = urlParams.dimunofficial;
    var snapshotURL = Util.makeURL(Traveller.SERVICE_BASE + '/api/tile', snapshotParams);
    $('a#download-snapshot').href = snapshotURL;
    snapshotParams.accept = 'application/pdf';
    snapshotURL = Util.makeURL(Traveller.SERVICE_BASE + '/api/tile', snapshotParams);
    $('a#download-snapshot-pdf').href = snapshotURL;

  }, PERMALINK_REFRESH_DELAY_MS);


  //////////////////////////////////////////////////////////////////////
  //
  // UI Binding
  //
  //////////////////////////////////////////////////////////////////////

  function setOptions(mask, flags) {
    map.SetOptions((map.GetOptions() & ~mask) | flags);
  }

  var optionObservers = [];

  bindCheckedToOption('#ShowSectorGrid', Traveller.MapOptions.GridMask);
  bindCheckedToOption('#ShowSectorNames', Traveller.MapOptions.SectorsMask);
  bindEnabled('#ShowSelectedSectorNames', function(o) { return o & Traveller.MapOptions.SectorsMask; });
  bindChecked('#ShowSelectedSectorNames',
              function(o) { return o & Traveller.MapOptions.SectorsSelected; },
              function(c) { setOptions(Traveller.MapOptions.SectorsMask, c ? Traveller.MapOptions.SectorsSelected : 0); });
  bindEnabled('#ShowAllSectorNames', function(o) { return o & Traveller.MapOptions.SectorsMask; });
  bindChecked('#ShowAllSectorNames',
              function(o) { return o & Traveller.MapOptions.SectorsAll; },
              function(c) { setOptions(Traveller.MapOptions.SectorsMask, c ? Traveller.MapOptions.SectorsAll : 0); });
  bindCheckedToOption('#ShowGovernmentBorders', Traveller.MapOptions.BordersMask);
  bindCheckedToNamedOption('#ShowRoutes', 'routes');
  bindCheckedToOption('#ShowGovernmentNames', Traveller.MapOptions.NamesMask);
  bindCheckedToOption('#ShowImportantWorlds', Traveller.MapOptions.WorldsMask);
  bindCheckedToOption('#cbForceHexes', Traveller.MapOptions.ForceHexes);
  bindCheckedToOption('#cbWorldColors', Traveller.MapOptions.WorldColors);
  bindCheckedToOption('#cbFilledBorders',Traveller.MapOptions.FilledBorders);
  bindCheckedToNamedOption('#cbDimUnofficial', 'dimunofficial');

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

  map.OnOptionsChanged = function(options) {
    optionObservers.forEach(function(o) { o(options); });
    $('#legendBox').classList[(options & Traveller.MapOptions.WorldColors) ? 'add' : 'remove']('world_colors');
    updatePermalink();
    updateSectorLinks();
    savePreferences();
  };

  map.OnStyleChanged = function(style) {
    ['poster', 'atlas', 'print', 'candy', 'draft', 'fasa'].forEach(function(s) {
      document.body.classList[s === style ? 'add' : 'remove']('style-' + s);
    });
    updatePermalink();
    updateSectorLinks();
    updateScaleIndicator();
    savePreferences();
  };

  map.OnScaleChanged = function() {
    updatePermalink();
    updateSectorLinks();
    updateScaleIndicator();
    savePreferences();
  };

  map.OnDisplayChanged = function() {
    showCredits(map.GetHexX(), map.GetHexY());
    updatePermalink();
    savePreferences();
  };

  function post(message) {
    if (window.parent !== window && 'postMessage' in window.parent) {
      // Fails cross-domain in IE10-
      try { window.parent.postMessage(message, '*'); } catch (_) {}
    }
  }

  map.OnClick = function(hex) {
    showCredits(hex.x, hex.y, /*immediate*/true);
    post({source: 'travellermap', type: 'click', location: hex});
  };

  map.OnDoubleClick = function(hex) {
    showCredits(hex.x, hex.y, /*immediate*/true);
    post({source: 'travellermap', type: 'doubleclick', location: hex});
  };

  // TODO: Generalize URLParam<->Control and URLParam<->Style binding
  var PARAM_OPTIONS = [
    {param: 'galdir', selector: '#cbGalDir', className: 'show-directions', 'default': true},
    {param: 'tilt', selector: '#cbTilt', className: 'tilt', 'default': false}
  ];
  PARAM_OPTIONS.forEach(function(option) {
    $(option.selector).checked = option['default'];
    document.body.classList[option['default'] ? 'add' : 'remove'](option.className);
    $(option.selector).addEventListener('click', function() {
      document.body.classList[this.checked ? 'add' : 'remove'](option.className);
      updatePermalink();
      savePreferences();
    });
  });

  (function() {
    if (isIframe) return;
    var preferences = JSON.parse(localStorage.getItem('preferences'));
    var location = JSON.parse(localStorage.getItem('location'));
    if (preferences) {
      $('#cbSavePreferences').checked = true;
      if ('style' in preferences) map.SetStyle(preferences.style);
      if ('options' in preferences) map.SetOptions(preferences.options);
      ['routes', 'dimunofficial'].forEach(function(name) {
        if (name in preferences) map.SetNamedOption(name, preferences[name]);
      });

      PARAM_OPTIONS.forEach(function(option) {
        if (option.param in preferences)
          document.body.classList[preferences[option.param] ? 'add' : 'remove'](option.className);
      });
    }

    if (location) {
      $('#cbSaveLocation').checked = true;
      if ('scale' in location) map.SetScale(location.scale);
      if ('position' in location) map.SetPosition(location.position.x, location.position.y);
    }
  }());


  $('#cbSavePreferences').addEventListener('click', savePreferences);
  $('#cbSaveLocation').addEventListener('click', savePreferences);

  //
  // Pull in options from URL - from permalinks
  //
  // Call this AFTER data binding is hooked up so UI is synchronized
  //
  var standalone = 'standalone' in window.navigator && window.navigator.standalone;
  var urlParams = standalone ? {} : map.ApplyURLParameters();

  // Force UI to synchronize in case URL parameters didn't do it
  map.OnOptionsChanged(map.GetOptions());

  if (isIframe) {
    var forceui = ('forceui' in urlParams) && Boolean(Number(urlParams.forceui));
    if (forceui)
      document.body.classList.remove('hide-ui');
  } else {
    document.body.classList.remove('hide-ui');
    document.body.classList.remove('hide-footer');
  }

  var dirty = false;
  PARAM_OPTIONS.forEach(function(option) {
    if (option.param in urlParams) {
      var show = Boolean(Number(urlParams[option.param]));
      document.body.classList[show ? 'add' : 'remove'](option.className);
      dirty = true;
    }
    $(option.selector).checked = document.body.classList.contains(option.className);
  });
  if (dirty) updatePermalink();

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

  $('#tiltBtn').addEventListener('click', toggleTilt);

  function toggleTilt() {
    $('#cbTilt').click();
  };

  mapElement.addEventListener('keydown', function(e) {
    if (e.ctrlKey || e.altKey || e.metaKey)
      return;
    var VK_H = 72, VK_T = 84;
    if (e.keyCode === VK_H) {
      e.preventDefault();
      e.stopPropagation();
      goHome();
      return;
    }
    if (e.keyCode === VK_T) {
      e.preventDefault();
      e.stopPropagation();
      toggleTilt();
      return;
    }
  });


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

      dataRequest = Traveller.MapService.credits(hexX, hexY, function(data) {
        dataRequest = null;
        displayResults(data);
      });

    }, immediate ? 0 : DATA_REQUEST_DELAY_MS);

    function displayResults(data) {
      if ('SectorTags' in data) {
        var tags =  String(data.SectorTags).split(/\s+/);
        data.Unofficial = true;
        ['Official', 'InReview', 'Unreviewed', 'Apocryphal', 'Preserve'].forEach(function(tag) {
          if (tags.indexOf(tag) !== -1) {
            delete data.Unofficial;
            data[tag] = true;
          }
        });
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

    var bookletURL = Traveller.SERVICE_BASE +
          '/data/' + encodeURIComponent(selectedSector) + '/booklet';
    var posterURL = Util.makeURL(Traveller.SERVICE_BASE + '/api/poster', {
      sector: selectedSector, accept: 'application/pdf', style: map.GetStyle()});
    var dataURL = Util.makeURL(Traveller.SERVICE_BASE + '/api/sec', {
      sector: selectedSector, type: 'SecondSurvey' });


    var title = selectedSector.replace(/ Sector$/, '') + ' Sector';
    $('#downloadBox #sector-name').innerHTML = Util.escapeHTML(title);
    $('#downloadBox a#download-booklet').href = bookletURL;
    $('#downloadBox a#download-poster').href = posterURL;
    $('#downloadBox a#download-data').href = dataURL;

    if (selectedWorld) {
      var worldURL = Util.makeURL('world.html', {
        sector: selectedSector,
        hex: selectedWorld.hex
      });

      $('#downloadBox a#world-data-sheet').href = worldURL;
      $('#downloadBox a#world-data-sheet').innerHTML = 'Data Sheet: ' +
        selectedWorld.name + ' (' + selectedWorld.hex + ')';

      var options = map.GetOptions() & (
        Traveller.MapOptions.BordersMask | Traveller.MapOptions.NamesMask |
          Traveller.MapOptions.WorldColors | Traveller.MapOptions.FilledBorders);

      for (var j = 1; j <= 6; ++j) {
        var jumpMapURL = Util.makeURL(Traveller.SERVICE_BASE + '/api/jumpmap', {
          sector: selectedSector,
          hex: selectedWorld.hex,
          jump: j,
          style: map.GetStyle(),
          options: options
        });
        $('#downloadBox a#world-jump-map-' + j).href = jumpMapURL;
      }
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

    searchRequest = Traveller.MapService.search(query, function(data) {
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
          item.Unofficial = true;
          ['Official', 'InReview', 'Unreviewed', 'Apocryphal', 'Preserve'].forEach(function(tag) {
            if (tags.indexOf(tag) !== -1) {
              delete item.Unofficial;
              item[tag] = true;
            }
          });
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
          hx = (((n % 4) | 0) + 0.5) * (Traveller.Astrometrics.SectorWidth / 4);
          hy = (((n / 4) | 0) + 0.5) * (Traveller.Astrometrics.SectorHeight / 4);
          scale = subsector.Scale || 32;
          subsector.href = Util.makeURL(base_url, {scale: scale, sx: sx, sy: sy, hx: hx, hy: hy});
          applyTags(subsector);
        } else if (item.Sector) {
          var sector = item.Sector;
          sx = sector.SectorX|0;
          sy = sector.SectorY|0;
          hx = (Traveller.Astrometrics.SectorWidth / 2);
          hy = (Traveller.Astrometrics.SectorHeight / 2);
          scale = sector.Scale || 8;
          sector.href = Util.makeURL(base_url, {scale: scale, sx: sx, sy: sy, hx: hx, hy: hy});
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
          world.href = Util.makeURL(base_url, {scale: scale, sx: sx, sy: sy, hx: hx, hy: hy});
          applyTags(world);
        }
      }

      $('#resultsContainer').innerHTML = searchTemplate(data);

      [].forEach.call(document.querySelectorAll('#resultsContainer a'), function(a) {
        a.addEventListener('click', function(e) {
          e.preventDefault();
          var params = Util.parseURLQuery(e.target);
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
  // Miscellaneous
  //
  //////////////////////////////////////////////////////////////////////

  var isCanvasSupported = ('getContext' in $('#scaleIndicator')),
      animId = 0;
  function updateScaleIndicator() {
    if (!isCanvasSupported) return;

    cancelAnimationFrame(animId);
    animId = requestAnimationFrame(function() {
      var scale = map.GetScale(),
          canvas = $('#scaleIndicator'),
          ctx = canvas.getContext('2d'),
          w = parseFloat(canvas.width),
          h = parseFloat(canvas.height),
          style = map.GetStyle(),
          color = ['atlas', 'print', 'draft', 'fasa'].indexOf(style) !== -1 ? 'black' : 'white';

      ctx.clearRect(0, 0, w, h);

      var dist = w / scale;
      var factor = Math.pow(10, Math.floor(Math.log(dist) / Math.LN10));
      dist = Math.floor(dist / factor) * factor;
      dist = parseFloat(dist.toPrecision(1));
      var label = dist + ' pc';
      var bar = dist * scale;

      ctx.strokeStyle = color;
      ctx.beginPath();
      ctx.moveTo(w - bar + 1, h / 2);
      ctx.lineTo(w - bar + 1, h * 3 / 4);
      ctx.lineTo(w - 1, h * 3 / 4);
      ctx.lineTo(w - 1, h / 2);
      ctx.stroke();

      ctx.fillStyle = color;
      ctx.font = '10px Univers, Arial, sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(label, w - bar / 2, h / 2);
    });
  }
  updateScaleIndicator();

  //////////////////////////////////////////////////////////////////////
  //
  // Popup Displays
  //
  //////////////////////////////////////////////////////////////////////

  window.showWorldPopup = function(url) {
    if (isSmallScreen) return true;
    $('#popup-iframe').src = url;
    $('#popup-iframe').onload = function() {
      $('#popup-overlay').classList.add('visible');
      $('#popup-click').focus();
    };
    return false;
  };
  ['click', 'keydown', 'touchstart'].forEach(function(event) {
    $('#popup-click').addEventListener(event, function(e) {
      e.preventDefault();
      $('#popup-overlay').classList.remove('visible');
      mapElement.focus();
    });
  });

  //////////////////////////////////////////////////////////////////////
  //
  // Final setup
  //
  //////////////////////////////////////////////////////////////////////

  if (!isIframe) // == for IE
    mapElement.focus();
});
