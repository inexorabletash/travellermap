// ======================================================================
// Exported Functionality
// ======================================================================

// NOTE: Used by other scripts
var Util = {
  makeURL: function(base, params) {
    'use strict';
    base = String(base).replace(/\?.*/, '');
    if (!params) return base;
    var keys = Object.keys(params), args = '';
    for (var i = 0; i < keys.length; ++i) {
      var key = keys[i], value = params[key];
      if (value === undefined || value === null) continue;
      args += (args ? '&' : '') + encodeURIComponent(key) + '=' + encodeURIComponent(value);
    }
    return args ? base + '?' + args : base;
  },

  // Replace with URL/searchParams
  parseURLQuery: function(url) {
    'use strict';
    var o = Object.create(null);
    if (url.search && url.search.length > 1) {
      url.search.substring(1).split('&').forEach(function(pair) {
        if (!pair) return;
        var kv = pair.split('=', 2);
        if (kv.length === 2)
          o[kv[0]] = decodeURIComponent(kv[1].replace(/\+/g, ' '));
        else
          o[kv[0]] = true;
      });
    }
    return o;
  },

  escapeHTML: function(s) {
    'use strict';
    return String(s).replace(/[&<>"']/g, function(c) {
      switch (c) {
      case '&': return '&amp;';
      case '<': return '&lt;';
      case '>': return '&gt;';
      case '"': return '&quot;';
      case "'": return '&#39;';
      default: return c;
      }
    });
  },

  once: function(func) {
    var run = false;
    return function() {
      if (run) return;
      run = true;
      func.apply(this, arguments);
    };
  },

  debounce: function(func, delay, immediate) {
    var timeoutId = 0;
    if (immediate) {
      return function() {
        if (timeoutId)
          clearTimeout(timeoutId);
        else
          func.apply(this, arguments);
        timeoutId = setTimeout(function() { timeoutId = 0; }, delay);
      };
    } else {
      return function() {
        var $this = this, $arguments = arguments;
        if (timeoutId)
          clearTimeout(timeoutId);
        timeoutId = setTimeout(function() {
          func.apply($this, $arguments);
          timeoutId = 0;
        }, delay);
      };
    }
  },

  memoize: function(f) {
    var cache = Object.create(null);
    return function() {
      var key = JSON.stringify([].slice.call(arguments));
      return (key in cache) ? cache[key] : cache[key] = f.apply(this, arguments);
    };
  },

  // p = ignorable(other_promise);
  // p.then(...);
  // p.ignore(); // p will neither resolve nor reject
  // WARNING: p = ignorable(...).then(...); p.ignore(); will fail
  // (Promise subclassing is not used)
  ignorable: function(p) {
    var ignored = false;
    var q = new Promise(function(resolve, reject) {
      p.then(function(r) { if (!ignored) resolve(r); },
             function(r) { if (!ignored) reject(r); });
    });
    q.ignore = function() { ignored = true; };
    return q;
  },

  fetchImage: function(url, img) {
    return new Promise(function(resolve, reject) {
      img = img || document.createElement('img');
      img.src = url;
      img.onload = function() { resolve(img); };
      img.onerror = function(e) { reject(Error('Image failed to load')); };
    });
  }
};


