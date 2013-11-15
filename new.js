window.addEventListener('load', function() {

  var $ = document.querySelector.bind(document);


  //////////////////////////////////////////////////////////////////////
  //
  // Main Page Logic
  //
  //////////////////////////////////////////////////////////////////////

  //
  // Initialize map and register callbacks
  //
  var mapElement = $("#dragContainer");
  var map = new Map(mapElement);

  // Export
  window.map = map;


  //////////////////////////////////////////////////////////////////////
  //
  // Parameters and Style
  //
  //////////////////////////////////////////////////////////////////////

  // Tweak defaults
  map.SetOptions(map.GetOptions() | MapOptions.ForceHexes);

  // TODO: Avoid flash on load

  // TODO: If style or position changes (below) does this generate bogus requests?
  map.ScaleCenterAtSectorHex(2, 0, 0, Astrometrics.ReferenceHexX, Astrometrics.ReferenceHexY);

  map.OnScaleChanged = function(scale) {
    updatePermalink();
  };

  var optionObservers = [];
  map.OnOptionsChanged = function(options) {
    optionObservers.forEach(function(o) { o(options); });
    $('#legendBox').classList[(options & MapOptions.WorldColors) ? "add" : "remove"]("world_colors");
    updatePermalink();
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
  bindCheckedToOption('#ShowGovernmentNames', MapOptions.NamesMask);
  bindCheckedToOption('#ShowImportantWorlds', MapOptions.WorldsMask);
  bindCheckedToOption('#cbForceHexes', MapOptions.ForceHexes);
  bindCheckedToOption('#cbWorldColors', MapOptions.WorldColors);
  bindCheckedToOption('#cbFilledBorders',MapOptions.FilledBorders);

  function bindControl(selector, property, onChange, event, onEvent) {
    var element = $(selector);
    if (!element) { console.error("Unmatched selector: " + selector); return; }
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


  map.OnStyleChanged = function(style) {
    ['poster', 'atlas', 'print', 'candy'].forEach(function(s) {
      document.body.classList[s === style ? 'add' : 'remove']('style-' + s);
    });
    updatePermalink();
  };

  map.OnDisplayChanged = function() {
    updatePermalink();
    showCredits(map.GetHexX(), map.GetHexY());
  };

  map.OnClick = map.OnDoubleClick = function(hex) {
    showCredits(hex.x, hex.y);
  };

  //
  // Pull in options from URL - from permalinks
  //

  // Call this AFTER data binding is hooked up so UI is synchronized
  var urlParams = applyUrlParameters(map);

  // Force UI to synchronize in case URL parameters didn't do it
  // TODO: Figure out desired order for this.
  map.OnOptionsChanged(map.GetOptions());

  // TODO: Make this less hokey
  $("#ShowGalacticDirections").checked = true;
  $("#ShowGalacticDirections").onclick = function() {
    mapElement.classList[this.checked ? 'add' : 'remove']('galdir');
    updatePermalink();
  };
  if ("galdir" in urlParams) {
    var showGalacticDirections = Boolean(Number(urlParams.galdir));
    mapElement.classList[showGalacticDirections ? 'add' : 'remove']('galdir');
    $("#ShowGalacticDirections").checked = showGalacticDirections;
    updatePermalink();
  }

  if ("q" in urlParams) {
    $('#searchBox').value = urlParams.q;
    search(urlParams.q);
  }

  var permalinkTimeout = 0;
  var lastHref;
  function updatePermalink() {
    var PERMALINK_REFRESH_DELAY_MS = 500;
    if (permalinkTimeout)
      clearTimeout(permalinkTimeout);
    permalinkTimeout = setTimeout(function() {

      // TODO: Factor this out and use for search results as well
      var href = document.location.href.replace(/\?.*/, '');

      urlParams.x = Math.round(map.GetX() * 1000) / 1000;
      urlParams.y = Math.round(map.GetY() * 1000) / 1000;
      urlParams.scale = Math.round(map.GetScale() * 100) / 100;
      urlParams.options = map.GetOptions();
      urlParams.style = map.GetStyle();
      if (mapElement.classList.contains('galdir'))
        delete urlParams.galdir;
      else
        urlParams.galdir = 0;

      href += '?' + Object.keys(urlParams).map(function(p) {
        return p + '=' + encodeURIComponent(urlParams[p]);
      }).join('&');

      if (href === lastHref)
        return;

      // EXPERIMENTAL: update the URL in-place
      if ('history' in window && 'replaceState' in window.history) {
        if (document.location.href !== href)
          window.history.replaceState(null, document.title, href);
      }

      $('#share-url').value = href;
      $('#share-embed').value = '<iframe width=400 height=300 src="' + href + '">';

    }, PERMALINK_REFRESH_DELAY_MS);
  }

  function setOptions(mask, flags) {
    map.SetOptions((map.GetOptions() & ~mask) | flags);
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

  function showCredits(hexX, hexY) {
    var DATA_REQUEST_DELAY_MS = 500;
    if (lastX === hexX && lastY === hexY)
      return;

    if (dataRequest && dataRequest.abort) {
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
      }, function (error) {
        $("#MetadataDisplay").innerHTML = "<i>Error: " + error + "</i>";
      });

    }, DATA_REQUEST_DELAY_MS);

    function displayResults(data) {
      var tags = String(data.SectorTags).split(/\s+/);
      if (tags.indexOf('Official') >= 0) data.Official = true;
      else if (tags.indexOf('Preserve') >= 0) data.Preserve = true;
      else data.Unofficial = true;

      data.Attribution = (function() {
        var r = [];
        ['SectorAuthor', 'SectorSource', 'SectorPublisher'].forEach(function (p) {
          if (p in data) { r.push(data[p]); }
        });
        return r.join(', ');
      }());

      if ('SectorName' in data) {
        data.PosterURL = SERVICE_BASE + '/api/poster?sector=' +
          encodeURIComponent(data.SectorName) + '&accept=application/pdf&style=' + map.GetStyle();
        data.DataURL = SERVICE_BASE + '/api/sec?sector=' +
          encodeURIComponent(data.SectorName) + '&type=SecondSurvey';
      }

      var template = map.GetScale() >= 16 ? worldMetadataTemplate : sectorMetadataTemplate;
      $("#MetadataDisplay").innerHTML = statusMetadataTemplate(data) +
        template(data) + commonMetadataTemplate(data);
    }
  }


  //////////////////////////////////////////////////////////////////////
  //
  // Search
  //
  //////////////////////////////////////////////////////////////////////

  window.txt = $("#SearchResultsTemplate").innerHTML;
  var searchTemplate = Handlebars.compile(window.txt);

  var searchRequest = null;

  function search(query) {
    if (query === "")
      return;

    // IE stops animated images when submitting a form - restart it
    if (document.images) {
      var progressImage = $("#ProgressImage");
      progressImage.src = progressImage.src;
    }

    // NOTE: Do this first in case the response is synchronous (cached)
    document.body.classList.add('search-progress');
    document.body.classList.remove('search-results');

    if (searchRequest && searchRequest.abort)
      searchRequest.abort();

    searchRequest = MapService.search(query, function(data) {
      searchRequest = null;
      displayResults(data);
      document.body.classList.remove('search-progress');
      document.body.classList.add('search-results');
    }, function (error) {
      $("#SearchResults").innerHTML = "<div><i>Error: " + error + "<" + "/i><" + "/div>";
    });

    // Transform the search results into clickable links
    function displayResults(data) {
      var base_url = document.location.href.replace(/\?.*/, '');

      // Pre-process the data
      for (i = 0; i < data.Results.Items.length; ++i) {

        var item = data.Results.Items[i];
        var sx, sy, hx, hy, scale;

        // TODO: replace onclick handlers with proper (copy/pasteable) URLs and
        // intercept (?) navigation

        if (item.Subsector) {
          var subsector = item.Subsector,
            index = subsector.Index || "A",
            n = (index.charCodeAt(0) - "A".charCodeAt(0));
          sx = subsector.SectorX|0;
          sy = subsector.SectorY|0;
          hx = (((n % 4) | 0) + 0.5) * (Astrometrics.SectorWidth / 4);
          hy = (((n / 4) | 0) + 0.5) * (Astrometrics.SectorHeight / 4);
          scale = subsector.Scale || 32;

          subsector.href = base_url + '?scale=' + scale + '&sx=' + sx + '&sy=' + sy + '&hx=' + hx + '&hy=' + hy;
          subsector.onclick = "map.ScaleCenterAtSectorHex(" + scale + "," + sx + "," + sy + "," + hx + "," + hy + "); return false;";
        } else if (item.Sector) {
          var sector = item.Sector;
          sx = sector.SectorX|0;
          sy = sector.SectorY|0;
          hx = (Astrometrics.SectorWidth / 2);
          hy = (Astrometrics.SectorHeight / 2);
          scale = sector.Scale || 8;

          sector.href = base_url + '?scale=' + scale + '&sx=' + sx + '&sy=' + sy + '&hx=' + hx + '&hy=' + hy;
          sector.onclick = "map.ScaleCenterAtSectorHex(" + scale + "," + sx + "," + sy + "," + hx + "," + hy + "); return false;";
        } else if (item.World) {
          var world = item.World;
          world.Name = world.Name || "(Unnamed)";
          sx = world.SectorX | 0;
          sy = world.SectorY | 0;
          hx = world.HexX | 0;
          hy = world.HexY|0;
          world.Hex = (hx < 10 ? "0" : "") + hx + (hy < 10 ? "0" : "") + hy;
          scale = world.Scale || 64;

          world.href = base_url + '?scale=' + scale + '&sx=' + sx + '&sy=' + sy + '&hx=' + hx + '&hy=' + hy;
          world.onclick = "map.ScaleCenterAtSectorHex(" + scale + "," + sx + "," + sy + "," + hx + "," + hy + "); return false;";
        }
      }

      $("#resultsContainer").innerHTML = searchTemplate(data);
    }
  }

  // Export
  window.search = search;


  //////////////////////////////////////////////////////////////////////
  //
  // Final setup
  //
  //////////////////////////////////////////////////////////////////////

  if (typeof mapElement.focus === 'function') {
    mapElement.focus();
  }
});
