/*global Traveller,Util,Handlebars */ // for lint and IDEs
(global => {
  'use strict';

  //////////////////////////////////////////////////////////////////////
  //
  // Utilities
  //
  //////////////////////////////////////////////////////////////////////

  const $ = s => document.querySelector(s);
  const $$ = s => Array.from(document.querySelectorAll(s));

  //////////////////////////////////////////////////////////////////////
  //
  // Main Page Logic
  //
  //////////////////////////////////////////////////////////////////////

  // Account for adjustments to innerHeight (dynamic browser UI)
  // (Repro: Safari on iPhone, enter landscape, make url bar appear)
  function resizeWindow() {
    if (window.innerHeight !== window.outerHeight && !navigator.standalone) {
      document.body.style.height = window.innerHeight + 'px';
      document.documentElement.style.height = window.innerHeight + 'px';
      window.scrollTo(0, 0);
    } else if (document.body.style.height !== '') {
      document.body.style.height = '';
      document.documentElement.style.height = '';
      window.scrollTo(0, 0);
    }
  }
  window.addEventListener('resize', event => {
    // Timeout to work around iOS Safari giving incorrect sizes while 'resize'
    // dispatched.
    setTimeout(resizeWindow, 100);
  });
  // Issues on load on iOS (non-standalone); just leave this running.
  if (navigator.userAgent.match(/iPad|iPhone|iPod/)) {
    if (navigator.standalone) {
      setTimeout(resizeMap, 500);
    } else {
      setInterval(resizeWindow, 500);
    }
  }

  const mapElement = $('#dragContainer'), sizeElement = mapElement.parentNode;
  const map = new Traveller.Map(mapElement, sizeElement);

  // Export
  window.map = map;

  const isIframe = (window != window.top); // != for IE
  const isSmallScreen = mapElement.offsetWidth <= 640; // Arbitrary

  function toggleFullscreen() {
    if (document.fullscreen || document.webkitIsFullScreen) {
      if ('exitFullscreen' in document)
        document.exitFullscreen();
      else if ('webkitExitFullscreen' in document)
        document.webkitExitFullscreen();
    } else {
      const elem = document.documentElement;
      if ('requestFullscreen' in elem)
        elem.requestFullscreen();
      else if ('webkitRequestFullscreen' in elem)
        elem.webkitRequestFullscreen();
    }
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Parameters and Style
  //
  //////////////////////////////////////////////////////////////////////

  // Tweak defaults
  map.options = map.options | Traveller.MapOptions.NamesMinor | Traveller.MapOptions.ForceHexes | Traveller.MapOptions.FilledBorders;
  map.scale = isSmallScreen ? 1 : 2;
  map.CenterAtSectorHex(0, 0, Traveller.Astrometrics.ReferenceHexX, Traveller.Astrometrics.ReferenceHexY);
  const defaults = {
    x: map.x,
    y: map.y,
    scale: map.scale,
    options: map.options,
    routes: 1,
    dimunofficial: 0,
    milieu: 'M1105',
    style: map.style
  };
  const home = {
    x: defaults.x,
    y: defaults.y,
    scale: defaults.scale
  };

  const SAVE_PREFERENCES_DELAY_MS = 500;
  const savePreferences = isIframe ? () => {} : Util.debounce(() => {
    const preferences = {
      style: map.style,
      options: map.options
    };
    map.namedOptions.NAMES.forEach(name => {
      const value = map.namedOptions.get(name);
      preferences[name] = value === '' ? undefined : value;
    });
    PARAM_OPTIONS.forEach(option => {
      preferences[option.param] = document.body.classList.contains(option.className);
    });
    localStorage.setItem('preferences', JSON.stringify(preferences));
    localStorage.setItem('location', JSON.stringify({
      position: { x: map.x, y: map.y },
      scale: map.scale
    }));
  }, SAVE_PREFERENCES_DELAY_MS);

  const template = Util.memoize(sel => Handlebars.compile($(sel).innerHTML));

  const jump_button_ids = $$('.jump-button').map(e => e.id);
  const milieu_choices =
        $$('#milieu-choices input[type=radio][name=milieu]')
        .map(e => e.value);

  //////////////////////////////////////////////////////////////////////
  //
  // Permalink
  //
  //////////////////////////////////////////////////////////////////////

  const PERMALINK_REFRESH_DELAY_MS = 500;
  let lastPageURL = null;
  const updatePermalink = Util.debounce(() => {
    function round(n, d) {
      d = 1 / d; // Avoid twitchy IEEE754 rounding.
      return Math.round(n * d) / d;
    }

    delete urlParams.x;
    delete urlParams.y;
    delete urlParams.scale;
    delete urlParams.sector;
    delete urlParams.subsector;
    delete urlParams.hex;

    urlParams.p = [
      round(map.x, 1/1000),
      round(map.y, 1/1000),
      round(map.logScale, 1/100)
    ].join('!');
    urlParams.options = map.options;
    urlParams.style = map.style;

    const namedOptions = map.namedOptions.keys();
    map.namedOptions.forEach((value, key) => {
      urlParams[key] = value;
    });

    Object.keys(defaults).forEach(p => {
      if (urlParams[p] === defaults[p]) delete urlParams[p];
    });

    PARAM_OPTIONS.forEach(option => {
      if (document.body.classList.contains(option.className) === option['default'])
        delete urlParams[option.param];
      else
        urlParams[option.param] = 1;
    });

    const pageURL = Util.makeURL(document.location, urlParams);

    if (pageURL === lastPageURL)
      return;

    if ('history' in window && 'replaceState' in window.history && document.location.href !== pageURL)
      window.history.replaceState(null, document.title, pageURL);

    $('#share-url').value = pageURL;
    $('#share-code').value = `<iframe width=400 height=300 src="${pageURL}">`;

    ['share-url', 'share-code'].forEach(share => {
      $(`#copy-${share}`).addEventListener('click', event => {
        event.preventDefault();
        Util.copyTextToClipboard($(`#${share}`).value);
      });
    });

    $$('a.share').forEach(anchor => {
      const data = {
        url: encodeURIComponent(pageURL),
        text: encodeURIComponent('The Traveller Map')
      };
      anchor.__func = anchor.__func || Handlebars.compile(anchor.getAttribute('data-template'));
      anchor.href = anchor.__func(data);
    });


    const snapshotParams = (() => {
      const map_center_x = map.x,
            map_center_y = map.y,
            scale = map.scale,
            rect = mapElement.getBoundingClientRect(),
            width = Math.round(rect.width),
            height = Math.round(rect.height),
            x = ( map_center_x * scale - ( width / 2 ) ) / width,
            y = ( -map_center_y * scale - ( height / 2 ) ) / height;
      return { x, y, w: width, h: height, scale };
    })();
    snapshotParams.x = round(snapshotParams.x, 1/1000);
    snapshotParams.y = round(snapshotParams.y, 1/1000);
    snapshotParams.scale = round(snapshotParams.scale, 1/128);
    snapshotParams.options = map.options;
    snapshotParams.style = map.style;
    snapshotParams.milieu = map.namedOptions.get('milieu');
    namedOptions.forEach(name => { snapshotParams[name] = urlParams[name]; });
    let snapshotURL = Traveller.MapService.makeURL('/api/tile', snapshotParams);
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

  let lastX, lastY, lastMilieu;
  let selectedSector = null;
  let selectedWorld = null;
  let lastRoute = null;
  let lastQuery = null, lastQueryRoute = null;

  const original_title = document.title;

  // Search Bar

  // Mutually exclusive panes:
  const SEARCH_PANES = ['search-results', 'route-ui', 'wds-visible', 'sds-visible'];
  function showSearchPane(pane, title) {
    if (!['wds-visible', 'sds-visible'].includes(pane))
      document.body.classList.add('ds-mini');
    SEARCH_PANES.forEach(c => { document.body.classList.toggle(c, c === pane);  });
    map.SetRoute(null);

    document.title = title ? `${title} - ${original_title}` : original_title;
  }

  function hideSearch() {
    document.body.classList.remove('search-results');
    map.SetRoute(null);
  }

  function hideCards() {
    document.body.classList.remove('wds-visible');
    document.body.classList.remove('sds-visible');
    document.body.classList.add('ds-mini');
    document.title = original_title;
  }


  $("#searchForm").addEventListener('submit', event => {
    search($('#searchBox').value, {onsubmit: true});
    event.preventDefault();
  });

  $("#searchBox").addEventListener('focus', event => {
    search($('#searchBox').value, {onfocus: true});
  });

  const SEARCH_TIMER_DELAY = 100; // ms
  $("#searchBox").addEventListener('input', Util.debounce(e => {
    if (e.key !== 'Enter') // Ignore double-submit on iOS
      search($('#searchBox').value, {typed: true});
  }, SEARCH_TIMER_DELAY));

  $('#closeSearchBtn').addEventListener('click', event => {
    event.preventDefault();
    $('#searchBox').value = '';
    lastQuery = null;
    hideSearch();
    mapElement.focus();
  });

  function resizeMap() {
    let event;
    if (typeof UIEvent === 'function') {
      event = new UIEvent('resize');
    } else {
      event = document.createEvent('UIEvent');
      event.initUIEvent('resize', true, false, window, 0);
    }
    (window.dispatchEvent || window.fireEvent)(event);
  }

  $("#routeBtn").addEventListener('click', event => {
    if (selectedWorld) {
      setRouteStart(selectedWorld.name, selectedWorld.hex, selectedSector);
      if (!isSmallScreen) $('#routeEnd').focus();
    } else {
      if (!isSmallScreen) $('#routeStart').focus();
    }
    showRoute();
  });

  // Track precise route start/end location to use by default, as
  // world names can be ambiguous (e.g. Inthe and Aramis in SPIN)
  let routeStart = undefined, routeEnd = undefined;
  function setRouteStart(name, hex, sector) {
    $('#routeStart').value = name;
    routeStart = { name, hex, sector };
  }
  function setRouteEnd(name, hex, sector) {
    $('#routeEnd').value = name;
    routeEnd= { name, hex, sector };
  }
  function clearRouteStart() {
    $('#routeStart').value = '';
    routeStart = undefined;
  }
  function clearRouteEnd() {
    $('#routeEnd').value = '';
    routeEnd = undefined;
  }
  $('#routeStart').addEventListener('change', event => {
    routeStart = undefined;
  });
  $('#routeEnd').addEventListener('change', event => {
    routeEnd = undefined;
  });


  function showRoute() {
    hidePanels();
    hideCards();
    showSearchPane('route-ui');
    resizeMap();
  }

  function closeRoute() {
    document.body.classList.remove('route-ui');
    resizeMap();
    document.body.classList.remove('route-shown');
    clearRouteStart();
    clearRouteEnd();
    $('#routePath').innerHTML = '';
    jump_button_ids.forEach(n => {
      $('#routeForm').classList.remove(n);
    });
    map.SetRoute(null);
    lastRoute = null;
  }

  $('#routeStart').addEventListener('keydown', event => {
    if (event.ctrlKey || event.altKey || event.metaKey)
      return;
    if (event.key === 'Enter') {
      event.preventDefault();
      event.stopPropagation();
      $('#routeEnd').focus();
    }
  });

  $('#routeEnd').addEventListener('keydown', event => {
    if (event.ctrlKey || event.altKey || event.metaKey)
      return;
    if (event.key === 'Enter') {
      event.preventDefault();
      event.stopPropagation();

      $('#routePath').innerHTML = '';
      let found = false;
      jump_button_ids.forEach(n => {
        if ($('#routeForm').classList.contains(n)) {
          found = true;
          $('#'+n).click();
        }
      });
      if (!found)
        $('#J-2').click();
    }
  });

  $('#closeRouteBtn').addEventListener('click', event => {
    event.preventDefault();
    closeRoute();
  });

  $('#swapRouteBtn').addEventListener('click', event => {
    event.preventDefault();
    [$('#routeStart').value, $('#routeEnd').value] = [$('#routeEnd').value, $('#routeStart').value];
    [routeStart, routeEnd] = [routeEnd, routeStart];
    $('#routePath').innerHTML = '';
    jump_button_ids.forEach(n => {
      if ($('#routeForm').classList.contains(n))
        $('#'+n).click();
    });
  });

  $$('#routeForm button[name="jump"]').forEach(button => {
    button.addEventListener('click', event => {
      event.preventDefault();

      jump_button_ids.forEach(n => {
        $('#routeForm').classList.remove(n);
      });
      $('#routeForm').classList.add(button.id);

      const start = routeStart ? `${routeStart.sector} ${routeStart.hex}` : $('#routeStart').value;
      const end = routeEnd ? `${routeEnd.sector} ${routeEnd.hex}` : $('#routeEnd').value;
      const jump = button.dataset.parsecs;
      route(start, end, jump);
    });
  });

  $$('#routeForm input[type="checkbox"]').forEach(input => {
    input.addEventListener('click', event => {
      if ($('#routePath').innerHTML !== '')
        reroute();
    });
  });

  let ignoreNextKeyUp = false;
  document.body.addEventListener('keyup', event => {
    if (ignoreNextKeyUp) {
      ignoreNextKeyUp = false;
      return;
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();

      hidePanels();
      hideSearch();
      hideCards();
      selectedWorld = selectedSector = null;
      closeRoute();

      map.SetMain(null);
      map.SetRoute(null);

      mapElement.focus();
    }
  }, {capture: true});

  // Options Bar

  const PANELS = ['legend', 'more'];
  const TABS = ['lab', 'milieu', 'settings', 'share', 'help'];

  function showPanel(shown) {
    PANELS.forEach(p => {
      document.body.classList[p === shown ? 'add' : 'remove'](`show-${p}`);
    });
  }

  function togglePanel(shown) {
    PANELS.forEach(p => {
      document.body.classList[p === shown ? 'toggle' : 'remove'](`show-${p}`);
    });
  }

  function hidePanels() {
    PANELS.forEach(p => {
      document.body.classList.remove(`show-${p}`);
    });
  }

  PANELS.forEach(p => {
    $('#'+p+'Btn').addEventListener('click', () => {
      togglePanel(p);
    });
  });

  function showTab(shown) {
    TABS.forEach(p => {
      document.body.classList[shown === p ? 'add' : 'remove'](`show-${p}`);
    });
  }

  TABS.forEach(p => {
    $('#'+p+'Btn').addEventListener('click', () => {
      showTab(p);
    });
  });

  const STYLES = ['poster', 'atlas', 'print', 'candy', 'draft', 'fasa', 'terminal', 'mongoose'];
  STYLES.forEach(s => {
    $('#settingsBtn-'+s).addEventListener('click', () => { map.style = s; });
  });
  $$('.styles-pager').forEach(element => {
    element.addEventListener('click', () => { $('#styles').classList.toggle('p2'); });
  });


  $('#homeBtn').addEventListener('click', goHome);
  $('#homeBtn2').addEventListener('click', goHome);

  function goHome() {
    if (['sx', 'sy', 'hx', 'hy'].every(p => (`yah_${p}`) in urlParams)) {
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

  $$('#share-url,#share-code').forEach(input => {
    input.addEventListener('click', event => {
      event.preventDefault();
      input.focus();
      input.select();
      input.setSelectionRange(0, input.value.length); // .select() fails on iOS
    });
    input.addEventListener('mouseup', event => {
      event.preventDefault();
    });
  });

  // Nav Bar

  $('#zoomInBtn').addEventListener('click', () => map.ZoomIn());
  $('#zoomOutBtn').addEventListener('click', () => map.ZoomOut());
  $('#tiltBtn').addEventListener('click', () => { $('#cbTilt').click(); });
  $('#fsBtn').addEventListener('click', toggleFullscreen);

  // Bottom Panel

  $("#LogoImage").addEventListener('dblclick', () => {
    document.body.classList.add('hide-footer');
  });

  // Keyboard Shortcuts

  mapElement.addEventListener('keydown', event => {
    if (event.ctrlKey || event.altKey || event.metaKey)
      return;
    if (event.key === 'c') {
      event.preventDefault();
      event.stopPropagation();
      updateContext(map.worldX, map.worldY, {directAction: true});
      showMain(map.worldX, map.worldY);
      return;
    }
    if (event.key === 'h') {
      event.preventDefault();
      event.stopPropagation();
      goHome();
      return;
    }
    if (event.key === 't') {
      event.preventDefault();
      event.stopPropagation();
      $('#tiltBtn').click();
      return;
    }
    if (event.key === 'm') {
      event.preventDefault();
      event.stopPropagation();
      showPanel('legend');
      return;
    }
    if (event.key === 'f') {
      event.preventDefault();
      event.stopPropagation();
      toggleFullscreen();
      return;
    }
    if (event.key === '?') {
      event.preventDefault();
      event.stopPropagation();
      showPanel('more');
      showTab('help');
      return;
    }
    if (event.key === '/') {
      event.preventDefault();
      event.stopPropagation();
      $('#searchBox').focus();
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

  const optionObservers = [];

  bindCheckedToOption('#ShowSectorGrid', Traveller.MapOptions.GridMask);
  bindCheckedToOption('#ShowSectorNames', Traveller.MapOptions.SectorsMask);
  bindEnabled('#ShowSelectedSectorNames', o => o & Traveller.MapOptions.SectorsMask);
  bindChecked('#ShowSelectedSectorNames',
              o => o & Traveller.MapOptions.SectorsSelected,
              c => { setOptions(Traveller.MapOptions.SectorsMask, c ? Traveller.MapOptions.SectorsSelected : 0); });
  bindEnabled('#ShowAllSectorNames', o => o & Traveller.MapOptions.SectorsMask);
  bindChecked('#ShowAllSectorNames',
              o => o & Traveller.MapOptions.SectorsAll,
              c => { setOptions(Traveller.MapOptions.SectorsMask, c ? Traveller.MapOptions.SectorsAll : 0); });
  bindCheckedToOption('#ShowGovernmentBorders', Traveller.MapOptions.BordersMask);
  bindCheckedToNamedOption('#ShowRoutes', 'routes');
  bindCheckedToOption('#ShowGovernmentNames', Traveller.MapOptions.NamesMask);
  bindCheckedToOption('#ShowImportantWorlds', Traveller.MapOptions.WorldsMask);
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

  // Overlays that take "milieu" or a year
  [['#cbWave', 'ew'], ['#cbAS', 'as']].forEach(pair => {
    const [id, op] = pair;
    bindChecked(id,
                o => map.namedOptions.get(op),
                c => {
                  if (c) {
                    map.namedOptions.set(op, 'milieu');
                  } else {
                    map.namedOptions.delete(op);
                    delete urlParams[op];
                  }
                });
  });

  function bindControl(selector, property, onChange, event, onEvent) {
    const element = $(selector);
    optionObservers.push(o => { element[property] = onChange(o); });
    element.addEventListener(event, () => { onEvent(element); });
  }
  function bindChecked(selector, onChange, onEvent) {
    bindControl(selector, 'checked', onChange, 'click', element => { onEvent(element.checked); });
  }
  function bindEnabled(selector, onChange) {
    const element = $(selector);
    optionObservers.push(o => { element.disabled = !onChange(o); });
  }
  function bindCheckedToOption(selector, bitmask) {
    bindChecked(selector,
                o => (o & bitmask),
                c => { setOptions(bitmask, c ? bitmask : 0); });
  }
  function bindCheckedToNamedOption(selector, name) {
    bindChecked(selector,
                () => { const v = map.namedOptions.get(name);
                        return v === undefined ? defaults[name] : v; },
                c => {
                  if (!!c === !!defaults[name]) {
                    delete urlParams[name];
                    map.namedOptions.delete(name);
                  } else map.namedOptions.set(name, c ? 1 : 0); });
  }
  function bindRadioToNamedOption(selector, name) {
    optionObservers.push(o => {
      let v = map.namedOptions.get(name);
      if (v === undefined) v = defaults[name];
      const e = $(`${selector}[value="${v}"]`);
      if (e) e.checked = true;
    });
    $$(selector).forEach(elem => {
      elem.addEventListener('click', event => {
        if (elem.value === defaults[name]) {
          delete urlParams[name];
          map.namedOptions.delete(name);
        } else {
          map.namedOptions.set(name, elem.value);
        }
      });
    });
  }

  const EVENT_DEBOUNCE_MS = 10;

  map.OnOptionsChanged = Util.debounce(options => {
    optionObservers.forEach(o => { o(options); });
    $('#legendBox').classList.toggle('world_colors', options & Traveller.MapOptions.WorldColors);
    map.namedOptions.NAMES.forEach(name => {
      $('#legendBox').classList.toggle(`opt-${name}`, !!map.namedOptions.get(name));
    });
    updateContext(lastX || map.worldX, lastY || map.worldY, {refresh: true});
    updatePermalink();
    savePreferences();
  }, EVENT_DEBOUNCE_MS);

  map.OnStyleChanged = Util.debounce(style => {
    STYLES.forEach(s => {
      document.body.classList.toggle(`style-${s}`, s === style);
    });
    updateContext(lastX || map.worldX, lastY || map.worldY, {refresh: true});
    updatePermalink();
    updateScaleIndicator();
    savePreferences();
  }, EVENT_DEBOUNCE_MS);

  map.OnScaleChanged = Util.debounce(() => {
    updateContext(lastX || map.worldX, lastY || map.worldY);
    updatePermalink();
    updateScaleIndicator();
    savePreferences();
  }, EVENT_DEBOUNCE_MS);

  map.OnPositionChanged = Util.debounce(() => {
    updateContext(map.worldX, map.worldY);
    updatePermalink();
    savePreferences();
  }, EVENT_DEBOUNCE_MS);

  function post(message) {
    if (window.parent !== window) {
      window.parent.postMessage(message, '*');
    }
  }

  map.OnClick = data => {
    hidePanels();
    updateContext(data.x, data.y, {directAction: true, activeElement: data.activeElement});
    showMain(data.x, data.y);
    post({source: 'travellermap', type: 'click', location: {x: data.x, y: data.y}});
  };

  map.OnDoubleClick = world => {
    hidePanels();
    updateContext(world.x, world.y, {directAction: true});
    showMain(world.x, world.y);
    post({source: 'travellermap', type: 'doubleclick', location: world});
  };


  // TODO: Generalize URLParam<->Control and URLParam<->Style binding
  const PARAM_OPTIONS = [
    {param: 'galdir', selector: '#cbGalDir', className: 'show-directions', 'default': true},
    {param: 'tilt', selector: '#cbTilt', className: 'tilt', 'default': false,
     onchange: flag => { if (flag) map.EnableTilt(); }
    },
    {param: 'mains', selector: '#cbMains', className: 'show-mains', 'default': false,
     onchange: flag => { if (!flag) map.SetMain(null); }
    }
  ];
  PARAM_OPTIONS.forEach(option => {
    const elem = $(option.selector);
    elem.checked = option['default'];
    document.body.classList.toggle(option.className, option['default']);
    elem.addEventListener('click', () => {
      document.body.classList.toggle(option.className, elem.checked);
      updatePermalink();
      savePreferences();
      if (option.onchange)
        option.onchange(elem.checked);
    });
  });


  $('#btnResetPrefs').addEventListener('click', event => {
    event.preventDefault();

    map.namedOptions.NAMES.forEach(name => {
      delete urlParams[name];
      if (!(name in defaults))
        map.namedOptions.delete(name);
      else
        map.namedOptions.set(name, defaults[name]);
    });
    PARAM_OPTIONS.forEach(option => {
      $(option.selector).checked = option['default'];
      document.body.classList.toggle(option.className, option['default']);
      if (option.onchange) option.onchange(option.default);
    });
    map.options = defaults.options;
    updatePermalink();
    savePreferences();
  });

  (() => {
    if (isIframe) return;
    const preferences = JSON.parse(localStorage.getItem('preferences'));
    const location = JSON.parse(localStorage.getItem('location'));
    if (preferences) {
      if ('style' in preferences) map.style = preferences.style;
      if ('options' in preferences) map.options = preferences.options;
      map.namedOptions.NAMES.forEach(name => {
        if (name in preferences) {
          const value = preferences[name];
          if (value !== '')
            map.namedOptions.set(name, value);
        }
      });

      PARAM_OPTIONS.forEach(option => {
        if (option.param in preferences) {
          document.body.classList.toggle(option.className, preferences[option.param]);
          if (option.onchange)
            option.onchange(preferences[option.param]);
        }
      });
    }

    if (location) {
      if ('scale' in location) map.scale = location.scale;
      if ('position' in location) { map.x = location.position.x; map.y = location.position.y; }
    }
  })();

  // Overlay to allow quick return to default milieu.
  optionObservers.push(o => {
    const milieu = map.namedOptions.get('milieu') || defaults.milieu;
    $('#milieu-field').innerText = milieu;
    $('#milieu-field-default').innerText = defaults.milieu;
    document.body.classList.toggle(
      'milieu-not-default', milieu !== defaults.milieu);

    milieu_choices.forEach(m => {
      document.body.classList.toggle('milieu-' + m, m === milieu);
    });
  });
  $('#milieu-escape').addEventListener('click', event => {
    map.namedOptions.set('milieu', defaults.milieu);
  });

  //
  // Pull in options from URL - from permalinks
  //
  // Call this AFTER data binding is hooked up so UI is synchronized
  //
  const standalone = 'standalone' in window.navigator && window.navigator.standalone;
  const urlParams = standalone ? {} : map.ApplyURLParameters();

  // Force UI to synchronize in case URL parameters didn't do it
  map.OnOptionsChanged(map.options);

  if (isIframe) {
    const forceui = ('forceui' in urlParams) && Boolean(Number(urlParams.forceui));
    if (forceui)
      document.body.classList.remove('hide-ui');
  } else {
    const hideui = ('hideui' in urlParams) && Boolean(Number(urlParams.hideui));
    if (!hideui) {
      document.body.classList.remove('hide-ui');
      document.body.classList.remove('hide-footer');
    }
  }
  if (document.body.classList.contains('hide-ui')) {
    mapElement.title = '';
  }

  let dirty = false;
  PARAM_OPTIONS.forEach(option => {
    if (option.param in urlParams) {
      const show = Boolean(Number(urlParams[option.param]));
      document.body.classList.toggle(option.className, show);
      dirty = true;
    }
    $(option.selector).checked = document.body.classList.contains(option.className);
  });
  if (dirty) updatePermalink();

  //////////////////////////////////////////////////////////////////////
  //
  // Attract Mode
  //
  //////////////////////////////////////////////////////////////////////

  async function doAttract() {
    async function pickTarget() {
      const data = await Traveller.MapService.search('(random world)', {
        milieu: map.namedOptions.get('milieu')
      }, 'POST');
      const items = data.Results.Items;
      if (items.length < 1)
        throw new Error('random world search failed');
      const world = items[0].World;
      const tags = world.SectorTags.split(/\s+/);
      return tags.includes('OTU') ? world : await pickTarget();
    }

    const HOME_WAIT_MS = 5e3;
    const TARGET_WAIT_MS = 20e3;

    goHome();
    const world = await pickTarget();
    const target = Traveller.Astrometrics.sectorHexToMap(
      world.SectorX, world.SectorY, world.HexX, world.HexY);
    hideCards();
    setTimeout(async () => {
      await map.animateTo(128, target.x, target.y, 10);
      updateContext(map.worldX, map.worldY, {directAction: true});
      setTimeout(doAttract, TARGET_WAIT_MS);
    }, HOME_WAIT_MS);
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Metadata
  //
  //////////////////////////////////////////////////////////////////////

  let dataRequest = null;
  let dataTimeout = 0;
  let ignoreIndirect = true;
  let enableContext;

  function makeWikiURL(suffix) {
    return 'https://wiki.travellerrpg.com/' + encodeURIComponent(suffix.replace(/ /g, '_'));
  }

  function updateContext(worldX, worldY, options) {
    if (!enableContext || document.body.classList.contains('hide-ui'))
      return;

    options = Object.assign({}, options);

    if (ignoreIndirect && !options.directAction)
      return;
    ignoreIndirect = options.ignoreIndirect;

    const DATA_REQUEST_DELAY_MS = 100;
    const milieu = map.namedOptions.get('milieu');

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
      dataTimeout = setTimeout(async () => {
        dataRequest = Util.ignorable(Traveller.MapService.credits(worldX, worldY, {
          milieu
        }));
        const data = await dataRequest;
        dataRequest = null;
        displayResults(data);
      }, options.directAction || options.refresh ? 0 : DATA_REQUEST_DELAY_MS);
    }

    function displayResults(data) {
      if ('SectorTags' in data) {
        const tags =  String(data.SectorTags).split(/\s+/);
        data.Unofficial = true;
        ['Official', 'InReview', 'Unreviewed', 'Apocryphal', 'Preserve']
          .filter(tag => tags.includes(tag))
          .forEach(tag => {
            delete data.Unofficial;
            data[tag] = true;
          });
      } else {
        data.Unmapped = true;
      }

      data.Attribution = ['SectorAuthor', 'SectorSource', 'SectorPublisher']
        .filter(p => p in data)
        .map(p => data[p])
        .join(', ');

      if (data.SectorName) {
        data.SectorWikiURL = makeWikiURL(`${data.SectorName} Sector`);
        data.SectorWikiURLNoScheme = data.SectorWikiURL.replace(/^\w+:\/\//, '');
        data.BookletURL = Traveller.MapService.makeURL(
          `/data/${encodeURIComponent(data.SectorName)}/booklet`, {
            milieu
          });

        data.PosterURL = Traveller.MapService.makeURL('/api/poster', {
          sector: data.SectorName,
          accept: 'application/pdf',
          style: map.style,
          options: map.options,
          milieu
        });
        data.DataURL = Traveller.MapService.makeURL('/api/sec', {
          sector: data.SectorName, type: 'SecondSurvey',
          milieu
        });
        data.TSVDataURL = Traveller.MapService.makeURL('/api/sec', {
          sector: data.SectorName, type: 'TabDelimited',
          milieu
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
            setRouteStart(data.WorldName, data.WorldHex, data.SectorName);
            $('#routeEnd').focus();
          } else {
            $('#routeStart').focus();
          }
        } else if (options.activeElement === $('#routeEnd')) {
          if (data.WorldName) {
            setRouteEnd(data.WorldName, data.WorldHex, data.SectorName);
            $('#J-2').click();
          } else {
            $('#routeEnd').focus();
          }
        } else if ($('#routeStart').value === '' && data.WorldName) {
          setRouteStart(data.WorldName, data.WorldHex, data.SectorName);
          if (!isSmallScreen) $('#routeEnd').focus();
        } else if ($('#routeEnd').value === '' && data.WorldName) {
          setRouteEnd(data.WorldName, data.WorldHex, data.SectorName);
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

      if (options.directAction) {
        document.body.classList.toggle('sector-selected', selectedSector);
        document.body.classList.toggle('world-selected', selectedWorld);

        if (selectedWorld) {
          showWorldData();
        } else if (selectedSector) {
          showSectorData(data);
        } else {
          hideCards();
        }
      }
    }
  }

  async function showSectorData(data) {
    $('#sds-data').innerHTML = template('#sds-template')(data);

    // Hook up toggle
    $$('#sds-data .ds-mini-toggle, .sds-sectorname').forEach(element => {
      element.addEventListener('click', event => {
        document.body.classList.toggle('ds-mini');
      });
    });

    showSearchPane('sds-visible', data.SectorName);
  }

  async function showWorldData() {
    if (!selectedWorld)
      return;
    const context = {
      sector: selectedSector,
      hex: selectedWorld.hex
    };
    $('#spinner').style.display = 'block';
    const milieu = map.namedOptions.get('milieu');

    try {
      // World Data Sheet ("Info Card")

      const response = await fetch(Traveller.MapService.makeURL(
        '/api/jumpworlds?', {
        sector: context.sector, hex: context.hex,
        milieu,
        jump: 0
      }));
      if (!response.ok) throw new Error(response.statusText);

      const data = await response.json();
      const world = await Traveller.prepareWorld(data.Worlds[0]);
      if (world) {
        await Promise.all([
          Traveller.renderWorldImage(world, $('#wds-world-image')),
          world.map_exists // Once resolved, `map`/`map_thumb` are set
        ]);

        // Data Sheet
        world.DataSheetURL = Util.makeURL('print/world', {
          sector: context.sector,
          hex: context.hex,
          milieu,
          style: map.style,
          print: true
        });

        // Jump Maps
        world.JumpMapURL = Traveller.MapService.makeURL('/api/jumpmap', {
          sector: context.sector,
          hex: context.hex,
          milieu,
          style: map.style,
          options: map.options &
            (Traveller.MapOptions.BordersMask | Traveller.MapOptions.NamesMask |
             Traveller.MapOptions.WorldColors | Traveller.MapOptions.FilledBorders)
        });

        $('#wds-data').innerHTML = template('#wds-template')(world);

        // Hook up any generated "expandy" fields
        $$('.wds-expandy').forEach(elem => {
          elem.addEventListener('click', event => {
            const c = elem.getAttribute('data-wds-expand');
            $('#wds-frame').classList.toggle(c);
          });
        });

        // Hook up toggle
        $$('#wds-data .ds-mini-toggle, #wds-data .wds-names').forEach(element => {
            element.addEventListener('click', event => {
              document.body.classList.toggle('ds-mini');
            });
        });

        // Hook up buttons
        $('#ds-route-link').addEventListener('click', event => {
          event.preventDefault();
          setRouteStart(world.Name, world.Hex, world.Sector);
          if (!isSmallScreen) $('#routeEnd').focus();
          showRoute();
        });

        $('#wds-print-ds-link').addEventListener('click', event => {
          event.preventDefault();
          window.open(world.DataSheetURL);
        });

        if ($('#wds-map')) {
          $('#wds-map').addEventListener('click', event => {
            event.preventDefault();
            const url = event.target.getAttribute('data-map');
            if (isSmallScreen)
              window.open(url);
            else
              showLightboxImage(url);
          });
        }

        // Make it visible
        showSearchPane('wds-visible', world.Name);
      }
    } catch (error) {
      console.warn(error);
    } finally {
      $('#spinner').style.display = 'none';
    }
  }

  $('#wds-world-image').addEventListener('click', event => {
    document.body.classList.toggle('ds-mini');
  });

  $$('#sds-closebtn,#wds-closebtn,#ds-shade').forEach(element => {
    element.addEventListener('click', event => {
      hideCards();
      selectedWorld = selectedSector = null;
    });
  });

  $$('#legend-closebtn,#more-closebtn,#panel-shade').forEach(element => {
    element.addEventListener('click', event => {
      hidePanels();
    });
  });

  //////////////////////////////////////////////////////////////////////
  //
  // Search
  //
  //////////////////////////////////////////////////////////////////////

  let searchRequest = null;

  async function search(query, options) {
    options = Object.assign({}, options);

    closeRoute();
    hideCards();
    showSearchPane('search-results');

    selectedWorld = selectedSector = null;
    map.SetRoute(null);

    if (query === '')
      query = '(default)';

    if (query === lastQuery) {
      if (!searchRequest && options.onsubmit) {
        const links = $$('#resultsContainer a');
        if (links.length > 0) {
          links[0].click();
          return;
        }
      }
      map.SetRoute(lastQueryRoute);
      return;
    }

    if (searchRequest)
      searchRequest.ignore();

    if (options.results) {
      searchRequest = options.results;
    } else {
      searchRequest = Util.ignorable(Traveller.MapService.search(query, {
        milieu: map.namedOptions.get('milieu')
      }));
    }
    try {
      const data = await searchRequest;
      displayResults(data);
    } catch (ex) {
      $('#resultsContainer').innerHTML = '<i>Error fetching results.</i>';
    } finally {
      searchRequest = null;
      if (options.navigate) {
        const first = $('#resultsContainer a');
        if (first)
          first.click();
      }
    }

    // Transform the search results into clickable links
    function displayResults(data) {
      const base_url = document.location.href.replace(/\?.*/, '');

      function applyTags(item) {
        if ('SectorTags' in item) {
          const tags = String(item.SectorTags).split(/\s+/);
          item.Unofficial = true;
          ['Official', 'InReview', 'Unreviewed', 'Apocryphal', 'Preserve'].forEach(tag =>{
            if (tags.includes(tag)) {
              delete item.Unofficial;
              item[tag] = true;
            }
          });
        }
      }

      function pad2(n) {
        return ('00' + n).slice(-2);
      }

      const route = [];

      // Pre-process the data
      for (let i = 0; i < data.Results.Items.length; ++i) {

        const item = data.Results.Items[i];
        let sx, sy, hx, hy, scale;

        if (item.Subsector) {
          const subsector = item.Subsector,
                index = subsector.Index || 'A',
                n = (index.charCodeAt(0) - 'A'.charCodeAt(0));
          sx = subsector.SectorX|0;
          sy = subsector.SectorY|0;
          hx = (((n % 4) | 0) + 0.5) * (Traveller.Astrometrics.SectorWidth / 4);
          hy = (((n / 4) | 0) + 0.5) * (Traveller.Astrometrics.SectorHeight / 4);
          scale = subsector.Scale || 32;
          subsector.href = Util.makeURL(base_url, {scale, sx, sy, hx, hy});
          applyTags(subsector);
        } else if (item.Sector) {
          const sector = item.Sector;
          sx = sector.SectorX|0;
          sy = sector.SectorY|0;
          hx = (Traveller.Astrometrics.SectorWidth / 2);
          hy = (Traveller.Astrometrics.SectorHeight / 2);
          scale = sector.Scale || 8;
          sector.href = Util.makeURL(base_url, {
            scale, sx, sy, hx, hy,
            sector: sector.Name
          });
          applyTags(sector);
        } else if (item.World) {
          const world = item.World;
          world.Name = world.Name || '(Unnamed)';
          sx = world.SectorX|0;
          sy = world.SectorY|0;
          hx = world.HexX|0;
          hy = world.HexY|0;
          world.Hex = pad2(hx) + pad2(hy);
          scale = world.Scale || 64;
          let params = {scale, sx, sy, hx, hy};
          if (!data.Tour) {
            params = Object.assign(params,
                                   {sector: world.Sector, world: world.Name, hex: world.Hex});
          }
          if (data.Route) {
            route.push({sx:sx, sy:sy, hx:hx, hy:hy});
          }
          world.href = Util.makeURL(base_url, params);
          applyTags(world);
        } else if (item.Label) {
          const label = item.Label;
          sx = label.SectorX | 0;
          sy = label.SectorY | 0;
          hx = label.HexX | 0;
          hy = label.HexY | 0;
          scale = label.Scale || 64;
          label.href = Util.makeURL(base_url, { scale, sx, sy, hx, hy });
          applyTags(label);
        }
      }

      $('#resultsContainer').innerHTML = template('#SearchResultsTemplate')(data);

      $$('#resultsContainer a').forEach(a => {
        a.addEventListener('click', event => {
          event.preventDefault();
          selectedWorld = null;

          const params = Util.parseURLQuery(event.target);
          map.CenterAtSectorHex(params.sx|0, params.sy|0, params.hx|0, params.hy|0, {scale: params.scale|0});
          if (isSmallScreen)
            document.body.classList.remove('search-results');

          const coords = Traveller.Astrometrics.sectorHexToWorld(
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

      const first = $('#resultsContainer a');
      if (first && !options.typed && !options.onfocus)
        setTimeout(() => { first.focus(); }, 0);

      if (route.length) {
        map.SetRoute(route);
      }

      lastQuery = query;
      lastQueryRoute = route.length ? route : null;
    }
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Route Search
  //
  //////////////////////////////////////////////////////////////////////

  function reroute() {
    if (lastRoute) route(lastRoute.start, lastRoute.end, lastRoute.jump);
  }

  async function route(start, end, jump) {
    $('#routePath').innerHTML = '';
    lastRoute = {start, end, jump};

    const options = {
      start, end, jump,
      x: map.worldX, y: map.worldY,
      milieu: map.namedOptions.get('milieu'),
      wild: $('#route-wild').checked?1:0,
      im: $('#route-im').checked?1:0,
      nored: $('#route-nored').checked?1:0,
      aok: $('#route-aok').checked?1:0
    };

    try {
      const response = await fetch(Traveller.MapService.makeURL('/api/route', options));
      if (!response.ok) throw new Error(response.statusText);

      const data = await response.json();
      if (typeof data === 'string') throw new Error(data);

      const base_url = document.location.href.replace(/\?.*/, '');
      const route = [];
      let total = 0;
      data.forEach((world, index) => {
        world.Name = world.Name || '(Unnamed)';
        const sx = world.SectorX|0;
        const sy = world.SectorY|0;
        const hx = world.HexX|0;
        const hy = world.HexY|0;
        const scale = 64;
        world.href = Util.makeURL(base_url, {scale: 64, sx, sy, hx, hy});

        if (index > 0) {
          const prev = data[index - 1];
          const a = Traveller.Astrometrics.sectorHexToWorld(
            prev.SectorX|0, prev.SectorY|0, prev.HexX|0, prev.HexY|0);
          const b = Traveller.Astrometrics.sectorHexToWorld(sx, sy, hx, hy);
          const dist = Traveller.Astrometrics.hexDistance(a.x, a.y, b.x, b.y);
          prev.Distance = dist;
          total += dist;
        }

        route.push({sx:sx, sy:sy, hx:hx, hy:hy});
      });

      map.SetRoute(route);
      const routeData = {
        Route: data,
        Distance: total,
        Jumps: data.length - 1,
        PrintURL: Util.makeURL('./print/route', options)
      };
      $('#routePath').innerHTML = template('#RouteResultsTemplate')(routeData);
      document.body.classList.add('route-shown');
      resizeMap();

      $$('#routePath .item a').forEach(a => {
        a.addEventListener('click', event => {
          event.preventDefault();
          selectedWorld = null;

          const params = Util.parseURLQuery(event.target);
          map.CenterAtSectorHex(params.sx|0, params.sy|0, params.hx|0, params.hy|0, {scale: params.scale|0});
        });
        });

      $('#copy-route').addEventListener('click', event => {
        event.preventDefault();
        Util.copyTextToClipboard(template('#RouteResultsTextTemplate')({
          Route: data,
          Distance: total,
          Jumps: data.length - 1
        }));
      });

      $('#print-route').addEventListener('click', event => {
        event.preventDefault();
        window.open(routeData.PrintURL);
      });

    } catch (reason) {
      $('#routePath').innerHTML = template('#RouteErrorTemplate')({Message: reason.message});
      map.SetRoute(null);
      document.body.classList.add('route-shown');
      resizeMap();
    }
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Scale Indicator
  //
  //////////////////////////////////////////////////////////////////////

  const isCanvasSupported = ('getContext' in $('#scaleIndicator'));
  let animId = 0;
  function updateScaleIndicator() {
    if (!isCanvasSupported) return;

    cancelAnimationFrame(animId);
    animId = requestAnimationFrame(() => {
      const scale = map.scale,
            canvas = $('#scaleIndicator'),
            ctx = canvas.getContext('2d'),
            w = parseFloat(canvas.width),
            h = parseFloat(canvas.height),
            style = map.style,
            color = ['atlas', 'print', 'draft', 'fasa', 'mongoose'].includes(style) ? 'black' : 'white';

      ctx.clearRect(0, 0, w, h);

      let dist = w / scale;
      const factor = Math.pow(10, Math.floor(Math.log(dist) / Math.LN10));
      dist = Math.floor(dist / factor) * factor;
      dist = parseFloat(dist.toPrecision(1));
      const label = `${dist} pc`;
      const bar = dist * scale;

      ctx.save();
      ctx.lineCap = 'square';
      ctx.lineWidth = 2;
      ctx.strokeStyle = color;
      ctx.beginPath();
      ctx.moveTo(Math.round(w - bar + 1), Math.round(h / 2));
      ctx.lineTo(Math.round(w - bar + 1), Math.round(h * 3 / 4));
      ctx.lineTo(Math.round(w - 1), Math.round(h * 3 / 4));
      ctx.lineTo(Math.round(w - 1), Math.round(h / 2));
      ctx.stroke();
      ctx.restore();

      ctx.fillStyle = color;
      ctx.font = '12px Univers, Arial, sans-serif';
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

  let worldToMainMap;
  async function showMain(worldX, worldY) {
    if (!$('#cbMains').checked) return;
    const main = await findMain(worldX, worldY);
    map.SetMain(main);
  }
  async function findMain(worldX, worldY) {
    function sectorHexToSig(sx, sy, hx, hy) {
      return `${sx}/${sy}/` + ('0000' + ( hx * 100 + hy )).slice(-4);
    }
    function sigToSectorHex(sig) {
      const parts = sig.split('/');
      return {sx: parts[0]|0, sy: parts[1]|0, hx: (parts[2] / 100) | 0, hy: parts[2] % 100};
    }

    async function getMainsMapping() {
      if (worldToMainMap)
        return worldToMainMap;
      const r = await fetch(Traveller.MapService.makeURL('/res/mains.json'));
      if (!r.ok) throw new Error(r.statusText);

      worldToMainMap = new Map();
      const mains = await r.json();
      mains.forEach(main => {
        main.forEach((sig, index) => {
          worldToMainMap.set(sig, main);
          main[index] = sigToSectorHex(sig);
        });
      });
      return worldToMainMap;
    }

    const map = await getMainsMapping();
    const sectorHex = Traveller.Astrometrics.worldToSectorHex(worldX, worldY);
    const sig = sectorHexToSig(sectorHex.sx, sectorHex.sy, sectorHex.hx, sectorHex.hy);
    return map.get(sig);
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Utilities
  //
  //////////////////////////////////////////////////////////////////////

  function showLightboxImage(url) {
    const lightbox = document.body.appendChild(document.createElement('div'));
    const inner = lightbox.appendChild(document.createElement('div'));
    lightbox.className = 'lightbox';
    lightbox.tabIndex = 0;
    inner.style.backgroundImage = `url("${url}")`;

    lightbox.addEventListener('click', event => {
      event.preventDefault();
      event.stopPropagation();
      lightbox.remove();
    });
    lightbox.addEventListener('keydown', event => {
      event.preventDefault();
      event.stopPropagation();
      ignoreNextKeyUp = true;
      lightbox.remove();
    });

    lightbox.focus();
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Final setup
  //
  //////////////////////////////////////////////////////////////////////

  if (!isIframe)
    mapElement.focus();

  $('#searchBox').disabled = false;

  // iOS WebKit workarounds
  if (navigator.userAgent.match(/iPad|iPhone/)) (() => {
    // Prevent inadvertant touch-scroll.
    $$('button, input').forEach(element => {
      element.addEventListener('touchmove', event => { event.preventDefault();  }, {passive:false});
    });
    // Prevent inadvertant touch-zoom.
    document.addEventListener('touchmove', event => {
      if (event.scale !== 1) { event.preventDefault(); }
    }, false);
    // Prevent double-tap-to-zoom.
    let last_time = Date.now();
    document.addEventListener('touchend', event => {
      const now = Date.now();
      if (now - last_time <= 500) {
        event.preventDefault();
        event.target.click();
      }
      last_time = now;
    }, false);
  })();

  // Show cookie accept prompt, if necessary.
  if (!isIframe) {
    setTimeout(() => {
      const cookies = Util.parseCookies();
      const cookies_key = 'tm_accept';
      if (!(cookies.tm_accept || localStorage.getItem(cookies_key))) {
        document.body.classList.add('cookies-not-accepted');
        $('#cookies button').addEventListener('click', event => {
          document.body.classList.remove('cookies-not-accepted');
          document.cookie = 'tm_accept=1;SameSite=Strict;Secure';
          localStorage.setItem(cookies_key, 1);
        });
      }
    }, 1000);
  }

  // Show promo, if not dismissed.
  if (!isIframe && $('#promo-closebtn')) {
    setTimeout(() => {
      const promo_key = 'tm_promo5';
      if (!localStorage.getItem(promo_key)) {
        document.body.classList.add('show-promo');
        $('#promo-closebtn').addEventListener('click', event => {
          document.body.classList.remove('show-promo');
          localStorage.setItem(promo_key, 1);
        });
        $$('#promo-hover a').forEach(a => a.addEventListener('click', event => {
          document.body.classList.remove('show-promo');
          localStorage.setItem(promo_key, 1);
        }));
      }
    }, 1000);
  }

  if ('serviceWorker' in navigator && location.protocol === 'https:') {
    navigator.serviceWorker.register('sw.js');
  }

  // Process URL params that drive the map

  ['q', 'qn'].forEach(key => {
    if (key in urlParams) {
      $('#searchBox').value = urlParams[key];
      search(urlParams[key], {navigate: key === 'qn'});
    }
  });

  if ('qr' in urlParams) {
    try {
      const results = JSON.parse(urlParams['qr']);
      const term = urlParams['search'] || '';
      $('#searchBox').value = term;
      search(term, {navigate: true, results});
    } catch (ex) {
      console.warn('Error parsing "qr" data: ', ex);
    }
  }

  if ('attract' in urlParams) {
    // TODO: Disable UI, or make any UI interaction cancel
    doAttract();
  }

  // After all async events from the map have fired...
  setTimeout(() => { enableContext = true; }, 0);

})(self);