(function(global) {
  'use strict';

  //----------------------------------------------------------------------
  // General Traveller stuff
  //----------------------------------------------------------------------

  var SERVICE_BASE = (function(l) {
    'use strict';
    if (l.hostname === 'localhost' && l.pathname.indexOf('~') !== -1)
      return 'https://travellermap.com';
    return '';
  }(window.location));

  var LEGACY_STYLES = true;

  function fromHex(c) {
    return '0123456789ABCDEFGHJKLMNPQRSTUVW'.indexOf(c.toUpperCase());
  }

  //----------------------------------------------------------------------
  // Enumerated types
  //----------------------------------------------------------------------

  var MapOptions = {
    SectorGrid: 0x0001,
    SubsectorGrid: 0x0002,
    GridMask: 0x0003,
    SectorsSelected: 0x0004,
    SectorsAll: 0x0008,
    SectorsMask: 0x000c,
    BordersMajor: 0x0010,
    BordersMinor: 0x0020,
    BordersMask: 0x0030,
    NamesMajor: 0x0040,
    NamesMinor: 0x0080,
    NamesMask: 0x00c0,
    WorldsCapitals: 0x0100,
    WorldsHomeworlds: 0x0200,
    WorldsMask: 0x0300,
    RoutesSelectedDeprecated: 0x0400,
    PrintStyleDeprecated: 0x0800,
    CandyStyleDeprecated: 0x1000,
    StyleMaskDeprecated: 0x1800,
    ForceHexes: 0x2000,
    WorldColors: 0x4000,
    FilledBorders: 0x8000,
    Mask: 0xffff
  };

  var Styles = {
    Poster: 'poster',
    Atlas: 'atlas',
    Print: 'print',
    Candy: 'candy',
    Draft: 'draft',
    FASA: 'fasa'
  };

  //----------------------------------------------------------------------
  // Astrometric Constants
  //----------------------------------------------------------------------

  var Astrometrics = {
    ParsecScaleX: Math.cos(Math.PI / 6), // cos(30)
    ParsecScaleY: 1.0,
    SectorWidth: 32,
    SectorHeight: 40,
    ReferenceHexX: 1, // Reference is at Core 0140
    ReferenceHexY: 40,
    TileWidth: 256,
    TileHeight: 256,
    MinScale: 0.0078125,
    MaxScale: 512,

    // World-space: Hex coordinate, centered on Reference
    sectorHexToWorld: function(sx, sy, hx, hy) {
      return {
        x: (sx * Astrometrics.SectorWidth) + hx - Astrometrics.ReferenceHexX,
        y: (sy * Astrometrics.SectorHeight) + hy - Astrometrics.ReferenceHexY
      };
    },

    worldToSectorHex: function(x, y) {
      x += Astrometrics.ReferenceHexX - 1;
      y += Astrometrics.ReferenceHexY - 1;

      var sx = Math.floor(x / Astrometrics.SectorWidth);
      var sy = Math.floor(y / Astrometrics.SectorHeight);
      var hx = (x - (sx * Astrometrics.SectorWidth) + 1);
      var hy = (y - (sy * Astrometrics.SectorHeight) + 1);

      return {sx:sx, sy:sy, hx:hx, hy:hy};
    },

    // Map-space: Cartesian coordinates, centered on Reference
    sectorHexToMap: function(sx, sy, hx, hy) {
      var world = Astrometrics.sectorHexToWorld(sx, sy, hx, hy);
      return Astrometrics.worldToMap(world.x, world.y);
    },

    worldToMap: function(wx, wy) {
      var x = wx;
      var y = wy;

      // Offset from the "corner" of the hex
      x -= 0.5;
      y -= ((wx % 2) !== 0) ? 0 : 0.5;

      // Scale to non-homogenous coordinates
      x *= Astrometrics.ParsecScaleX;
      y *= -Astrometrics.ParsecScaleY;

      // Drop precision (avoid animations, etc)
      x = Math.round(x * 1000) / 1000;
      y = Math.round(y * 1000) / 1000;

      return {x: x, y: y};
    },

    mapToWorld: function(x, y) {
      var wx = Math.round((x / Astrometrics.ParsecScaleX) + 0.5);
      var wy = Math.round((-y / Astrometrics.ParsecScaleY) + ((wx % 2 === 0) ? 0.5 : 0));
      return {x: wx, y: wy};
    },

    // World-space Coordinates (Reference is 0,0)
    hexDistance: function(ax, ay, bx, by) {
      function even(x) { return (x % 2) == 0; }
      function odd (x) { return (x % 2) != 0; }
      var dx = bx - ax;
      var dy = by - ay;
      var adx = Math.abs(dx);
      var ody = dy + Math.floor(adx / 2);
      if (even(ax) && odd(bx))
        ody += 1;
      return Math.max(adx - ody, ody, adx);
    }
  };

  var Defaults = {
    options:
    MapOptions.SectorGrid | MapOptions.SubsectorGrid |
      MapOptions.SectorsSelected |
      MapOptions.BordersMajor | MapOptions.BordersMinor |
      MapOptions.NamesMajor |
      MapOptions.WorldsCapitals | MapOptions.WorldsHomeworlds,
    scale: 2,
    style: Styles.Poster
  };

  var styleLookup = (function() {
    var sheets = {};
    var base = {
      overlay_color: '#8080ff',
      route_color: '#048104',
      main_s_color: 'pink',
      main_m_color: '#FFCC00',
      main_l_color: 'cyan',
      main_opacity: 0.25,
      ew_color: '#FFCC00',
      you_are_here_url: 'res/ui/youarehere.png'
    };
    sheets[Styles.Poster] = base;
    sheets[Styles.Candy] = base;
    sheets[Styles.Draft] = base;
    sheets[Styles.Atlas] = Object.assign({}, base, {
      overlay_color: '#808080',
      you_are_here_url: 'res/ui/youarehere_gray.png'
    });
    sheets[Styles.FASA] = sheets[Styles.Print] =
      Object.assign({}, base, {
        you_are_here_url: 'res/ui/youarehere_gray.png'
      });

    return function(style, property) {
      var sheet = sheets[style] || sheets[Defaults.style];
      return sheet[property];
    };
  }());


  // ======================================================================
  // Data Services
  // ======================================================================

  var MapService = (function() {
    function service(url, contentType, method) {
      return fetch(url, {method: method || 'GET',
                         headers: {Accept: contentType}})
        .then(function(response) {
          if (!response.ok)
            throw Error(response.statusText);
          return (contentType === 'application/json') ?
            response.json() : response.text();
        });
    }

    function url(path, options) {
      return Util.makeURL(SERVICE_BASE + path, options);
    }

    return {
      makeURL: function(path, options) {
        return url(path, options);
      },

      coordinates: function(sector, hex, options) {
        options = Object.assign({}, options, {sector: sector, hex: hex});
        return service(url('/api/coordinates', options),
                       options.accept || 'application/json');
      },

      credits: function(worldX, worldY, options) {
        options = Object.assign({}, options, {x: worldX, y: worldY});
        return service(url('/api/credits', options),
                       options.accept || 'application/json');
      },

      search: function(query, options, method) {
        options = Object.assign({}, options, {q: query});
        return service(url('/api/search', options),
                       options.accept || 'application/json', method);
      },

      sectorData: function(sector, options) {
        options = Object.assign({}, options, {sector: sector});
        return service(url('/api/sec', options),
                       options.accept || 'text/plain');
      },

      sectorDataTabDelimited: function(sector, options) {
        options = Object.assign({}, options, {sector: sector, type: 'TabDelimited'});
        return service(url('/api/sec', options),
                       options.accept || 'text/plain');
      },

      sectorMetaData: function(sector, options) {
        options = Object.assign({}, options, {sector: sector});
        return service(url('/api/metadata', options),
                       options.accept || 'application/json');
      },

      MSEC: function(sector, options) {
        options = Object.assign({}, options, {sector: sector});
        return service(url('/api/msec', options),
                       options.accept || 'text/plain');
      },

      universe: function(options) {
        options = Object.assign({}, options);
        return service(url('/api/universe', options),
                       options.accept || 'application/json');
      }
    };
  }());

  // ======================================================================
  // Least-Recently-Used Cache
  // ======================================================================

  function LRUCache(capacity) {
    this.capacity = capacity;
    this.map = {};
    this.queue = [];
  }
  LRUCache.prototype = {
    ensureCapacity: function(capacity) {
      if (this.capacity < capacity)
        this.capacity = capacity;
    },

    clear: function() {
      this.map = {};
      this.queue = [];
    },

    fetch: function(key) {
      key = '$' + key;
      var value = this.map[key];
      if (value === undefined)
        return undefined;

      var index = this.queue.indexOf(key);
      if (index !== -1)
        this.queue.splice(index, 1);
      this.queue.push(key);
      return value;
    },

    insert: function(key, value) {
      key = '$' + key;
      // Remove previous instances
      var index = this.queue.indexOf(key);
      if (index !== -1)
        this.queue.splice(index, 1);

      this.map[key] = value;
      this.queue.push(key);

      while (this.queue.length > this.capacity) {
        key = this.queue.shift();
        delete this.map[key];
      }
    }
  };

  // ======================================================================
  // Image Stash
  // ======================================================================

  function ImageStash() {
    this.map = new Map();
  }
  ImageStash.prototype = {
    get: function(url, callback) {
      if (this.map.has(url))
        return this.map.get(url);

      this.map.set(url, undefined);
      Util.fetchImage(url).then(function(img) {
        this.map.set(url, img);
        callback(img);
      }.bind(this));

      return undefined;
    }
  };
  var stash = new ImageStash();


  // ======================================================================
  // Animation Utilities
  // ======================================================================

  var Animation = (function() {
    function isCallable(o) {
      return typeof o === 'function';
    }

    //
    // dur = total duration (seconds)
    // smooth = optional smoothing function
    // set onanimate to function called with animation position (0.0 ... 1.0)
    //
    function Animation(dur, smooth) {
      var start = Date.now();

      this.onanimate = null;
      this.oncancel = null;
      this.oncomplete = null;

      var tickFunc = function() {
        var f = (Date.now() - start) / 1000 / dur;
        if (f < 1.0)
          this.timerid = requestAnimationFrame(tickFunc);

        var p = f;
        if (isCallable(smooth))
          p = smooth(p);

        if (isCallable(this.onanimate))
          this.onanimate(p);

        if (f >= 1.0 && isCallable(this.oncomplete))
          this.oncomplete();
      }.bind(this);

      this.timerid = requestAnimationFrame(tickFunc);
   }

    Animation.prototype = {
      cancel: function() {
        if (this.timerid) {
          cancelAnimationFrame(this.timerid);
          if (isCallable(this.oncancel))
            this.oncancel();
        }
      }
    };

    Animation.interpolate = function(a, b, p) {
      return a * (1.0 - p) + b * p;
    };

    // Time smoothing function - input time is t within duration dur.
    // Acceleration period is a, deceleration period is d.
    //
    // Example:     t_filtered = smooth( t, 1.0, 0.25, 0.25 );
    //
    // Reference:   http://www.w3.org/TR/2005/REC-SMIL2-20050107/smil-timemanip.html
    Animation.smooth = function(t, dur, a, d) {
      var dacc = dur * a;
      var ddec = dur * d;
      var r = 1 / (1 - a / 2 - d / 2);
      var r_t, tdec, pd;

      if (t < dacc) {
        r_t = r * (t / dacc);
        return t * r_t / 2;
      } else if (t <= (dur - ddec)) {
        return r * (t - dacc / 2);
      } else {
        tdec = t - (dur - ddec);
        pd = tdec / ddec;

        return r * (dur - dacc / 2 - ddec + tdec * (2 - pd) / 2);
      }
    };

    return Animation;
  }());


  // ======================================================================
  // Observable name/value map
  // ======================================================================

  function NamedOptions(notify) {
    this._options = {};
    this._notify = notify;
  }
  NamedOptions.prototype = {
    keys: function() { return Object.keys(this._options); },
    get: function(key) { return this._options[key]; },
    set: function(key, value) { this._options[key] = value; this._notify(key); },
    delete: function(key) { delete this._options[key]; this._notify(key); },
    forEach: function(fn, thisArg) {
      var keys = Object.keys(this._options);
      for (var i = 0; i < keys.length; ++i) {
        var k = keys[i];
        fn.call(thisArg, this._options[k], k, i);
      }
    }
  };

  //----------------------------------------------------------------------
  //
  // Usage:
  //
  //   var map = new Map( document.getElementById('YourMapDiv') );
  //
  //   map.OnPositionChanged = function() { update permalink }
  //   map.OnScaleChanged    = function() { update scale indicator }
  //   map.OnStyleChanged    = function() { update control panel }
  //   map.OnOptionsChanged  = function() { update control panel }
  //
  //   map.OnHover           = function( {x, y} ) { show data }
  //   map.OnClick           = function( {x, y} ) { show data }
  //   map.OnDoubleClick     = function( {x, y} ) { show data }
  //
  //   Read-Only:
  //     map.worldX
  //     map.worldY
  //
  //   Read/Write:
  //     map.x
  //     map.y
  //     map.position ~= [map.x, map.y]
  //     map.scale
  //     map.style
  //     map.options
  //
  //   map.namedOptions
  //      .keys()
  //      .get(k)
  //      .set(k, v)
  //      .delete(k)
  //      .forEach(function(value, key, index) { ... });
  //
  //   map.CenterAtSectorHex( sx, sy, hx, hy, {scale, immediate} );
  //   map.Scroll( dx, dy, fAnimate );
  //   map.ZoomIn();
  //   map.ZoomOut();
  //
  //   map.ApplyURLParameters()
  //
  //   map.SetRoute()
  //   map.AddMarker(id, x, y, opt_url); // should have CSS style for .marker#<id>
  //   map.AddOverlay({type:'rectangle', x, y, w, h}); // should have CSS style for .overlay
  //   map.AddOverlay({type:'circle', x, y, r}); // should have CSS style for .overlay
  //
  //----------------------------------------------------------------------

  function fireEvent(target, event, data) {
    if (typeof target['On' + event] !== 'function') return;
    setTimeout(function() { target['On' + event](data); }, 0);
  }

  // ======================================================================
  // Slippy Map using Tiles
  // ======================================================================

  function log2(v) { return Math.log(v) / Math.LN2; }
  function pow2(v) { return Math.pow(2, v); }
  function dist(x, y) { return Math.sqrt(x*x + y*y); }

  var SINK_OFFSET = 1000;

  var INT_OPTIONS = [
    'routes', 'rifts', 'dimunofficial',
    'sscoords', 'allhexes',
    'dw', 'an', 'mh', 'po', 'im', 'cp', 'stellar'
  ];
  var STRING_OPTIONS = [
    'ew', 'qz', 'hw', 'milieu'
  ];

  function TravellerMap(container, boundingElement) {
    this.container = container;
    this.rect = boundingElement.getBoundingClientRect();

    this.min_scale = -5;
    this.max_scale = 10;

    // Exposed via getters/setters
    this._options = Defaults.options;
    this._style = Defaults.style;

    this._logScale = 1;
    this._tx = 0;
    this._ty = 0;

    this.tilesize = 256;

    this.cache = new LRUCache(64);

    this.namedOptions = new NamedOptions(Util.debounce(function(key) {
      this.invalidate();
      fireEvent(this, 'OptionsChanged', this.options);
    }.bind(this), 1));
    this.namedOptions.NAMES = INT_OPTIONS.concat(STRING_OPTIONS);

    this.loading = new Set();

    this.defer_loading = true;

    var CLICK_SCALE_DELTA = -0.5;
    var SCROLL_SCALE_DELTA = -0.15;
    var KEY_SCROLL_DELTA = 25;

    container.style.position = 'relative';

    // Event target, so it doesn't change during refreshes
    var sink = document.createElement('div');
    sink.style.position = 'absolute';
    sink.style.left = sink.style.top = sink.style.right = sink.style.bottom = (-SINK_OFFSET) + 'px';
    sink.style.zIndex = 1000;
    container.appendChild(sink);

    this.canvas = document.createElement('canvas');
    this.canvas.style.position = 'absolute';
    this.canvas.style.zIndex = 0;
    container.appendChild(this.canvas);

    this.ctx = this.canvas.getContext('2d');

    this.markers = [];
    this.overlays = [];
    this.route = null;
    this.main = null;

    // ======================================================================
    // Event Handlers
    // ======================================================================

    var dragging, drag_coords, was_dragged, previous_focus;
    container.addEventListener('mousedown', function(e) {
      this.cancelAnimation();
      previous_focus = document.activeElement;
      container.focus();
      dragging = true;
      was_dragged = false;
      drag_coords = this.eventCoords(e);
      container.classList.add('dragging');

      e.preventDefault();
      e.stopPropagation();
    }.bind(this), true);

    var hover_coords;
    container.addEventListener('mousemove', function(e) {
      var coords = this.eventCoords(e);

      // Ignore mousemove immediately following mousedown with same coords.
      if (dragging && coords.x === drag_coords.x && coords.y === drag_coords.y)
        return;

      if (dragging) {
        was_dragged = true;

        this._offset(drag_coords.x - coords.x, drag_coords.y - coords.y);
        drag_coords = coords;
        e.preventDefault();
        e.stopPropagation();
      }

      var wc = this.eventToWorldCoords(e);

      // Throttle the events
      if (hover_coords && hover_coords.x === wc.x && hover_coords.y === wc.y)
        return;

      hover_coords = wc;
      fireEvent(this, 'Hover', hover_coords);
    }.bind(this), true);

    document.addEventListener('mouseup', function(e) {
      if (dragging) {
        dragging = false;
        container.classList.remove('dragging');
        e.preventDefault();
        e.stopPropagation();
      }
    });

    container.addEventListener('click', function(e) {
      e.preventDefault();
      e.stopPropagation();

      if (!was_dragged) {
        fireEvent(this, 'Click',
                  Object.assign({}, this.eventToWorldCoords(e), {activeElement: previous_focus}));
      }
    }.bind(this));

    container.addEventListener('dblclick', function(e) {
      e.preventDefault();
      e.stopPropagation();

      this.cancelAnimation();

      var MAX_DOUBLECLICK_SCALE = 9;
      if (this._logScale < MAX_DOUBLECLICK_SCALE) {
        var newscale = this._logScale + CLICK_SCALE_DELTA * (e.altKey ? 1 : -1);
        newscale = Math.min(newscale, MAX_DOUBLECLICK_SCALE);

        var coords = this.eventCoords(e);
        this._setScale(newscale, coords.x, coords.y);
      }

      fireEvent(this, 'DoubleClick', this.eventToWorldCoords(e));
    }.bind(this));

    container.addEventListener('wheel', function(e) {
      this.cancelAnimation();

      var newscale = this._logScale + SCROLL_SCALE_DELTA * Math.sign(e.deltaY);
      var coords = this.eventCoords(e);
      this._setScale(newscale, coords.x, coords.y);

      e.preventDefault();
      e.stopPropagation();
    }.bind(this));

    window.addEventListener('resize', function() {
      var rect = boundingElement.getBoundingClientRect();
      if (rect.left === this.rect.left &&
          rect.top === this.rect.top &&
          rect.width === this.rect.width &&
          rect.height === this.rect.height) return;
      this.rect = rect;
      this.resetCanvas();
    }.bind(this));


    var pinch1, pinch2;
    var touch_coords, touch_wx, touch_wc, was_touch_dragged;

    container.addEventListener('touchmove', function(e) {
      was_touch_dragged = true;
      if (e.touches.length === 1) {

        var coords = this.eventCoords(e.touches[0]);
        this._offset(touch_coords.x - coords.x, touch_coords.y - coords.y);
        touch_coords = coords;
        touch_wc = this.eventToWorldCoords(e.touches[0]);

      } else if (e.touches.length === 2) {

        var od = dist(pinch2.x - pinch1.x, pinch2.y - pinch1.y),
            ocx = (pinch1.x + pinch2.x) / 2,
            ocy = (pinch1.y + pinch2.y) / 2;

        pinch1 = this.eventCoords(e.touches[0]),
        pinch2 = this.eventCoords(e.touches[1]);

        var nd = dist(pinch2.x - pinch1.x, pinch2.y - pinch1.y),
            ncx = (pinch1.x + pinch2.x) / 2,
            ncy = (pinch1.y + pinch2.y) / 2;

        this._offset(ocx - ncx, ocy - ncy);

        var newscale = this._logScale + log2(nd / od);
        this._setScale(newscale, ncx, ncy);
      }

      e.preventDefault();
      e.stopPropagation();
    }.bind(this), true);

    container.addEventListener('touchend', function(e) {
      if (e.touches.length < 2) {
        this.defer_loading = false;
        this.invalidate();
      }

      if (e.touches.length === 1)
        touch_coords = this.eventCoords(e.touches[0]);

      if (e.touches.length === 0 && !was_touch_dragged) {
        fireEvent(this, 'Click',
                  Object.assign({}, touch_wc, {activeElement: previous_focus}));
      }
      e.preventDefault();
      e.stopPropagation();
    }.bind(this), true);

    container.addEventListener('touchstart', function(e) {
      was_touch_dragged = false;
      previous_focus = document.activeElement;

      if (e.touches.length === 1) {
        touch_coords = this.eventCoords(e.touches[0]);
        touch_wc = this.eventToWorldCoords(e.touches[0]);
      } else if (e.touches.length === 2) {
        this.defer_loading = true;
        pinch1 = this.eventCoords(e.touches[0]),
        pinch2 = this.eventCoords(e.touches[1]);
      }

      e.preventDefault();
      e.stopPropagation();
    }.bind(this), true);

    container.addEventListener('keydown', function(e) {
      if (e.ctrlKey || e.altKey || e.metaKey)
        return;

      // TODO: Use KeyboardEvent.prototype.key if available
      var VK_I = KeyboardEvent.DOM_VK_I || 0x49,
          VK_J = KeyboardEvent.DOM_VK_J || 0x4A,
          VK_K = KeyboardEvent.DOM_VK_K || 0x4B,
          VK_L = KeyboardEvent.DOM_VK_L || 0x4C,
          VK_LEFT = KeyboardEvent.DOM_VK_LEFT || 0x25,
          VK_UP = KeyboardEvent.DOM_VK_UP || 0x26,
          VK_RIGHT = KeyboardEvent.DOM_VK_RIGHT || 0x27,
          VK_DOWN = KeyboardEvent.DOM_VK_DOWN || 0x28,
          VK_SUBTRACT = KeyboardEvent.DOM_VK_HYPHEN_MINUS || 0xBD,
          VK_EQUALS = KeyboardEvent.DOM_VK_EQUALS || 0xBB;

      switch (e.keyCode) {
        case VK_UP:
        case VK_I: this.Scroll(0, -KEY_SCROLL_DELTA); break;
        case VK_LEFT:
        case VK_J: this.Scroll(-KEY_SCROLL_DELTA, 0); break;
        case VK_DOWN:
        case VK_K: this.Scroll(0, KEY_SCROLL_DELTA); break;
        case VK_RIGHT:
        case VK_L: this.Scroll(KEY_SCROLL_DELTA, 0); break;
        case VK_SUBTRACT: this.ZoomOut(); break;
        case VK_EQUALS: this.ZoomIn(); break;
        default: return;
      }

      e.preventDefault();
      e.stopPropagation();
    }.bind(this));

    this.resetCanvas();
    this.defer_loading = false;
    this.invalidate();

    if (window == window.top) // == for IE
      container.focus();
  }

  // ======================================================================
  // Internal Methods
  // ======================================================================

  TravellerMap.prototype._offset = function(dx, dy) {
    this.position = [this.x + dx / this.scale, this.y - dy / this.scale];
  };

  TravellerMap.prototype._setScale = function(newscale, px, py) {
    newscale = Math.max(Math.min(newscale, this.max_scale), this.min_scale);
    if (newscale === this._logScale)
      return;

    var cw = this.rect.width,
        ch = this.rect.height;

    // Mathmagic to preserve hover coordinates
    var hx, hy;
    if (arguments.length >= 3) {
      hx = (this.x + (px - cw / 2) / this.scale) / this.tilesize;
      hy = (-this.y + (py - ch / 2) / this.scale) / this.tilesize;
    }

    this._logScale = newscale;

    if (arguments.length >= 3) {
      this.position = [hx * this.tilesize - (px - cw / 2) / this.scale,
                       -(hy * this.tilesize - (py - ch / 2) / this.scale)];
    }

    this.invalidate();
    fireEvent(this, 'ScaleChanged', this.scale);
  };

  TravellerMap.prototype.resetCanvas = function() {
    var cw = this.rect.width;
    var ch = this.rect.height;

    var dpr = 'devicePixelRatio' in window ? window.devicePixelRatio : 1;

    // iOS devices have a limit of 3 or 5 megapixels for canvas backing
    // store; given screen resolution * ~3x size for "tilt" display this
    // can easily be reached, so reduce effective dpr.
    if (dpr > 1 && /\biPad\b/.test(navigator.userAgent) &&
        this.tilt_enabled &&
        (cw * ch * dpr * dpr * 2 * 2) > 3e6) {
      dpr = 1;
    }

    // Scale factor for canvas to accomodate tilt.
    var sx = 1, sy = 1;
    if (this.tilt_enabled) {
      sx = 1.75;
      sy = 1.85;
    }

    // Pixel size of the canvas backing store.
    var pw = (cw * sx * dpr) | 0;
    var ph = (ch * sy * dpr) | 0;

    // Offset of the canvas against the container.
    var ox = 0, oy = 0;
    if (this.tilt_enabled) {
      ox = (-((cw * sx) - cw) / 2) | 0;
      oy = (-((ch * sy) - ch) * 0.8) | 0;
    }

    this.canvas.width = pw;
    this.canvas.height = ph;
    this.canvas.style.width = ((cw * sx) | 0) + 'px';
    this.canvas.style.height = ((ch * sy) | 0) + 'px';
    this.canvas.offset_x = ox;
    this.canvas.offset_y = oy;
    this.canvas.style.left = ox + 'px';
    this.canvas.style.top = oy + 'px';
    this.ctx.setTransform(1,0,0,1,0,0);
    this.ctx.scale(dpr, dpr);

    this.redraw(true);
  };

  TravellerMap.prototype.invalidate = function() {
    this.dirty = true;
    if (this._raf_handle) return;

    this._raf_handle = requestAnimationFrame(function invalidationRAF(ms) {
      this._raf_handle = null;
      this.redraw();
    }.bind(this));
  };

  TravellerMap.prototype.redraw = function(force) {
    if (!this.dirty && !force)
      return;

    this.dirty = false;

    // Integral scale (the tiles that will be used)
    var tscale = Math.round(this._logScale);

    // Tile URL (apart from x/y/scale)
    var params = {options: this.options, style: this.style};
    this.namedOptions.forEach(function(value, key) {
      if (key === 'ew' || key === 'qz') return;
      params[key] = value;
    });
    if ('devicePixelRatio' in window && window.devicePixelRatio > 1)
      params.dpr = window.devicePixelRatio;
    this._tile_url_base = Util.makeURL(SERVICE_BASE + '/api/tile', params);

    // How the tiles themselves are scaled (naturally 1, unless pinched)
    var tmult = pow2(this._logScale - tscale),

    // From map space to tile space
    // (Traveller map coords change at each integral zoom level)
        cf = pow2(tscale - 1), // Coordinate factor (integral)

    // Compute edges in tile space
        cw = this.rect.width,
        ch = this.rect.height,

        l = this._tx * cf - (cw / 2) / (this.tilesize * tmult),
        r = this._tx * cf + (cw / 2) / (this.tilesize * tmult),
        t = this._ty * cf - (ch / 2) / (this.tilesize * tmult),
        b = this._ty * cf + (ch / 2) / (this.tilesize * tmult);

    // Quantize to bounding tiles
    l = Math.floor(l) - 1;
    t = Math.floor(t) - 1;
    r = Math.floor(r) + 1;
    b = Math.floor(b) + 1;

    // Add extra around l/t/r edges for "tilt" effect
    if (this.tilt_enabled) {
      l -= 1;
      t -= 2;
      r += 1;
    }

    var tileCount = (r - l + 1) * (b - t + 1);
    this.cache.ensureCapacity(tileCount * 2);

    // TODO: Defer loading of new tiles while in the middle of a zoom gesture
    // Draw a rectanglular area of the map in a spiral from the center of the requested map outward
    this.ctx.save();
    this.ctx.setTransform(1, 0, 0, 1, 0, 0);
    this.ctx.globalCompositeOperation = 'source-over';
    this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    this.ctx.restore();

    this.ctx.globalCompositeOperation = 'destination-over';
    this.drawRectangle(l, t, r, b, tscale, tmult, ch, cw, cf);

    // Draw markers and overlays.
    this.markers.forEach(this.drawMarker, this);
    this.overlays.forEach(this.drawOverlay, this);

    if (this.main)
      this.drawMain(this.main);
    if (this.route)
      this.drawRoute(this.route);

    if (this.namedOptions.get('ew'))
      this.drawWave(this.namedOptions.get('ew'));

    if (this.namedOptions.get('qz'))
      this.drawQZ();
  };

  // Draw a rectangle (x1, y1) to (x2, y2)
  TravellerMap.prototype.drawRectangle = function(x1, y1, x2, y2, scale, mult, ch, cw, cf) {
    var $this = this;
    var sizeMult = this.tilesize * mult;

    var dw = sizeMult;
    var dh = sizeMult;

    var ox = $this._tx * -cf * dw + (cw / 2);
    var oy = $this._ty * -cf * dh + (ch / 2);

    // Start from the center, work outwards, so center tiles load first.
    for (var dd = Math.floor((Math.min(x2 - x1 + 1, y2 - y1 + 1) + 1) / 2) - 1; dd >= 0; --dd)
      frame(x1 + dd, y1 + dd, x2 - dd, y2 - dd);

    function frame(x1, y1, x2, y2) {
      var x, y;
      if (y1 === y2) {
        for (x = x1; x <= x2; ++x) draw(x, y1);
      } else if (x1 === x2) {
        for (y = y1; y <= y2; ++y) draw(x1, y);
      } else {
        for (x = x1; x <= x2; ++x) { draw(x, y1); draw(x, y2); }
        for (y = y1 + 1; y <= y2 - 1; ++y) { draw(x1, y); draw(x2, y); }
      }
    }

    function draw(x, y) {
      var dx = x * dw + ox;
      var dy = y * dh + oy;
      $this.drawTile(x, y, scale, dx, dy, dw, dh);
    }
  };

  //
  // Draw the specified tile (scale, x, y) into the rectangle (dx, dy, dw, dh);
  // if the tile is not available it is requested, and higher/lower rez tiles
  // are used to fill in the gap until it loads.
  //
  TravellerMap.prototype.drawTile = function(x, y, scale, dx, dy, dw, dh) {
    var $this = this; // for closures

    function drawImage(img, x, y, w, h) {
      x -= $this.canvas.offset_x;
      y -= $this.canvas.offset_y;
      var px = x | 0;
      var py = y | 0;
      var pw = ((x + w) | 0) - px;
      var ph = ((y + h) | 0) - py;
      $this.ctx.drawImage(img, px, py, pw, ph);
    }

    var img = this.getTile(x, y, scale, this.invalidate.bind(this));

    if (img) {
      drawImage(img, dx, dy, dw, dh);
      return;
    }

    // Otherwise, while we're waiting, see if we have upscale/downscale versions to draw instead

    function drawLower(x, y, scale, dx, dy, dw, dh) {
      if (scale <= $this.min_scale)
        return;

      var tscale = scale - 1;
      var factor = pow2(scale - tscale);

      var tx = Math.floor(x / factor);
      var ty = Math.floor(y / factor);

      var ax = dx - dw * (x - (tx * factor));
      var ay = dy - dh * (y - (ty * factor));
      var aw = dw * factor;
      var ah = dh * factor;

      var img = $this.getTile(tx, ty, tscale);
      if (img)
        drawImage(img, ax, ay, aw, ah);
      else
        drawLower(tx, ty, tscale, ax, ay, aw, ah);
    }
    drawLower(x, y, scale, dx, dy, dw, dh);

    function drawHigher(x, y, scale, dx, dy, dw, dh) {
      if (scale >= $this.max_scale)
        return;

      var tscale = scale + 1;
      var factor = pow2(scale - tscale);

      for (var oy = 0; oy < 2; oy += 1) {
        for (var ox = 0; ox < 2; ox += 1) {

          var tx = (x / factor) + ox;
          var ty = (y / factor) + oy;
          var img = $this.getTile(tx, ty, tscale);

          var ax = dx + ox * dw * factor;
          var ay = dy + oy * dh * factor;
          var aw = dw * factor;
          var ah = dh * factor;

          if (img)
            drawImage(img, ax, ay, aw, ah);
          // NOTE:  Don't recurse if not found as it would try an exponential number of tiles
          // e.g. drawHigher(tx, ty, tscale, ax, ay, aw, ah);
        }
      }
    }
    drawHigher(x, y, scale, dx, dy, dw, dh);
  };


  //
  // Looks in the tile cache for the specified tile. If found, it is
  // returned immediately. If not found and a callback is specified,
  // the image is requested and the callback is called with the image
  // once it has successfully loaded.
  //
  TravellerMap.prototype.getTile = function(x, y, scale, callback) {
    var url = this._tile_url_base +
          '&x=' + String(x) + '&y=' + String(y) + '&scale=' + String(pow2(scale - 1));

    // Have it? Great, get out fast!
    var img = this.cache.fetch(url);
    if (img)
      return img;

    // Load if missing?
    if (!callback)
      return undefined;

    // In progress?
    if (this.loading.has(url))
      return undefined;

    if (this.defer_loading)
      return undefined;

    if ('onLine' in navigator && !navigator.onLine)
      return undefined;

    // Nope, better try loading it
    this.loading.add(url);

    Util.fetchImage(url)
      .then(function(img) {
        this.loading.delete(url);
        this.cache.insert(url, img);
        callback(img);
      }.bind(this), function() {
        this.loading.delete(url);
      }.bind(this));

    return undefined;
  };

  TravellerMap.prototype.shouldAnimateTo = function(scale, x, y) {
    // TODO: Allow scale changes if target is "visible" (zooming in)
    if (scale !== this.scale)
      return false;

    var threshold = Astrometrics.SectorHeight * 64 / this.scale;
    return dist(x - this.x, y - this.y) < threshold;
  };

  TravellerMap.prototype.cancelAnimation = function() {
    if (this.animation) {
      this.animation.cancel();
      this.animation = null;
    }
  };

  TravellerMap.prototype.animateTo = function(scale, x, y, sec) {
    return new Promise(function(resolve, reject) {
      this.cancelAnimation();
      sec = sec || 2.0;
      var os = this.scale,
          ox = this.x,
          oy = this.y;
      if (ox === x && oy === y && os === scale) {
        resolve();
        return;
      }

      this.animation = new Animation(sec, function(p) {
        return Animation.smooth(p, 1.0, 0.1, 0.25);
      });

      this.animation.onanimate = function(p) {
        // Interpolate scale in log space.
        this.scale = pow2(Animation.interpolate(log2(os), log2(scale), p));
        // TODO: If animating scale, this should follow an arc (parabola?) through 3space treating
        // scale as Z and computing a height such that the target is in view at the turnaround.
        var p2 = 1 - ((1-p) * (1-p));
        this.position = [Animation.interpolate(ox, x, p2), Animation.interpolate(oy, y, p2)];
        this.redraw();
      }.bind(this);

      this.animation.oncomplete = resolve;
      this.animation.oncancel = reject;
    }.bind(this));
  };

  TravellerMap.prototype.drawOverlay = function(overlay) {
    var ctx = this.ctx;
    ctx.save();
    ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 0.5;
    ctx.fillStyle = styleLookup(this.style, 'overlay_color');
    if (overlay.type === 'rectangle') {
      // Compute physical location
      var pt1 = this.mapToPixel(overlay.x, overlay.y);
      var pt2 = this.mapToPixel(overlay.x + overlay.w, overlay.y + overlay.h);
      ctx.fillRect(pt1.x, pt1.y, pt2.x - pt1.x, pt1.y - pt2.y);
    } else if (overlay.type === 'circle') {
      var pt = this.mapToPixel(overlay.x, overlay.y);
      var r = Math.abs(this.mapToPixel(overlay.x, overlay.y + overlay.r).y - pt.y);
      ctx.beginPath();
      ctx.ellipse(pt.x, pt.y, r, r, 0, 0, Math.PI*2);
      ctx.fill();
    }
    ctx.restore();
  };

  TravellerMap.prototype.drawRoute = function(route) {
    var ctx = this.ctx;
    ctx.save();
    ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 0.5;
    ctx.strokeStyle = styleLookup(this.style, 'route_color');
    if (this._logScale >= 7)
      ctx.lineWidth = 0.25 * this.scale;
    else
      ctx.lineWidth = 15;

    ctx.beginPath();
    route.forEach(function(stop, index) {
      var pt = Astrometrics.sectorHexToMap(stop.sx, stop.sy, stop.hx, stop.hy);
      pt = this.mapToPixel(pt.x, pt.y);
      ctx[index ? 'lineTo' : 'moveTo'](pt.x, pt.y);
    }, this);
    var dots = (this._logScale >= 7) ? route : [route[0], route[route.length - 1]];
    dots.forEach(function(stop, index) {
      var pt = Astrometrics.sectorHexToMap(stop.sx, stop.sy, stop.hx, stop.hy);
      pt = this.mapToPixel(pt.x, pt.y);
      ctx.moveTo(pt.x + ctx.lineWidth / 2, pt.y);
      ctx.arc(pt.x, pt.y, ctx.lineWidth / 2, 0, Math.PI*2);
    }, this);

    ctx.stroke();
    ctx.restore();
  };

  TravellerMap.prototype.drawMain = function(main) {
    var ctx = this.ctx;
    ctx.save();
    ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = styleLookup(this.style, 'main_opacity');
    ctx.fillStyle = styleLookup(this.style,
                                main.length <= 10 ? 'main_s_color' :
                                main.length <= 50 ? 'main_m_color' : 'main_l_color');
    ctx.beginPath();
    var radius = 1.15 * this.scale / 2;
    main.forEach(function(world) {
      var pt = Astrometrics.sectorHexToMap(world.sx, world.sy, world.hx, world.hy);
      pt = this.mapToPixel(pt.x, pt.y);
      ctx.moveTo(pt.x + radius, pt.y);
      ctx.arc(pt.x, pt.y, radius, 0, Math.PI*2);
    }, this);
    ctx.fill();
    ctx.restore();
  };

  TravellerMap.prototype.drawMarker = function(marker) {
    var pt = this.mapToPixel(marker.x, marker.y);

    var ctx = this.ctx;
    var image;

    if (marker.url) {
      image = stash.get(marker.url, this.invalidate.bind(this));
      if (!image) return;

      var MARKER_SIZE = 128;
      ctx.save();
      ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
      ctx.globalCompositeOperation = 'source-over';
      ctx.drawImage(image,
                    pt.x - MARKER_SIZE/2, pt.y - MARKER_SIZE/2,
                    MARKER_SIZE, MARKER_SIZE);
      ctx.restore();
      return;
    }

    if (styleLookup(this.style, marker.id + '_url')) {
      var url = styleLookup(this.style, marker.id + '_url');
      image = stash.get(url, this.invalidate.bind(this));
      if (!image) return;

      ctx.save();
      ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
      ctx.globalCompositeOperation = 'source-over';
      ctx.drawImage(image, pt.x, pt.y);
      ctx.restore();
    }
  };

  TravellerMap.prototype.drawWave = function(date) {
    var year = 1105;
    var w = 1; /*pc*/
    var m;
    if (date === 'milieu') {
      var milieu = this.namedOptions.get('milieu') || 'M1105';
      year = (milieu === 'IW') ? -2404 : Number(milieu.replace('M', ''));
    } else if ((m = /^(-?\d+)-(\d+)$/.exec(date))) {
      // day-year, e.g. 001-1105
      year = Number(m[2]) + (Number(m[1]) - 1) / 365;
      w = 0.1;
    } else if (/^(-?\d+)\.(\d*)$/.test(date)) {
      // decimal year, e.g. 1105.5
      year = Number(date);
      w = 0.1;
    } else if (/^-?\d+$/.test(date)) {
      // year
      year = Number(date) + 0.5;
      w = 1;
    }

    // Per MWM: Velocity of wave is PI * c
    var vel /*pc/y*/ = Math.PI /*ly/y*/ / 3.26 /*ly/pc*/;

    // Per MWM: center is 10000pc coreward
    var x = 0, y = 10000;

    // Per MWM: Wave crosses Ring 10,000 [Reference] on 045-1281
    var radius = (year - (1281 + (45 - 1) / 365)) * vel + y;
    if (radius < 0)
      return;

    var ctx = this.ctx;
    ctx.save();
    ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 0.3;
    ctx.lineWidth = Math.max(w * this.scale, 5);
    ctx.strokeStyle = styleLookup(this.style, 'ew_color');
    ctx.beginPath();
    var px_offset = 0.5; // offset from corner to center of hex
    var pt = this.mapToPixel(x + px_offset, y + px_offset);
    ctx.arc(pt.x,
      pt.y,
      this.scale * radius,
      Math.PI / 2 - Math.PI / 12,
      Math.PI / 2 + Math.PI / 12);
    ctx.stroke();
    ctx.restore();
  };

  TravellerMap.prototype.drawQZ = function() {
    var x = -179.4, y = 131, radius = 30 * Traveller.Astrometrics.ParsecScaleX, w = 1;
    var ctx = this.ctx;
    ctx.save();
    ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 0.3;
    ctx.lineWidth = Math.max(w * this.scale, 5);
    ctx.strokeStyle = styleLookup(this.style, 'ew_color');
    ctx.beginPath();
    var px_offset = 0.5; // offset from corner to center of hex
    var pt = this.mapToPixel(x + px_offset, y + px_offset);
    ctx.arc(pt.x,
        pt.y,
        this.scale * radius, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
  };

  TravellerMap.prototype.mapToPixel = function(mx, my) {
    return {
      x: (mx - this._tx * this.tilesize) * this.scale + this.rect.width / 2,
      y: (-my - this._ty * this.tilesize) * this.scale + this.rect.height / 2
    };
  };

  TravellerMap.prototype.pixelToMap = function(px, py) {
    return {
      x: this._tx * this.tilesize + (px - this.rect.width  / 2) / this.scale,
      y: -(this._ty * this.tilesize + (py - this.rect.height / 2) / this.scale)
    };
  };

  TravellerMap.prototype.eventCoords = function(event) {
    // Attempt to get transformed coords; offsetX/Y for Chrome/Safari/IE,
    // layerX/Y for Firefox. Touch events lack these, so compute untransformed
    // coords.
    // TODO: Map touch coordinates back into world-space.
    var offsetX = 'offsetX' in event ? event.offsetX :
          'layerX' in event ? event.layerX :
          event.pageX - event.target.offsetLeft;
    var offsetY = 'offsetY' in event ? event.offsetY :
          'layerY' in event ? event.layerY :
          event.pageY - event.target.offsetTop;

    return {
      x: offsetX - SINK_OFFSET - this.rect.left,
      y: offsetY - SINK_OFFSET - this.rect.top
    };
  };

  TravellerMap.prototype.eventToWorldCoords = function(event) {
    var coords = this.eventCoords(event);
    var map = this.pixelToMap(coords.x, coords.y);
    return Astrometrics.mapToWorld(map.x, map.y);
  };


  // ======================================================================
  // Public API
  // ======================================================================

  Object.defineProperties(TravellerMap.prototype, {
    scale: {
      get: function() { return pow2(this._logScale - 1); },
      set: function(value) {
        value = 1 + log2(Number(value));
        if (value === this._logScale)
          return;
        this._setScale(value);
      },
      enumerable: true, configurable: true
    },

    options: {
      get: function() { return this._options; },
      set: function(value) {
        if (LEGACY_STYLES) {
          // Handle legacy styles specified in options bits
          if ((value & MapOptions.StyleMaskDeprecated) === MapOptions.PrintStyleDeprecated)
            this.style = 'atlas';
          else if ((value & MapOptions.StyleMaskDeprecated) === MapOptions.CandyStyleDeprecated)
            this.style = 'candy';
          value = value & ~MapOptions.StyleMaskDeprecated;
        }

        value = value & MapOptions.Mask;
        if (value === this._options) return;

        this._options = value;
        this.cache.clear();
        this.invalidate();
        fireEvent(this, 'OptionsChanged', this._options);
      },
      enumerable: true, configurable: true
    },

    style: {
      get: function() { return this._style; },
      set: function(value) {
        if (value === this._style) return;

        this._style = value;
        this.cache.clear();
        this.invalidate();
        fireEvent(this, 'StyleChanged', this._style);
      },
      enumerable: true, configurable: true
    },

    x: {
      get: function() { return this._tx * this.tilesize; },
      set: function(value) { this.position = [value, this.y]; },
      enumerable: true, configurable: true
    },

    y: {
      get: function() { return this._ty * -this.tilesize; },
      set: function(value) { this.position = [this.x, value]; },
      enumerable: true, configurable: true
    },

    position: {
      get: function() { return [this._tx * this.tilesize, this._ty * -this.tilesize]; },
      set: function(value) {
        var x = value[0] / this.tilesize, y = value[1] / -this.tilesize;
        if (x === this._tx && y === this._ty) return;
        this._tx = x;
        this._ty = y;
        this.invalidate();
        fireEvent(this, 'PositionChanged');
      },
      enumerable: true, configurable: true
    },

    worldX: {
      get: function() { return Astrometrics.mapToWorld(this.x, this.y).x; },
      enumerable: true, configurable: true
    },

    worldY: {
      get: function() { return Astrometrics.mapToWorld(this.x, this.y).y; },
      enumerable: true, configurable: true
    }
  });


  // This places the specified Sector, Hex coordinates (parsec)
  // at the center of the viewport.
  TravellerMap.prototype.CenterAtSectorHex = function(sx, sy, hx, hy, options) {
    options = Object.assign({}, options);

    this.cancelAnimation();
    var target = Astrometrics.sectorHexToMap(sx, sy, hx, hy);

    if (!options.immediate &&
        'scale' in options &&
        this.shouldAnimateTo(options.scale, target.x, target.y)) {
      this.animateTo(options.scale, target.x, target.y)
        .catch(function(){});
      return;
    }

    if ('scale' in options)
      this.scale = options.scale;
    this.position = [target.x, target.y];
  };


  // Scroll the map view by the specified dx/dy (in pixels)
  TravellerMap.prototype.Scroll = function(dx, dy, fAnimate) {
    this.cancelAnimation();

    if (!fAnimate) {
      this._offset(dx, dy);
      return;
    }

    var s = this.scale * this.tilesize,
        ox = this.x,
        oy = this.y,
        tx = ox + dx / s,
        ty = oy + dy / s;

    this.animation = new Animation(1.0, function(p) {
      return Animation.smooth(p, 1.0, 0.1, 0.25);
    });
    this.animation.onanimate = function(p) {
      this.position = [Animation.interpolate(ox, tx, p), Animation.interpolate(oy, ty, p)];
    }.bind(this);
  };

  var ZOOM_DELTA = 0.5;
  function roundScale(s) {
    return Math.round(s / ZOOM_DELTA) * ZOOM_DELTA;
  }

  TravellerMap.prototype.ZoomIn = function() {
    this._setScale(roundScale(this._logScale) + ZOOM_DELTA);
  };

  TravellerMap.prototype.ZoomOut = function() {
    this._setScale(roundScale(this._logScale) - ZOOM_DELTA);
  };


  // NOTE: This API is subject to change
  // |x| and |y| are map-space coordinates
  TravellerMap.prototype.AddMarker = function(id, x, y, opt_url) {
    var marker = {
      x: x,
      y: y,
      id: id,
      url: opt_url,
      z: 909
    };

    this.markers.push(marker);
    this.invalidate();
  };


  TravellerMap.prototype.AddOverlay = function(o) {
    // TODO: Take id, like AddMarker
    var overlay = Object.assign({
      id: 'overlay',
      z: 910
    }, o);

    this.overlays.push(overlay);
    this.invalidate();
  };

  TravellerMap.prototype.SetRoute = function(route) {
    this.route = route;
    this.invalidate();
  };

  TravellerMap.prototype.SetMain = function(main) {
    this.main = main;
    this.invalidate();
  };

  TravellerMap.prototype.EnableTilt = function() {
    this.tilt_enabled = true;
    this.resetCanvas();
  };

  TravellerMap.prototype.ApplyURLParameters = function() {
    var params = Util.parseURLQuery(document.location);

    function float(prop) {
      var n = parseFloat(params[prop]);
      return isNaN(n) ? 0 : n;
    }

    function int(prop) {
      var v = params[prop];
      if (typeof v === 'boolean') return v ? 1 : 0;
      var n = parseInt(v, 10);
      return isNaN(n) ? 0 : n;
    }

    function has(params, list) {
      return list.every(function(item) { return item in params; });
    }

    if ('scale' in params)
      this.scale = float('scale');

    if ('options' in params)
      this.options = int('options');

    if ('style' in params)
      this.style = params.style;

    var pt;

    if (has(params, ['yah_sx', 'yah_sy', 'yah_hx', 'yah_hx'])) {
      pt = Astrometrics.sectorHexToMap(int('yah_sx'), int('yah_sy'), int('yah_hx'), int('yah_hy'));
      this.AddMarker('you_are_here', pt.x, pt.y);
    } else if (has(params, ['yah_x', 'yah_y'])) {
      this.AddMarker('you_are_here', float('yah_x'), float('yah_y'));
    } else if (has(params, ['yah_sector'])) {
      MapService.coordinates(params.yah_sector, params.yah_hex)
        .then(function(location) {
          var pt = Astrometrics.worldToMap(location.x, location.y);
          this.AddMarker('you_are_here', pt.x, pt.y);
        }.bind(this), function() {
          alert('The requested marker location "' + params.yah_sector +
                ('yah_hex' in params ? (' ' + params.yah_hex) : '') +
                '" was not found.');
        });
    }

    if (has(params, ['marker_sx', 'marker_sy', 'marker_hx', 'marker_hx', 'marker_url'])) {
      pt = Astrometrics.sectorHexToMap(int('marker_sx'), int('marker_sy'), int('marker_hx'), int('marker_hy'));
      this.AddMarker('custom', pt.x, pt.y, params.marker_url);
    } else if (has(params, ['marker_x', 'marker_y', 'marker_url'])) {
      this.AddMarker('custom', float('marker_x'), float('marker_y'), params.marker_url);
    } else if (has(params, ['marker_sector', 'marker_url'])) {
      MapService.coordinates(params.marker_sector, params.marker_hex)
        .then(function(location) {
          var pt = Astrometrics.worldToMap(location.x, location.y);
          this.AddMarker('custom', pt.x, pt.y, params.marker_url);
        }.bind(this), function() {
          alert('The requested marker location "' + params.marker_sector +
                ('marker_hex' in params ? (' ' + params.marker_hex) : '') +
                '" was not found.');
        });
    }

    for (var i = 0; ; ++i) {
      var n = (i === 0) ? '' : i, oxs = 'ox' + n, oys = 'oy' + n, ows = 'ow' + n, ohs = 'oh' + n;
      if (has(params, [oxs, oys, ows, ohs])) {
        var x = float(oxs);
        var y = float(oys);
        var w = float(ows);
        var h = float(ohs);
        this.AddOverlay({type: 'rectangle', x:x, y:y, w:w, h:h});
      } else {
        break;
      }
    }
    for ( i = 0; ; ++i) {
      n = (i === 0) ? '' : i;
      var ocxs = 'ocx' + n, ocys = 'ocy' + n, ocrs = 'ocr' + n;
      if (has(params, [ocxs, ocys, ocrs])) {
        var cx = float(ocxs);
        var cy = float(ocys);
        var cr = float(ocrs);
        this.AddOverlay({type: 'circle', x:cx, y:cy, r:cr});
      } else {
        break;
      }
    }

    // Various coordinate schemes - ordered by priority
    if (has(params, ['x', 'y'])) {
      this.position = [float('x'), float('y')];
    } else if (has(params, ['sx', 'sy', 'hx', 'hy', 'scale'])) {
      this.CenterAtSectorHex(
        float('sx'), float('sy'), float('hx'), float('hy'), {scale: float('scale')});
    } else if ('sector' in params) {
      MapService.coordinates(params.sector, params.hex, {subsector: params.subsector})
        .then(function(location) {
          if (location.hx && location.hy) { // NOTE: Test for undefined -or- zero
            this.CenterAtSectorHex(location.sx, location.sy, location.hx, location.hy, {scale: 64});
          } else {
            this.CenterAtSectorHex(location.sx, location.sy,
                                   Astrometrics.SectorWidth / 2, Astrometrics.SectorHeight / 2,
                                   {scale: 16});
          }

          if ('yah' in params) {
            this.AddMarker('you_are_here', this.position[0], this.position[1]);
            params.yah_x = String(this.position[0]);
            params.yah_y = String(this.position[1]);
            delete params.yah;
          }

          if ('marker' in params) {
            this.AddMarker('custom', this.position[0], this.position[1], params['marker']);
            params.marker_url = params.marker;
            params.marker_x = String(this.position[0]);
            params.marker_y = String(this.position[1]);
            delete params.marker;
          }

        }.bind(this), function() {
          alert('The requested location "' + params.sector +
                ('hex' in params ? (' ' + params.hex) : '') + '" was not found.');
        });
    }

    // Int/Boolean options
    INT_OPTIONS.forEach(function(name) {
      if (name in params)
        this.namedOptions.set(name, int(name));
    }, this);
    // String options
    STRING_OPTIONS.forEach(function(name) {
      if (name in params)
        this.namedOptions.set(name, params[name]);
    }, this);

    return params;
  };

  //----------------------------------------------------------------------
  // Exports
  //----------------------------------------------------------------------

  global.Traveller = {
    SERVICE_BASE: SERVICE_BASE,
    LEGACY_STYLES: LEGACY_STYLES,
    Astrometrics: Astrometrics,
    Map: TravellerMap,
    MapOptions: MapOptions,
    Styles: Styles,
    MapService: MapService,
    fromHex: fromHex
  };

}(this));
