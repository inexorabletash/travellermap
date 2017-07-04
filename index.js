/*global Traveller,Util,Handlebars */ // for lint and IDEs

window.addEventListener('DOMContentLoaded', function() {
  'use strict';

  //////////////////////////////////////////////////////////////////////
  //
  // Utilities
  //
  //////////////////////////////////////////////////////////////////////

  // IE8: document.querySelector can't use bind()
  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return document.querySelectorAll(s); };

  //////////////////////////////////////////////////////////////////////
  //
  // Main Page Logic
  //
  //////////////////////////////////////////////////////////////////////

  // Account for adjustments to innerHeight (dynamic browser UI)
  // (Repro: Safari on iPhone, enter landscape, make url bar appear)
  window.addEventListener('resize', function(e) {
    if (window.innerHeight !== window.outerHeight) {
      document.body.style.height = window.innerHeight + 'px';
      window.scrollTo(0, 0);
    }
  });

  var mapElement = $('#dragContainer'), sizeElement = mapElement.parentNode;
  var map = new Traveller.Map(mapElement, sizeElement);

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
  map.options = map.options | Traveller.MapOptions.NamesMinor | Traveller.MapOptions.ForceHexes;
  map.scale = isSmallScreen ? 1 : 2;
  map.CenterAtSectorHex(0, 0, Traveller.Astrometrics.ReferenceHexX, Traveller.Astrometrics.ReferenceHexY);
  var defaults = {
    x: map.x,
    y: map.y,
    scale: map.scale,
    options: map.options,
    routes: 1,
    dimunofficial: 0,
    milieu: 'M1105',
    style: map.style
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
      style: map.style,
      options: map.options
    };
    NAMED_OPTIONS.forEach(function(name) {
      prefs[name] = map.namedOptions.get(name);
    });
    PARAM_OPTIONS.forEach(function(option) {
      prefs[option.param] = document.body.classList.contains(option.className);
    });
    maybeSave($('#cbSavePreferences').checked, 'preferences', prefs);
    maybeSave($('#cbSaveLocation').checked, 'location', {
      position: { x: map.x, y: map.y },
      scale: map.scale
    });
    maybeSave($('#cbExperiments').checked, 'experiments', {});
  }, SAVE_PREFERENCES_DELAY_MS);

  var template = Util.memoize(function(sel) {
    return Handlebars.compile($(sel).innerHTML);
  });

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

    delete urlParams.sector;
    delete urlParams.subsector;
    delete urlParams.hex;

    urlParams.x = round(map.x, 1/1000);
    urlParams.y = round(map.y, 1/1000);
    urlParams.scale = round(map.scale, 1/128);
    urlParams.options = map.options;
    urlParams.style = map.style;

    var namedOptions = map.namedOptions.keys();
    map.namedOptions.forEach(function(value, key) { urlParams[key] = value; });

    Object.keys(defaults).forEach(function(p) {
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
    $('#share-code').value = '<iframe width=400 height=300 src="' + pageURL + '">';

    var snapshotParams = (function() {
      var map_center_x = map.x,
          map_center_y = map.y,
          scale = map.scale,
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
    snapshotParams.options = map.options;
    snapshotParams.style = map.style;
    snapshotParams.milieu = map.namedOptions.get('milieu');
    namedOptions.forEach(function(name) { snapshotParams[name] = urlParams[name]; });
    var snapshotURL = Traveller.MapService.makeURL('/api/tile', snapshotParams);
    $('a#download-snapshot').href = snapshotURL;
    snapshotParams.accept = 'application/pdf';
    snapshotURL = Traveller.MapService.makeURL('/api/tile', snapshotParams);
    $('a#download-snapshot-pdf').href = snapshotURL;

  }, PERMALINK_REFRESH_DELAY_MS);

  //////////////////////////////////////////////////////////////////////
  //
  // UI Hookup
  //
  //////////////////////////////////////////////////////////////////////

  // Search Bar

  // Mutually exclusive panes:
  var SEARCH_PANES = ['search-results', 'route-ui', 'wds-visible', 'sds-visible'];
  function showSearchPane(pane) {
    if (!['wds-visible', 'sds-visible'].includes(pane))
      document.body.classList.add('ds-mini');
    SEARCH_PANES.forEach(function(c) { document.body.classList.toggle(c, c === pane);  });
  }
  function hideSearchPanes() {
    document.body.classList.add('ds-mini');
    SEARCH_PANES.forEach(function(c) { document.body.classList.remove(c); });
  }
  function hideSearchPanesExcept(pane) {
    if (!['wds-visible', 'sds-visible'].includes(pane))
      document.body.classList.add('ds-mini');
    SEARCH_PANES
      .filter(function(c) { return c !== pane; })
      .forEach(function(c) { document.body.classList.remove(c); });
  }


  $("#searchForm").addEventListener('submit', function(e) {
    search($('#searchBox').value);
    e.preventDefault();
  });

  $("#searchBox").addEventListener('focus', function(e) {
    search($('#searchBox').value, {onfocus: true});
  });

  var SEARCH_TIMER_DELAY = 100; // ms
  $("#searchBox").addEventListener('keyup', Util.debounce(function(e) {
    search($('#searchBox').value, {typed: true});
  }, SEARCH_TIMER_DELAY));

  $('#closeSearchBtn').addEventListener('click', function(e) {
    e.preventDefault();
    $('#searchBox').value = '';
    lastQuery = null;
    document.body.classList.remove('search-results');
    mapElement.focus();
  });

  function resizeMap() {
    if (typeof UIEvent === 'function') {
      var event = new UIEvent('resize');
    } else {
      event = document.createEvent('UIEvent');
      event.initUIEvent('resize', true, false, window, 0);
    }
    (window.dispatchEvent || window.fireEvent)(event);
  }

  $("#routeBtn").addEventListener('click', function(e) {
    showSearchPane('route-ui');
    resizeMap();
    if (selectedWorld) {
      $('#routeStart').value = selectedWorld.name;
      if (!isSmallScreen) $('#routeEnd').focus();
    } else {
      if (!isSmallScreen) $('#routeStart').focus();
    }
  });

  $('#closeRouteBtn').addEventListener('click', function(e) {
    $('#routeStart').value = '';
    $('#routeEnd').value = '';
    $('#routePath').innerHTML = '';
    ['J-1','J-2','J-3','J-4','J-5','J-6'].forEach(function(n) {
      $('#routeForm').classList.remove(n);
    });
    document.body.classList.remove('route-ui');
    document.body.classList.remove('route-shown');
    map.SetRoute(null);
    lastRoute = null;
    resizeMap();
  });

  $('#swapRouteBtn').addEventListener('click', function(e) {
    var tmp = $('#routeStart').value;
    $('#routeStart').value = $('#routeEnd').value;
    $('#routeEnd').value = tmp;
    $('#routePath').innerHTML = '';
    ['J-1','J-2','J-3','J-4','J-5','J-6'].forEach(function(n) {
      if ($('#routeForm').classList.contains(n))
        $('#'+n).click();
    });
  });


  Array.from($$("#routeForm button")).forEach(function(button) {
    button.addEventListener('click', function(e) {
      e.preventDefault();

      ['J-1','J-2','J-3','J-4','J-5','J-6'].forEach(function(n) {
        $('#routeForm').classList.remove(n);
      });
      $('#routeForm').classList.add(button.id);

      var start = $('#routeStart').value;
      var end = $('#routeEnd').value;
      var jump = button.id.replace('J-', '');;

      route(start, end, jump);
    });
  });

  Array.from($$('#routeForm input[type="checkbox"]')).forEach(function(input) {
    input.addEventListener('click', function(e) {
      if ($('#routePath').innerHTML !== '')
        reroute();
    });
  });

  var VK_ESCAPE = KeyboardEvent.DOM_VK_ESCAPE || 0x1B,
      VK_C = KeyboardEvent.DOM_VK_C || 0x43,
      VK_H = KeyboardEvent.DOM_VK_H || 0x48,
      VK_T = KeyboardEvent.DOM_VK_T || 0x54;

  document.body.addEventListener('keyup', function(e) {
    if (e.key === 'Escape' || e.keyCode === VK_ESCAPE) {
      hideSearchPanes();
      selectedWorld = selectedSector = null;
      map.SetRoute(null);
      map.SetMain(null);
      resizeMap();
      mapElement.focus();
    }
  });

  // Options Bar

  var PANELS = ['legend', 'lab', 'milieu', 'settings', 'share', 'help'];
  PANELS.forEach(function(b) {
    $('#'+b+'Btn').addEventListener('click', function() {
      PANELS.forEach(function(p) {
        document.body.classList[b === p ? 'toggle' : 'remove']('show-' + p);
      });
    });
  });
  document.body.addEventListener('keyup', function(e) {
    if (e.key === 'Escape' || e.keyCode === VK_ESCAPE) {
      PANELS.forEach(function(p) {
        document.body.classList.remove('show-' + p);
      });
    }
  });

  var STYLES = ['poster', 'atlas', 'print', 'candy', 'draft', 'fasa', 'terminal'];
  STYLES.forEach(function(s) {
    $('#settingsBtn-'+s).addEventListener('click', function() { map.style = s; });
  });

  $('#homeBtn').addEventListener('click', goHome);

  function goHome() {
    if (['sx', 'sy', 'hx', 'hy'].every(function(p) { return ('yah_' + p) in urlParams; })) {
      map.CenterAtSectorHex(
        urlParams.yah_sx|0, urlParams.yah_sy|0,
        urlParams.yah_hx|0, urlParams.yah_hy|0,
        {scale: 64});
      return;
    }
    map.cancelAnimation();
    map.scale = home.scale;
    map.x = home.x;
    map.y = home.y;
  }

  Array.from($$('#share-url,#share-code')).forEach(function(input) {
    input.addEventListener('click', function(e) {
      e.preventDefault();
      input.focus();
      input.select();
      input.setSelectionRange(0, input.value.length); // .select() fails on iOS
    });
    input.addEventListener('mouseup', function(e) {
      e.preventDefault();
    });
  });

  // Nav Bar

  $('#zoomInBtn').addEventListener('click', map.ZoomIn.bind(map));
  $('#zoomOutBtn').addEventListener('click', map.ZoomOut.bind(map));
  $('#tiltBtn').addEventListener('click', toggleTilt);

  function toggleTilt() {
    $('#cbTilt').click();
  };

  // Bottom Panel

  $("#LogoImage").addEventListener('dblclick', function() {
    document.body.classList.add('hide-footer');
  });

  // Keyboard Shortcuts

  mapElement.addEventListener('keydown', function(e) {
    if (e.ctrlKey || e.altKey || e.metaKey)
      return;
    if (e.key === 'c' || e.keyCode === VK_C) {
      e.preventDefault();
      e.stopPropagation();
      updateContext(map.worldX, map.worldY, {directAction: true});
      showMain(map.worldX, map.worldY);
      return;
    }
    if (e.key === 'h' || e.keyCode === VK_H) {
      e.preventDefault();
      e.stopPropagation();
      goHome();
      return;
    }
    if (e.key === 't' || e.keyCode === VK_T) {
      e.preventDefault();
      e.stopPropagation();
      toggleTilt();
      return;
    }
  });

  //////////////////////////////////////////////////////////////////////
  //
  // Options UI Binding
  //
  //////////////////////////////////////////////////////////////////////

  function setOptions(mask, flags) {
    map.options = (map.options & ~mask) | flags;
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

  bindRadioToNamedOption('input[type=radio][name=milieu]', 'milieu');

  bindCheckedToNamedOption('#cbImpOverlay', 'im');
  bindCheckedToNamedOption('#cbPopOverlay', 'po');
  bindCheckedToNamedOption('#cbCapitalOverlay', 'cp');
  bindCheckedToNamedOption('#cbDroyneWorlds', 'dw');
  bindCheckedToNamedOption('#cbAncientWorlds', 'an');
  bindCheckedToNamedOption('#cbMinorHomeworlds', 'mh');
  bindCheckedToNamedOption('#cbStellar', 'stellar');
  bindCheckedToNamedOption('#cbQZ', 'qz');
  bindChecked('#cbWave',
    function(o) { return map.namedOptions.get('ew'); },
    function(c) {
      if (c) {
        map.namedOptions.set('ew', 'milieu');
      } else {
        map.namedOptions.delete('ew');
        delete urlParams['ew'];
      }
    });

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
                function() { var v = map.namedOptions.get(name);
                             return v === undefined ? defaults[name] : v; },
                function(c) {
                  if (!!c === !!defaults[name]) {
                    delete urlParams[name];
                    map.namedOptions.delete(name);
                  } else map.namedOptions.set(name, c ? 1 : 0); });
  }
  function bindRadioToNamedOption(selector, name) {
    optionObservers.push(function(o) {
      var v = map.namedOptions.get(name);
      if (v === undefined) v = defaults[name];
      var e = $(selector + '[value="' +  v + '"]');
      if (e) e.checked = true;
    });
    Array.from($$(selector)).forEach(function(elem) {
      elem.addEventListener('click', function(event) {
        if (elem.value === defaults[name]) {
          delete urlParams[name];
          map.namedOptions.delete(name);
        } else {
          map.namedOptions.set(name, elem.value);
        }
      });
    });
  }

  map.OnOptionsChanged = function(options) {
    optionObservers.forEach(function(o) { o(options); });
    $('#legendBox').classList.toggle('world_colors', options & Traveller.MapOptions.WorldColors);
    updateContext(lastX || map.worldX, lastY || map.worldY, {refresh: true});
    updatePermalink();
    savePreferences();
  };

  map.OnStyleChanged = function(style) {
    STYLES.forEach(function(s) {
      document.body.classList.toggle('style-' + s, s === style);
    });
    updateContext(lastX || map.worldX, lastY || map.worldY, {refresh: true});
    updatePermalink();
    updateScaleIndicator();
    savePreferences();
  };

  map.OnScaleChanged = function() {
    updateContext(lastX || map.worldX, lastY || map.worldY);
    updatePermalink();
    updateScaleIndicator();
    savePreferences();
  };

  map.OnPositionChanged = function() {
    updateContext(map.worldX, map.worldY);
    updatePermalink();
    savePreferences();
  };

  function post(message) {
    if (window.parent !== window && 'postMessage' in window.parent) {
      // Fails cross-domain in IE10-
      try { window.parent.postMessage(message, '*'); } catch (_) {}
    }
  }

  map.OnClick = function(data) {
    updateContext(data.x, data.y, {directAction: true, activeElement: data.activeElement});
    showMain(data.x, data.y);
    post({source: 'travellermap', type: 'click', location: {x: data.x, y: data.y}});
  };

  map.OnDoubleClick = function(world) {
    updateContext(world.x, world.y, {directAction: true});
    showMain(world.x, world.y);
    post({source: 'travellermap', type: 'doubleclick', location: world});
  };


  var NAMED_OPTIONS = [
    'routes', 'dimunofficial',
    'milieu',
    'im', 'po', 'dw', 'an', 'mh', 'stellar'
  ];

  // TODO: Generalize URLParam<->Control and URLParam<->Style binding
  var PARAM_OPTIONS = [
    {param: 'galdir', selector: '#cbGalDir', className: 'show-directions', 'default': true},
    {param: 'tilt', selector: '#cbTilt', className: 'tilt', 'default': false,
     onchange: function(flag) { if (flag) map.EnableTilt(); }
    },
    {param: 'mains', selector: '#cbMains', className: 'show-mains', 'default': false,
     onchange: function(flag) { if (!flag) map.SetMain(null); }
    }
  ];
  PARAM_OPTIONS.forEach(function(option) {
    $(option.selector).checked = option['default'];
    document.body.classList.toggle(option.className, option['default']);
    $(option.selector).addEventListener('click', function() {
      document.body.classList.toggle(option.className, this.checked);
      updatePermalink();
      savePreferences();
      if (option.onchange)
        option.onchange(this.checked);
    });
  });

  (function() {
    if (isIframe) return;
    var preferences = JSON.parse(localStorage.getItem('preferences'));
    var location = JSON.parse(localStorage.getItem('location'));
    var experiments = JSON.parse(localStorage.getItem('experiments'));
    if (preferences) {
      $('#cbSavePreferences').checked = true;
      if ('style' in preferences) map.style = preferences.style;
      if ('options' in preferences) map.options = preferences.options;
      NAMED_OPTIONS.forEach(function(name) {
        if (name in preferences) map.namedOptions.set(name, preferences[name]);
      });

      PARAM_OPTIONS.forEach(function(option) {
        if (option.param in preferences) {
          document.body.classList.toggle(option.className, preferences[option.param]);
          if (option.onchange)
            option.onchange(preferences[option.param]);
        }
      });
    }

    if (location) {
      $('#cbSaveLocation').checked = true;
      if ('scale' in location) map.scale = location.scale;
      if ('position' in location) { map.x = location.position.x; map.y = location.position.y; }
    }

    if (experiments) {
      $('#cbExperiments').checked = true;
      document.body.classList.add('enable-experiments');
    }
  }());

  $('#cbSavePreferences').addEventListener('click', savePreferences);
  $('#cbSaveLocation').addEventListener('click', savePreferences);
  $('#cbExperiments').addEventListener('click', function() {
    savePreferences();
    document.body.classList.toggle('enable-experiments', this.checked);
  });


  //
  // Pull in options from URL - from permalinks
  //
  // Call this AFTER data binding is hooked up so UI is synchronized
  //
  var standalone = 'standalone' in window.navigator && window.navigator.standalone;
  var urlParams = standalone ? {} : map.ApplyURLParameters();

  // Force UI to synchronize in case URL parameters didn't do it
  map.OnOptionsChanged(map.options);

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
      document.body.classList.toggle(option.className, show);
      dirty = true;
    }
    $(option.selector).checked = document.body.classList.contains(option.className);
  });
  if (dirty) updatePermalink();

  ['q', 'qn'].forEach(function(key) {
    if (key in urlParams) {
      $('#searchBox').value = urlParams[key];
      search(urlParams[key], {navigate: key === 'qn'});
    }
  });

  if ('attract' in urlParams) {
    // TODO: Disable UI, or make any UI interaction cancel
    doAttract();
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Attract Mode
  //
  //////////////////////////////////////////////////////////////////////

  function doAttract() {
    function pickTarget() {
      return Traveller.MapService.search('(random world)', {
        milieu: map.namedOptions.get('milieu')
      }, 'POST').then(function(data) {
        var items = data.Results.Items;
        if (items.length < 1)
          throw new Error('random world search failed');
        var world = items[0].World;
        var tags = world.SectorTags.split(/\s+/);
        return tags.includes('OTU') ? world : pickTarget();
      });
    }

    var HOME_WAIT_MS = 5e3;
    var TARGET_WAIT_MS = 10e3;

    goHome();
    pickTarget().then(function(world) {
      var target = Traveller.Astrometrics.sectorHexToMap(
        world.SectorX, world.SectorY, world.HexX, world.HexY);
      setTimeout(function() {
        map.animateTo(128, target.x, target.y, 10).then(function() {
          updateContext(map.worldX, map.worldY, {directAction: true});
          setTimeout(doAttract, TARGET_WAIT_MS);
        }, function() {});
      }, HOME_WAIT_MS);
    });
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Metadata
  //
  //////////////////////////////////////////////////////////////////////

  var dataRequest = null;
  var dataTimeout = 0;
  var lastX, lastY, lastMilieu;
  var selectedSector = null;
  var selectedWorld = null;
  var ignoreIndirect = false;
  var enableContext;

  function makeWikiURL(suffix) {
    return 'http://wiki.travellerrpg.com/' + encodeURIComponent(suffix.replace(/ /g, '_'));
  }

  function updateContext(worldX, worldY, options) {
    if (!enableContext || document.body.classList.contains('hide-ui'))
      return;

    options = Object.assign({}, options);

    if (ignoreIndirect && !options.directAction)
      return;
    ignoreIndirect = options.ignoreIndirect;

    var DATA_REQUEST_DELAY_MS = 100;
    var milieu = map.namedOptions.get('milieu');

    if (!(options.directAction || options.refresh)
        && lastX === worldX && lastY === worldY && lastMilieu === milieu)
      return;

    lastX = worldX;
    lastY = worldY;
    lastMilieu = milieu;

    if (dataRequest) {
      dataRequest.ignore();
      dataRequest = null;
    }

    if (dataTimeout)
      window.clearTimeout(dataTimeout);

    if (map.scale < 1) {
      displayResults({});
    } else {
      dataTimeout = setTimeout(function() {
        dataRequest = Util.ignorable(Traveller.MapService.credits(worldX, worldY, {
          milieu: milieu
        }));
        dataRequest.then(function(data) {
          dataRequest = null;
          displayResults(data);
        });
      }, options.directAction || options.refresh ? 0 : DATA_REQUEST_DELAY_MS);
    }

    function displayResults(data) {
      if ('SectorTags' in data) {
        var tags =  String(data.SectorTags).split(/\s+/);
        data.Unofficial = true;
        ['Official', 'InReview', 'Unreviewed', 'Apocryphal', 'Preserve']
          .filter(function(tag) { return tags.includes(tag); })
          .forEach(function(tag) {
            delete data.Unofficial;
            data[tag] = true;
          });
      } else {
        data.Unmapped = true;
      }

      data.Attribution = ['SectorAuthor', 'SectorSource', 'SectorPublisher']
        .filter(function(p) { return p in data; })
        .map(function(p) { return data[p]; })
        .join(', ');

      if (data.SectorName) {
        data.SectorWikiURL = makeWikiURL(data.SectorName + ' Sector');
        data.SectorWikiURLNoScheme = data.SectorWikiURL.replace(/^\w+:\/\//, '');
        data.BookletURL = Traveller.MapService.makeURL(
          '/data/' + encodeURIComponent(data.SectorName) + '/booklet', {
            milieu: milieu
          });

        data.PosterURL = Traveller.MapService.makeURL('/api/poster', {
          sector: data.SectorName,
          accept: 'application/pdf',
          style: map.style,
          options: map.options,
          milieu: milieu
        });
        data.DataURL = Traveller.MapService.makeURL('/api/sec', {
          sector: data.SectorName, type: 'SecondSurvey',
          milieu: milieu
        });
      }

      if (data.SectorY)
        data.SectorY = -data.SectorY;

      // Credits
      $('#MetadataDisplay').innerHTML = template('#MetadataTemplate')(data);

      // Sector/World Data Sheets
      if (options.directAction && document.body.classList.contains('route-ui')) {
        selectedSector = null;
        selectedWorld = null;
        if (options.activeElement === $('#routeStart')) {
          if (data.WorldName) {
            $('#routeStart').value = data.WorldName;
            $('#routeEnd').focus();
          } else {
            $('#routeStart').focus();
          }
        } else if (options.activeElement === $('#routeEnd')) {
          if (data.WorldName) {
            $('#routeEnd').value = data.WorldName;
            $('#J-2').click();
          } else {
            $('#routeEnd').focus();
          }
        } else if ($('#routeStart').value === '' && data.WorldName) {
          $('#routeStart').value = data.WorldName;
          if (!isSmallScreen) $('#routeEnd').focus();
        } else if ($('#routeEnd').value === '' && data.WorldName) {
          $('#routeEnd').value = data.WorldName;
          $('#J-2').click();
        }
        return;
      } else if (options.directAction && !document.body.classList.contains('route-ui')) {
        mapElement.focus();
        selectedSector = ('SectorName' in data && 'SectorTags' in data) ? data.SectorName : null;
        selectedWorld = map.scale > 16 && 'WorldHex' in data
          ? { name: data.WorldName, hex: data.WorldHex } : null;
      } else if (options.refresh) {
        // Keep as-is
      } else {
        selectedSector = null;
        selectedWorld = null;
      }

      if (selectedSector && (options.refresh || options.directAction)) {
        $('#sds-data').innerHTML = template('#sds-template')(data);
        // Hook up toggle
        $('#sds-data .ds-mini-toggle').addEventListener('click', function(event) {
          document.body.classList.toggle('ds-mini');
        });
      }

      if (options.directAction) {
        document.body.classList.toggle('sector-selected', selectedSector);
        document.body.classList.toggle('world-selected', selectedWorld);

        if (selectedWorld) {
          showSearchPane('wds-visible');
          showWorldData();
        } else if (selectedSector && map.scale <= 16) {
          showSearchPane('sds-visible');
        } else {
          hideSearchPanes();
        }
      }
    }
  }

  function showWorldData() {
    if (!selectedWorld)
      return;
    var context = {
      sector: selectedSector,
      hex: selectedWorld.hex
    };
    $('#wds-spinner').style.display = 'block';
    var milieu = map.namedOptions.get('milieu');
    // World Data Sheet ("Info Card")
    fetch(Traveller.MapService.makeURL(
      '/api/jumpworlds?', {
        sector: context.sector, hex: context.hex,
        milieu: milieu,
        jump: 0
      }))
      .then(function(response) {
        if (!response.ok) throw Error(response.statusText);
        return response.json();
      })
      .then(function(data) {
        return Traveller.prepareWorld(data.Worlds[0]);
      })
      .then(function(world) {
        if (!world) return undefined;
        return Traveller.renderWorldImage(world, $('#wds-world-image'));
      })
      .then(function(world) {
        if (!world) return;

        // Data Sheet
        world.DataSheetURL = Util.makeURL('world', {
          sector: context.sector,
          hex: context.hex,
          milieu: milieu,
          style: map.style,
          print: true
        });

        // Jump Maps
        world.JumpMapURL = Traveller.MapService.makeURL('/api/jumpmap', {
          sector: context.sector,
          hex: context.hex,
          milieu: milieu,
          style: map.style,
          options: map.options &
            (Traveller.MapOptions.BordersMask | Traveller.MapOptions.NamesMask |
             Traveller.MapOptions.WorldColors | Traveller.MapOptions.FilledBorders)
        });

       $('#wds-data').innerHTML = template('#wds-template')(world);

        // Hook up any generated "expandy" fields
        Array.from($$('.wds-expandy')).forEach(function(elem) {
          elem.addEventListener('click', function(event) {
            var c = elem.getAttribute('data-wds-expand');
            $('#wds-frame').classList.toggle(c);
          });
        });

        // Hook up toggle
        $('#wds-data .ds-mini-toggle').addEventListener('click', function(event) {
          document.body.classList.toggle('ds-mini');
        });
      })
      .catch(function(error) {
        console.warn(error);
      })
      .then(function() {
        $('#wds-spinner').style.display = 'none';
      });
  }

  Array.from($$('.ds-closebtn,#ds-shade')).forEach(function(element) {
    element.addEventListener('click', function(event) {
      hideSearchPanes();
      selectedWorld = selectedSector = null;
    });
  });

  //////////////////////////////////////////////////////////////////////
  //
  // Search
  //
  //////////////////////////////////////////////////////////////////////

  var searchRequest = null;
  var lastQuery = null;

  function search(query, options) {
    options = Object.assign({}, options);

    hideSearchPanesExcept('search-results');
    selectedWorld = selectedSector = null;
    map.SetRoute(null);

    if (query === '')
      query = '(default)';

    if (query === lastQuery) {
      if (!searchRequest && !options.typed)
        showSearchPane('search-results');
      return;
    }
    lastQuery = query;

    if (!options.typed)
      document.body.classList.remove('search-results');

    if (searchRequest)
      searchRequest.ignore();

    searchRequest = Util.ignorable(Traveller.MapService.search(query, {
      milieu: map.namedOptions.get('milieu')
    }));
    searchRequest
      .then(function(data) {
        displayResults(data);
      }, function() {
        $('#resultsContainer').innerHTML = '<i>Error fetching results.</i>';
      })
      .then(function() {
        searchRequest = null;
        showSearchPane('search-results');
        if (options.navigate) {
          var first = $('#resultsContainer a');
          if (first)
            first.click();
        }
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
          sector.href = Util.makeURL(base_url, {
            scale: scale, sx: sx, sy: sy, hx: hx, hy: hy,
            sector: sector.Name
          });
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
          var params = {scale: scale, sx: sx, sy: sy, hx: hx, hy: hy};
          if (!data.Tour) {
            params = Object.assign(params,
                                   {sector: world.Sector, world: world.Name, hex: world.Hex});
          }
          world.href = Util.makeURL(base_url, params);
          applyTags(world);
        } else if (item.Label) {
          var label = item.Label;
          sx = label.SectorX | 0;
          sy = label.SectorY | 0;
          hx = label.HexX | 0;
          hy = label.HexY | 0;
          scale = label.Scale || 64;
          label.href = Util.makeURL(base_url, { scale: scale, sx: sx, sy: sy, hx: hx, hy: hy });
          applyTags(label);
        }
      }

      $('#resultsContainer').innerHTML = template('#SearchResultsTemplate')(data);

      Array.from($$('#resultsContainer a')).forEach(function(a) {
        a.addEventListener('click', function(e) {
          e.preventDefault();
          selectedWorld = null;

          var params = Util.parseURLQuery(e.target);
          map.CenterAtSectorHex(params.sx|0, params.sy|0, params.hx|0, params.hy|0, {scale: params.scale|0});
          if (mapElement.offsetWidth < 640)
            document.body.classList.remove('search-results');

          var coords = Traveller.Astrometrics.sectorHexToWorld(
            params.sx|0, params.sy|0, params.hx|0, params.hy|0);

          if (params.world && params.sector) {
            selectedSector = params.sector;
            selectedWorld = { name: params.name, hex: params.hex };
            updateContext(coords.x, coords.y, {directAction: true, ignoreIndirect: true});
          } else if (params.sector) {
            selectedSector = params.sector;
            updateContext(coords.x, coords.y, {directAction: true, ignoreIndirect: true});
          }
        });
      });

      var first = $('#resultsContainer a');
      if (first && !options.typed && !options.onfocus)
        setTimeout(function() { first.focus(); }, 0);
    }
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Route Search
  //
  //////////////////////////////////////////////////////////////////////

  var lastRoute = null;
  function reroute() {
    if (lastRoute) route(lastRoute.start, lastRoute.end, lastRoute.jump);
  }

  function route(start, end, jump) {
    $('#routePath').innerHTML = '';
    lastRoute = {start:start, end:end, jump:jump};

    var options = {
      start: start, end: end, jump: jump,
      x: map.worldX, y: map.worldY,
      milieu: map.namedOptions.get('milieu'),
      wild: $('#route-wild').checked?1:0,
      im: $('#route-im').checked?1:0,
      nored: $('#route-nored').checked?1:0
    };
    fetch(Traveller.MapService.makeURL('/api/route', options))
      .then(function(response) {
        if (!response.ok) return response.text();
        return response.json();
      })
      .then(function(data) {
        if (typeof data === 'string') throw new Error(data);

        var base_url = document.location.href.replace(/\?.*/, '');
        var route = [];
        var total = 0;
        data.forEach(function(world, index) {
          world.Name = world.Name || '(Unnamed)';
          var sx = world.SectorX|0;
          var sy = world.SectorY|0;
          var hx = world.HexX|0;
          var hy = world.HexY|0;
          var scale = 64;
          world.href = Util.makeURL(base_url, {scale: 64, sx: sx, sy: sy, hx: hx, hy: hy});

          if (index > 0) {
            var prev = data[index - 1];
            var a = Traveller.Astrometrics.sectorHexToWorld(
              prev.SectorX|0, prev.SectorY|0, prev.HexX|0, prev.HexY|0);
            var b = Traveller.Astrometrics.sectorHexToWorld(sx, sy, hx, hy);
            var dist = Traveller.Astrometrics.hexDistance(a.x, a.y, b.x, b.y);
            prev.Distance = dist;
            total += dist;
          }

          route.push({sx:sx, sy:sy, hx:hx, hy:hy});
        });

        map.SetRoute(route);
        $('#routePath').innerHTML = template('#RouteResultsTemplate')({
          Route: data,
          Distance: total,
          Jumps: data.length - 1,
          PrintURL: Util.makeURL('./print/route', options)
        });
        document.body.classList.add('route-shown');
        resizeMap();

        Array.from($$('#routePath .item a')).forEach(function(a) {
          a.addEventListener('click', function(e) {
            e.preventDefault();
            selectedWorld = null;

            var params = Util.parseURLQuery(e.target);
            map.CenterAtSectorHex(params.sx|0, params.sy|0, params.hx|0, params.hy|0, {scale: params.scale|0});
          });
        });

      })
      .catch(function(reason) {
        $('#routePath').innerHTML = template('#RouteErrorTemplate')({Message: reason.message});
        map.SetRoute(null);
        document.body.classList.add('route-shown');
        resizeMap();
      });
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Scale Indicator
  //
  //////////////////////////////////////////////////////////////////////

  var isCanvasSupported = ('getContext' in $('#scaleIndicator')),
      animId = 0;
  function updateScaleIndicator() {
    if (!isCanvasSupported) return;

    cancelAnimationFrame(animId);
    animId = requestAnimationFrame(function() {
      var scale = map.scale,
          canvas = $('#scaleIndicator'),
          ctx = canvas.getContext('2d'),
          w = parseFloat(canvas.width),
          h = parseFloat(canvas.height),
          style = map.style,
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
  // Mains
  //
  //////////////////////////////////////////////////////////////////////

  var worldToMainMap;
  function showMain(worldX, worldY) {
    if (!$('#cbMains').checked) return;
    findMain(worldX, worldY)
      .then(function(main) { map.SetMain(main); });
  }
  function findMain(worldX, worldY) {
    function sectorHexToSig(sx, sy, hx, hy) {
      return sx + '/' + sy + '/' + ('0000' + ( hx * 100 + hy )).slice(-4);
    }
    function sigToSectorHex(sig) {
      var parts = sig.split('/');
      return {sx: parts[0]|0, sy: parts[1]|0, hx: (parts[2] / 100) | 0, hy: parts[2] % 100};
    }

    function getMainsMapping() {
      if (worldToMainMap)
        return Promise.resolve(worldToMainMap);
      worldToMainMap = new Map();
      return fetch('./res/mains.json')
        .then(function(r) { return r.json(); })
        .then(function(mains) {
          mains.forEach(function(main) {
            main.forEach(function(sig, index) {
              worldToMainMap.set(sig, main);
              main[index] = sigToSectorHex(sig);
            });
          });
          return worldToMainMap;
        });
    }
    return getMainsMapping().then(function(map) {
      var sectorHex = Traveller.Astrometrics.worldToSectorHex(worldX, worldY);
      var sig = sectorHexToSig(sectorHex.sx, sectorHex.sy, sectorHex.hx, sectorHex.hy);
      return map.get(sig);
    });
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Final setup
  //
  //////////////////////////////////////////////////////////////////////

  if (!isIframe)
    mapElement.focus();

  $('#searchBox').disabled = false;

  // Init all of the "social" UI asynchronously.
  setTimeout(window.initSharingLinks, 5000);
  $('#shareBtn').addEventListener('click', function() {
    window.initSharingLinks();
  });

  // After all async events from the map have fired...
  setTimeout(function() { enableContext = true; }, 0);
});
