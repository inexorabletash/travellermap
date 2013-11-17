window.onload = function() {

  var $ = function(selector) {
    return document.querySelector(selector);
  };


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
  // TODO: This is used by Search result click handlers - make this dynamic instead.
  window.map = map;
  map.ScaleCenterAtSectorHex(2, 0, 0, Astrometrics.ReferenceHexX, Astrometrics.ReferenceHexY);

  map.OnScaleChanged = function(scale) {
    $("#SelectScale").value = scale;

    updatePermalink();
  };

  map.OnOptionsChanged = function(options) {
    // Grid
    $("#ShowSectorGrid").checked = (options & MapOptions.SectorGrid);
    $("#HideSectorGrid").checked = !(options & MapOptions.SectorGrid);

    // Sector Names
    $("#ShowSectorNames").checked = (options & MapOptions.SectorsMask);
    $("#ShowSelectedSectorNames").checked = (options & MapOptions.SectorsSelected);
    $("#ShowAllSectorNames").checked = (options & MapOptions.SectorsAll);

    $("#sector_label_options").disabled = !(options & MapOptions.SectorsMask);
    $("#ShowSelectedSectorNames").disabled = !(options & MapOptions.SectorsMask);
    $("#ShowAllSectorNames").disabled = !(options & MapOptions.SectorsMask);

    // Borders
    $("#ShowGovernmentBorders").checked = (options & MapOptions.BordersMask);

    // Names
    $("#ShowGovernmentNames").checked = (options & MapOptions.NamesMask);

    // Worlds
    $("#ShowCapitals").checked = (options & MapOptions.WorldsCapitals);
    $("#ShowHomeworlds").checked = (options & MapOptions.WorldsHomeworlds);

    // Appearance
    $("#cbForceHexes").checked = (options & MapOptions.ForceHexes);
    $("#cbWorldColors").checked = (options & MapOptions.WorldColors);
    $("#cbFilledBorders").checked = (options & MapOptions.FilledBorders);

    $('#legend').classList[(options & MapOptions.WorldColors) ? "add" : "remove"]("world_colors");

    updatePermalink();
  };

  map.OnStyleChanged = function(style) {
    $("#SelectStyle").value = style;
    document.body.className = style;

    updatePermalink();
  };

  map.OnDisplayChanged = function() {
    updatePermalink();
    showCredits(map.GetHexX(), map.GetHexY());
  };

  map.OnClick = function(hex) {
    showCredits(hex.x, hex.y);
  };

  map.OnDoubleClick = function(hex) {
    showCredits(hex.x, hex.y);
  };

  var permalinkTimeout = 0;
  function updatePermalink() {
    if (permalinkTimeout) {
      window.clearTimeout(permalinkTimeout);
    }

    var PERMALINK_REFRESH_DELAY_MS = 500;
    permalinkTimeout = window.setTimeout(function() {

      var href =
            "?x=" + Math.round(map.GetX() * 1000) / 1000 +
            "&y=" + Math.round(map.GetY() * 1000) / 1000 +
            "&scale=" + Math.round(map.GetScale() * 1000) / 1000 +
            "&options=" + map.GetOptions() +
            "&style=" + map.GetStyle() +
            (mapElement.classList.contains('galdir') ? '' : '&galdir=0');

      // TODO: Include markers/overlays in any URL updates.

      // $("#permalink").href = href;
      if (document.location.href === href) {
        return;
      }

      // EXPERIMENTAL: update the URL in-place
      if (window.history && window.history.replaceState) {
        window.history.replaceState(null, document.title, href);
      } else {
        // TODO: Update URLs for Google+, Twitter, Facebook, etc.
      }
    }, PERMALINK_REFRESH_DELAY_MS);
  }

  //
  // Pull in options from URL - from permalinks
  //

  var oParams = applyUrlParameters(map);

  if ("galdir" in oParams) {
    var showGalacticDirections = Boolean(Number(oParams.galdir));
    mapElement[showGalacticDirections ? 'add' : 'remove']('galdir');
    $("#ShowGalacticDirections").checked = showGalacticDirections ? 'checked' : '';
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Control Panel of Map Options
  //
  //////////////////////////////////////////////////////////////////////

  function setOption(optionFlag, setFlag) {
    if (setFlag) {
      map.SetOptions(map.GetOptions() | optionFlag);
    } else {
      map.SetOptions(map.GetOptions() & ~optionFlag);
    }
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

  var dataRequest;
  var dataTimeout = 0;
  var lastX, lastY;

  function showCredits(hexX, hexY) {
    if (lastX === hexX && lastY === hexY) {
      return;
    }

    if (dataRequest && dataRequest.abort) {
      dataRequest.abort();
    }
    dataRequest = null;

    if (dataTimeout) {
      window.clearTimeout(dataTimeout);
    }

    function displayResults(data) {
      // TODO: Do this with classes instead?
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
        data.PosterURL = SERVICE_BASE + '/api/poster?sector=' + encodeURIComponent(data.SectorName) + '&accept=application/pdf&style=' + map.GetStyle();
        data.DataURL = SERVICE_BASE + '/api/sec?sector=' + encodeURIComponent(data.SectorName) + '&type=SecondSurvey';
      }

      var template = map.GetScale() >= 16 ? worldMetadataTemplate : sectorMetadataTemplate;
      $("#MetadataDisplay").innerHTML = statusMetadataTemplate(data) + template(data) + commonMetadataTemplate(data);
    }

    var DATA_REQUEST_DELAY_MS = 500;
    dataTimeout = window.setTimeout(function() {
      lastX = hexX;
      lastY = hexY;

      dataRequest = MapService.credits(hexX, hexY, function(data) {
        displayResults(data);
        dataRequest = null;
      }, function (error) {
        $("#MetadataDisplay").innerHTML = "<i>Error: " + error + "</i>";
      });

    }, DATA_REQUEST_DELAY_MS);
  }


  //////////////////////////////////////////////////////////////////////
  //
  // Search
  //
  //////////////////////////////////////////////////////////////////////

  window.txt = $("#SearchResultsTemplate").innerHTML;
  var searchTemplate = Handlebars.compile(window.txt);

  var searchRequest;  
  function search(strSearchText, bActivateUI) {

    // Ensure the search results are maximized
    function showSearchUI() {
      if (bActivateUI) {
        $("#SearchDisplay").classList.remove('progress');
        showControlPanel('search');
      }
    }

    // Transform the search results into clickable links
    function displayResults(data) {
      var searchResults = $("#SearchResults");

      // Pre-process the data
      for (i = 0; i < data.Results.Items.length; ++i) {

        var item = data.Results.Items[i];
        var sx, sy, hx, hy, scale;

        if (item.Subsector) {
          var subsector = item.Subsector,
            index = subsector.Index || "A",
            n = (index.charCodeAt(0) - "A".charCodeAt(0));
          sx = subsector.SectorX|0;
          sy = subsector.SectorY|0;
          hx = (((n % 4) | 0) + 0.5) * (Astrometrics.SectorWidth / 4);
          hy = (((n / 4) | 0) + 0.5) * (Astrometrics.SectorHeight / 4);
          scale = subsector.Scale || 32;

          subsector.onclick = "map.ScaleCenterAtSectorHex(" + scale + "," + sx + "," + sy + "," + hx + "," + hy + "); return false;";
   
        } else if (item.Sector) {
          var sector = item.Sector;
          sx = sector.SectorX|0;
          sy = sector.SectorY|0;
          hx = (Astrometrics.SectorWidth / 2);
          hy = (Astrometrics.SectorHeight / 2);
          scale = sector.Scale || 8;

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

          world.onclick = "map.ScaleCenterAtSectorHex(" + scale + "," + sx + "," + sy + "," + hx + "," + hy + "); return false;";
        }
      }
        
      searchResults.innerHTML = searchTemplate(data);
    }

    if (typeof bActivateUI === 'undefined') {
      bActivateUI = true;
    }

    if (strSearchText === "") {
      return;
    }

    var progressImage;
    if (bActivateUI) {
      // IE stops animated images when submitting a form - restart it
      if (document.images) {
        progressImage = $("#ProgressImage");
        progressImage.src = progressImage.src;
      }

      // NOTE: Do this first in case the response is synchronous (cached)
      $("#SearchDisplay").classList.add('progress');
    }

    if (searchRequest && searchRequest.abort) {
      searchRequest.abort();
    }

    searchRequest = MapService.search(strSearchText, function(data) {
      displayResults(data);
      searchRequest = null;
      showSearchUI();
    }, function (error) {
      $("#SearchResults").innerHTML = "<div><i>Error: " + error + "<" + "/i><" + "/div>";
    });
  }

  // Populate the search pane with stock data
  if ("q" in oParams) {
    search(oParams.q, true);
  } else {
    search("(default)", false);
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Other page setup
  //
  //////////////////////////////////////////////////////////////////////

  // Block unwanted text selection, but allow within text fields (for MSIE)
  document.body.onselectstart = function() { return false; };
  $("#SearchText").onselectstart = function() { window.event.cancelBubble = true; return true; };
  $("#SearchResults").onselectstart = function() { window.event.cancelBubble = true; return true; };

  $("#SelectScale").onmousewheel = function() { window.event.cancelBubble = true; return true; };

  //////////////////////////////////////////////////////////////////////
  //
  // Form Events
  //
  //////////////////////////////////////////////////////////////////////s

  $("#ShowGalacticDirections").onclick = function() { mapElement.classList.toggle('galdir'); updatePermalink(); };
  $("#ShowSectorNames").onclick = function() { setOptions(MapOptions.SectorsMask, this.checked ? MapOptions.SectorsMask : 0); };
  $("#ShowSelectedSectorNames").onclick = function() { setOptions(MapOptions.SectorsMask, MapOptions.SectorsSelected); };
  $("#ShowAllSectorNames").onclick = function() { setOptions(MapOptions.SectorsMask, MapOptions.SectorsAll); };
  $("#ShowGovernmentBorders").onclick = function() { setOptions(MapOptions.BordersMask, this.checked ? MapOptions.BordersMask : 0); };
  $("#ShowGovernmentNames").onclick = function() { setOptions(MapOptions.NamesMask, this.checked ? MapOptions.NamesMask : 0); };
  $("#ShowCapitals").onclick = function() { setOption(MapOptions.WorldsCapitals, this.checked); };
  $("#ShowHomeworlds").onclick = function() { setOption(MapOptions.WorldsHomeworlds, this.checked); };

  $('#cbForceHexes').onclick = function() { setOption(MapOptions.ForceHexes, this.checked); };
  $('#cbWorldColors').onclick = function() { setOption(MapOptions.WorldColors, this.checked); };
  $('#cbFilledBorders').onclick = function() { setOption(MapOptions.FilledBorders, this.checked); };

  $("#SelectStyle").onchange = function() { if (this.selectedIndex !== -1) { map.SetStyle(this.value); } };
  $("#SelectScale").onchange = function() { if (this.selectedIndex !== -1) { map.SetScale(this.value); } };
  $("#ShowSectorGrid").onclick = function() { setOption(MapOptions.SectorGrid | MapOptions.SubsectorGrid, true); };
  $("#HideSectorGrid").onclick = function() { setOption(MapOptions.SectorGrid | MapOptions.SubsectorGrid, false); };

  $("#SearchForm").onsubmit = function() { search($('#SearchText').value); return false; };

  var animate = true, scrollDelta = 200;
  $("#ScrollCoreward").ondblclick = $("#ScrollCoreward").onclick = function() { map.Scroll(0, -scrollDelta, animate); };
  $("#ScrollSpinward").ondblclick = $("#ScrollSpinward").onclick = function() { map.Scroll(-scrollDelta, 0, animate); };
  $("#ScrollTrailing").ondblclick = $("#ScrollTrailing").onclick = function() { map.Scroll(scrollDelta, 0, animate); };
  $("#ScrollRimward").ondblclick = $("#ScrollRimward").onclick = function() { map.Scroll(0, scrollDelta, animate); };

  $("#LogoImage").ondblclick = function() {
    document.body.classList.add('hide-footer');
    map.invalidate();
  };

  $("#Controls").ondblclick = function(e) {
    if (!e) { e = window.event; }
    var target = (e.target) ? e.target : (e.srcElement) ? e.srcElement : null;
    if (target !== $("#Controls")) {
      return;
    }

    document.body.classList.add('hide-controls');
    map.invalidate();
  };

  if (typeof mapElement.focus == 'function') {
    mapElement.focus();
  }

  var MIN_ACCORDION_HEIGHT = 18;
  var accordion = {
    style: { min: MIN_ACCORDION_HEIGHT, max: 310 }, // TODO: Make style block an accordion as well
    search: { min: 45 },
    legend: { min: MIN_ACCORDION_HEIGHT },
    scroll: { min: MIN_ACCORDION_HEIGHT, max: 90 }
  };

  function updateAccordion() {
    // TODO: use CSS classes instead
    $('#style').style.height = ($('#style').classList.contains('accordion_open') ? accordion['style'].max : accordion['style'].min) + 'px';
    $('#search').style.top = $('#style').style.height;

    $('#scroll').style.height = ($('#scroll').classList.contains('accordion_open') ? accordion['scroll'].max : accordion['scroll'].min) + 'px';
    $('#legend').style.bottom = $('#scroll').style.height;

    if (!$('#legend').classList.contains('accordion_open')) {
      $('#legend').style.top = '';
      $('#legend').style.height = accordion['legend'].min + 'px';
      $('#search').style.height = '';
      $('#search').style.bottom = ($('#accordion').offsetHeight - $('#legend').offsetTop) + 'px';
    } else {
      $('#search').style.bottom = '';
      $('#search').style.height = accordion['search'].min + 'px';
      $('#legend').style.height = '';
      $('#legend').style.top = ($('#search').offsetTop + $('#search').clientHeight) + 'px';
    }
  }

  function showControlPanel(pane) {
    Object.keys(accordion).forEach(function(key) {
      $('#' + key).classList[(key === pane) ? 'add' : 'remove']('accordion_open');
    });
    updateAccordion();
  }

  $('#style_toggle').onclick = function(e) {
    $('#style').classList.toggle('accordion_open');
    updateAccordion();
  };
  $('#search_toggle').onclick = function(e) {
    if ($('#search').classList.toggle('accordion_open')) {
      $('#legend').classList.remove('accordion_open');
      $('#style').classList.remove('accordion_open');
      $('#scroll').classList.remove('accordion_open');
    } else {
      $('#legend').classList.add('accordion_open');
    }
    updateAccordion();
  };
  $('#legend_toggle').onclick = function(e) {
    if ($('#legend').classList.toggle('accordion_open')) {
      $('#search').classList.remove('accordion_open');
      $('#style').classList.remove('accordion_open');
      $('#scroll').classList.remove('accordion_open');
    } else {
      $('#search').classList.add('accordion_open');
    }
    updateAccordion();
  };
  $('#scroll_toggle').onclick = function(e) {
    $('#scroll').classList.toggle('accordion_open');
    updateAccordion();
  };
};
