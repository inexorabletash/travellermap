// ======================================================================
// Exported Functionality
// ======================================================================

// NOTE: Used by other scripts
var Util = {
  makeURL: function(base, params) {
    'use strict';
    base = String(base).replace(/\?.*/, '');
    var keys = Object.keys(params);
    if (keys.length === 0) return base;
    return base += '?' + keys.filter(function (p) {
      return params[p] !== undefined;
    }).map(function (p) {
      return encodeURIComponent(p) + '=' + encodeURIComponent(params[p]);
    }).join('&');
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

  // Replace with Fetch API (or polyfill)
  fetch: function(url, options, callback, errback) {
    // NOTE: Due to proxies/user-agents not respecting Vary tags
    // prefer URL params instead of headers for specifying 'Accept'
    // type(s).
    try {
      options = options || {};
      var method = options.method || 'GET';
      var headers = options.headers || {};
      var body = options.body || null;
      var xhr = new XMLHttpRequest(), async = true;
      xhr.open(method, url, async);
      Object.keys(headers).forEach(function(key) {
        xhr.setRequestHeader(key, headers[key]);
      });
      xhr.onreadystatechange = function() {
        if (xhr.readyState !== XMLHttpRequest.DONE) return;
        if (xhr.status === 200) {
          if (callback) callback(xhr);
        } else {
          if (errback) errback(xhr);
        }
      };
      var original_abort = xhr.abort;
      xhr.abort = function() {
        xhr.onreadystatechange = null;
        if (original_abort)
          original_abort.call(xhr);
      };
      xhr.send(body);
      return xhr;
    } catch (ex) {
      // If cross-domain, blocked by browsers that don't implement CORS.
      if (errback)
        setTimeout(function() { errback(ex.message); }, 0);
      return {
        abort: function() {},
        readyState: XMLHttpRequest.DONE,
        status: 0,
        statusText: 'Forbidden',
        responseText: 'Connection error'
      };
    }
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
  }
};


(function (global) {
  'use strict';

  //----------------------------------------------------------------------
  // General Traveller stuff
  //----------------------------------------------------------------------

  var SERVICE_BASE = (function(l) {
    'use strict';
    if (l.hostname === 'localhost' && l.pathname.indexOf('~') !== -1)
      return 'http://travellermap.com';
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
    Draft: 'draft'
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
    MaxScale: 512
  };

  // ======================================================================
  // Data Services
  // ======================================================================

  // TODO: Make these ES6-Promise based
  var MapService = (function() {
    function service(url, contentType, callback, errback) {
      return Util.fetch(
        url,
        {headers: {Accept: contentType}},
        function(response) {
          var data = response.responseText;
          if (contentType === 'application/json') {
            try {
              data = JSON.parse(data);
            } catch (ex) {
              if (errback)
                errback(ex);
              return;
            }
          }
          callback(data);
        },
        function(error) {
          if (errback)
            errback(Error(error.statusText));
        });
    }

    return {
      coordinates: function(sector, hex, callback, errback, options) {
        options = options || {};
        options.sector = sector;
        options.hex = hex;
        return service(Util.makeURL(SERVICE_BASE + '/api/coordinates', options),
                       options.accept || 'application/json', callback, errback);
      },

      credits: function (hexX, hexY, callback, errback, options) {
        options = options || {};
        options.x = hexX;
        options.y = hexY;
        return service(Util.makeURL(SERVICE_BASE + '/api/credits', options),
                       options.accept || 'application/json', callback, errback);
      },

      search: function (query, callback, errback, options) {
        options = options || {};
        options.q = query;
        return service(Util.makeURL(SERVICE_BASE + '/api/search', options),
                       options.accept || 'application/json', callback, errback);
      },

      sectorData: function (sector, callback, errback, options) {
        options = options || {};
        options.sector = sector;
        return service(Util.makeURL(SERVICE_BASE + '/api/sec', options),
                       options.accept || 'text/plain', callback, errback);
      },

      sectorDataTabDelimited: function (sector, callback, errback, options) {
        options = options || {};
        options.sector = sector;
        options.type = 'TabDelimited';
        return service(Util.makeURL(SERVICE_BASE + '/api/sec', options),
                       options.accept || 'text/plain', callback, errback);
      },

      sectorMetaData: function (sector, callback, errback, options) {
        options = options || {};
        options.sector = sector;
        return service(Util.makeURL(SERVICE_BASE + '/api/metadata', options),
                       options.accept || 'application/json', callback, errback);
      },

      MSEC: function (sector, callback, errback, options) {
        options = options || {};
        options.sector = sector;
        return service(Util.makeURL(SERVICE_BASE + '/api/msec', options),
                       options.accept || 'text/plain', callback, errback);
      },

      universe: function (callback, errback, options) {
        options = options || {};
        return service(Util.makeURL(SERVICE_BASE + '/api/universe', options),
                       options.accept || 'application/json', callback, errback);
      }
    };
  }());

  // ======================================================================
  // Least-Recently-Used Cache
  // ======================================================================

  var LRUCache = function(capacity) {
    this.capacity = capacity;
    this.cache = {};
    this.queue = [];

    this.ensureCapacity = function(capacity) {
      if (this.capacity < capacity)
        this.capacity = capacity;
    };

    this.clear = function() {
      this.cache = [];
      this.queue = [];
    };

    this.fetch = function(key) {
      var value = this.cache[key];
      if (value === undefined)
        return undefined;

      var index = this.queue.indexOf(key);
      if (index !== -1)
        this.queue.splice(index, 1);
      this.queue.push(key);
      return value;
    };

    this.insert = function(key, value) {
      // Remove previous instances
      var index = this.queue.indexOf(key);
      if (index !== -1)
        this.queue.splice(index, 1);

      this.cache[key] = value;
      this.queue.push(key);

      while (this.queue.length > this.capacity) {
        key = this.queue.shift();
        delete this.cache[key];
      }
    };
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

  // ======================================================================
  // Helper functions to normalize behavior across event models
  // ======================================================================

  var DOMHelpers = {
    setCapture: function(element) {
      // Prevents click events in FF (?!?)
      var isIE = navigator.userAgent.indexOf('MSIE') !== -1;
      if (isIE && 'setCapture' in element)
        element.setCapture(true);
    },
    releaseCapture: function(element) {
      var isIE = navigator.userAgent.indexOf('MSIE') !== -1;
      if (isIE && 'releaseCapture' in element)
        element.releaseCapture();
    }
  };

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
    var Animation = function(dur, smooth) {
      var start = Date.now();
      var self = this;
      this.timerid = requestAnimationFrame(tickFunc);

      function tickFunc() {
        var f = (Date.now() - start) / 1000 / dur;
        if (f < 1.0)
          requestAnimationFrame(tickFunc);

        var p = f;
        if (isCallable(smooth))
          p = smooth(p);

        if (isCallable(self.onanimate))
          self.onanimate(p);

        if (f >= 1.0 && isCallable(self.oncomplete))
          self.oncomplete();
      }
    };

    Animation.prototype.cancel = function() {
      if (this.timerid) {
        cancelAnimationFrame(this.timerid);
        if (isCallable(this.oncancel))
          this.oncancel();
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


  //----------------------------------------------------------------------
  //
  // Usage:
  //
  //   var map = new Map( document.getElementById('YourMapDiv') );
  //
  //   map.OnScaleChanged   = function() { update scale indicator }
  //   map.OnOptionsChanged = function() { update control panel }
  //   map.OnStyleChanged   = function() { update control panel }
  //   map.OnDisplayChanged = function() { update permalink }
  //   map.OnHover          = function( {x, y} ) { show data }
  //   map.OnClick          = function( {x, y} ) { show data }
  //   map.OnDoubleClick    = function( {x, y} ) { show data }
  //
  //   var hx = map.GetHexX();
  //   var hy = map.GetHexY();
  //   var x = map.GetX();
  //   var y = map.GetY();
  //   var s = map.GetScale();
  //   var o = map.GetOptions();
  //
  //   map.SetScale( scale, bRefresh );
  //   map.SetOptions( flags, bRefresh );
  //   map.SetStyle( style, bRefresh );
  //   map.SetPosition( x, y );
  //
  //   map.ScaleCenterAtSectorHex( scale, sx, sy, hx, hy );
  //   map.CenterAtSectorHex( sx, sy, hx, hy );
  //   map.Scroll( dx, dy, fAnimate );
  //   map.ZoomIn();
  //   map.ZoomOut();
  //
  //   map.AddMarker(id, sx, sy, hx, hy, opt_url); // should have CSS style for .marker#<id>
  //   map.AddOverlay(x, y, w, h); // should have CSS style for .overlay
  //
  //----------------------------------------------------------------------

  function sectorHexToLogical(sx, sy, hx, hy) {
    // Offset from origin
    var x = (sx * Astrometrics.SectorWidth) + hx - Astrometrics.ReferenceHexX;
    var y = (sy * Astrometrics.SectorHeight) + hy - Astrometrics.ReferenceHexY;

    // Offset from the "corner" of the hex
    x -= 0.5;
    y -= ((hx % 2) === 0) ? 0 : 0.5;

    // Scale to non-homogenous coordinates
    x *= Astrometrics.ParsecScaleX;
    y *= -Astrometrics.ParsecScaleY;

    // Drop precision (avoid animations, etc)
    x = Math.round(x * 1000) / 1000;
    y = Math.round(y * 1000) / 1000;

    return {x: x, y: y};
  }

  function logicalToHex(x, y) {
    var hx = Math.round((x / Astrometrics.ParsecScaleX) + 0.5);
    var hy = Math.round((-y / Astrometrics.ParsecScaleY) + ((hx % 2 === 0) ? 0.5 : 0));
    return {hx: hx, hy: hy};
  }

  function fireEvent(target, event, data) {
    if (typeof target['On' + event] === 'function') {
      try {
        target['On' + event](data);
      } catch (ex) {
        if (console && console.error)
          console.error('Event handler for ' + event + ' threw:', ex);
      }
    }
  }

  function eventCoords(event) {
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
    return { x: offsetX - SINK_OFFSET, y: offsetY - SINK_OFFSET};
  }

  // ======================================================================
  // Slippy Map using Tiles
  // ======================================================================

  function log2(v) { return Math.log(v) / Math.LN2; }
  function pow2(v) { return Math.pow(2, v); }

  var SINK_OFFSET = 1000;

  function Map(container) {

    var self = this; // For event closures that may muck with 'this'

    this.container = container;

    this.min_scale = -5;
    this.max_scale = 10;

    this.options = Defaults.options;
    this.style = Defaults.style;
    this.tileOptions = {};

    this.scale = 1;
    this.x = 0;
    this.y = 0;

    this.tilesize = 256;

    this.cache = new LRUCache(64);

    this.loading = {};
    this.pass = 0;

    this.defer_loading = false;

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

    this.canvas = null;
    this.ctx = null;
    var canvas = document.createElement('canvas');
    if ('getContext' in canvas && canvas.getContext('2d')) {
      var cw = container.offsetWidth;
      var ch = container.offsetHeight;
      var pw = (cw * 2) | 0;
      var ph = (ch * 2) | 0;

      canvas.width = pw;
      canvas.height = ph;
      canvas.offset_x = -(cw / 2);
      canvas.offset_y = -ch;
      canvas.style.width = cw * 2;
      canvas.style.height = ch * 2;
      canvas.style.left = canvas.offset_x + 'px';
      canvas.style.top = canvas.offset_y + 'px';

      canvas.style.position = 'absolute';
      canvas.style.zIndex = 0;
      container.appendChild(canvas);
      this.canvas = canvas;
      this.ctx = canvas.getContext('2d');
    }

    this.markers = [];
    this.overlays = [];

    // ======================================================================
    // Event Handlers
    // ======================================================================

    function eventToHexCoords(event) {
      var f = pow2(1 - self.scale) / self.tilesize;
      var coords = eventCoords(event);
      var cx = self.x + f * (coords.x - self.container.offsetWidth / 2),
          cy = self.y + f * (coords.y - self.container.offsetHeight / 2);
      return logicalToHex(cx * self.tilesize, cy * -self.tilesize);
    }

    var dragging, drag_x, drag_y;
    container.addEventListener('mousedown', function(e) {
      self.cancelAnimation();
      container.focus();
      dragging = true;
      var coords = eventCoords(e);
      drag_x = coords.x;
      drag_y = coords.y;
      DOMHelpers.setCapture(container);
      container.classList.add('dragging');

      e.preventDefault();
      e.stopPropagation();
    }, true);

    var hover_x, hover_y;
    container.addEventListener('mousemove', function(e) {
      if (dragging) {
        var coords = eventCoords(e);
        var dx = drag_x - coords.x;
        var dy = drag_y - coords.y;

        self.offset(dx, dy);

        drag_x = coords.x;
        drag_y = coords.y;
        e.preventDefault();
        e.stopPropagation();
      }

      var hex = eventToHexCoords(e);

      // Throttle the events
      if (hover_x !== hex.hx || hover_y !== hex.hy) {
        hover_x = hex.hx;
        hover_y = hex.hy;
        fireEvent(self, 'Hover', { x: hex.hx, y: hex.hy });
      }

    }, true);

    document.addEventListener('mouseup', function(e) {
      if (dragging) {
        dragging = false;
        container.classList.remove('dragging');
        DOMHelpers.releaseCapture(document);
        e.preventDefault();
        e.stopPropagation();
      }
    });

    container.addEventListener('click', function(e) {
      e.preventDefault();
      e.stopPropagation();

      var hex = eventToHexCoords(e);
      fireEvent(self, 'Click', { x: hex.hx, y: hex.hy });
    });

    container.addEventListener('dblclick', function(e) {
      self.cancelAnimation();

      e.preventDefault();
      e.stopPropagation();

      var MAX_DOUBLECLICK_SCALE = 9;
      if (self.scale >= MAX_DOUBLECLICK_SCALE)
        return;

      var newscale = self.scale + CLICK_SCALE_DELTA * ((e.altKey) ? 1 : -1);
      newscale = Math.min(newscale, MAX_DOUBLECLICK_SCALE);

      coords = eventCoords(e);
      self.setScale(newscale, coords.x, coords.y);

      // Compute the physical coordinates
      var f = pow2(1 - self.scale) / self.tilesize,
          coords = eventCoords(e),
          cx = self.x + f * (coords.x - self.container.offsetWidth / 2),
          cy = self.y + f * (coords.y - self.container.offsetHeight / 2),
          hex = logicalToHex(cx * self.tilesize, cy * -self.tilesize);

      fireEvent(self, 'DoubleClick', { x: hex.hx, y: hex.hy });
    });

    var wheelListener = function(e) {
      self.cancelAnimation();
      var delta = e.detail ? e.detail * -40 : e.wheelDelta;

      var newscale = self.scale + SCROLL_SCALE_DELTA * ((delta > 0) ? -1 : (delta < 0) ? 1 : 0);

      var coords = eventCoords(e);
      self.setScale(newscale, coords.x, coords.y);

      e.preventDefault();
      e.stopPropagation();
    };
    container.addEventListener('mousewheel', wheelListener); // IE/Chrome/Safari/Opera
    container.addEventListener('DOMMouseScroll', wheelListener); // FF

    window.addEventListener('resize', function() {
      self.redraw(true); // synchronous
    });

    var pinch_x1, pinch_y1, pinch_x2, pinch_y2;
    var touch_x, touch_y;

    container.addEventListener('touchmove', function(e) {
      function dist(x1, y1, x2, y2) {
        var dx = x2 - x1, dy = y2 - y1;
        return Math.sqrt(dx * dx + dy * dy);
      }

      if (e.touches.length === 1) {

        var coords = eventCoords(e.touches[0]);
        var dx = touch_x - coords.x;
        var dy = touch_y - coords.y;

        self.offset(dx, dy);

        touch_x = coords.x;
        touch_y = coords.y;

      } else if (e.touches.length === 2) {

        var od = dist(pinch_x1, pinch_y1, pinch_x2, pinch_y2),
            ocx = (pinch_x1 + pinch_x2) / 2,
            ocy = (pinch_y1 + pinch_y2) / 2;

        var coords0 = eventCoords(e.touches[0]),
            coords1 = eventCoords(e.touches[1]);
        pinch_x1 = coords0.x;
        pinch_y1 = coords0.y;
        pinch_x2 = coords1.x;
        pinch_y2 = coords1.y;

        var nd = dist(pinch_x1, pinch_y1, pinch_x2, pinch_y2),
            ncx = (pinch_x1 + pinch_x2) / 2,
            ncy = (pinch_y1 + pinch_y2) / 2;

        self.offset(ocx - ncx, ocy - ncy);

        var newscale = self.scale + log2(nd / od);
        self.setScale(newscale, ncx, ncy);
      }

      e.preventDefault();
      e.stopPropagation();
    }, true);

    container.addEventListener('touchend', function(e) {
      if (e.touches.length < 2) {
        self.defer_loading = false;
        self.invalidate();
      }

      if (e.touches.length === 1) {
        var coords = eventCoords(e.touches[0]);
        touch_x = coords.x;
        touch_y = coords.y;
      }

      e.preventDefault();
      e.stopPropagation();
    }, true);

    container.addEventListener('touchstart', function(e) {
      if (e.touches.length === 1) {
        var coords = eventCoords(e.touches[0]);
        touch_x = coords.x;
        touch_y = coords.y;
      } else if (e.touches.length === 2) {
        self.defer_loading = true;
        var coords0 = eventCoords(e.touches[0]),
            coords1 = eventCoords(e.touches[1]);
        pinch_x1 = coords0.x;
        pinch_y1 = coords0.y;
        pinch_x2 = coords1.x;
        pinch_y2 = coords1.y;
      }

      e.preventDefault();
      e.stopPropagation();
    }, true);

    container.addEventListener('keydown', function(e) {
      if (e.ctrlKey || e.altKey || e.metaKey)
        return;

      var VK_I = 0x49,
          VK_J = 0x4A,
          VK_K = 0x4B,
          VK_L = 0x4C,
          VK_LEFT = 0x25,
          VK_UP = 0x26,
          VK_RIGHT = 0x27,
          VK_DOWN = 0x28,
          VK_SUBTRACT = ('DOM_VK_HYPHEN_MINUS' in e) ? e.DOM_VK_HYPHEN_MINUS : 0xBD,
          VK_EQUALS = ('DOM_VK_EQUALS' in e) ? e.DOM_VK_EQUALS : 0xBB;

      switch (e.keyCode) {
        case VK_UP:
        case VK_I: self.Scroll(0, -KEY_SCROLL_DELTA); break;
        case VK_LEFT:
        case VK_J: self.Scroll(-KEY_SCROLL_DELTA, 0); break;
        case VK_DOWN:
        case VK_K: self.Scroll(0, KEY_SCROLL_DELTA); break;
        case VK_RIGHT:
        case VK_L: self.Scroll(KEY_SCROLL_DELTA, 0); break;
        case VK_SUBTRACT: self.ZoomOut(); break;
        case VK_EQUALS: self.ZoomIn(); break;
        default: return;
      }

      e.preventDefault();
      e.stopPropagation();
    });

    self.invalidate();

    if (window == window.top) // == for IE
      container.focus();
  }

  // ======================================================================
  // Private Methods
  // ======================================================================

  Map.prototype.offset = function(dx, dy) {
    if (dx === 0 && dy === 0)
      return;

    var f = pow2(1 - this.scale) / this.tilesize;

    this.x = this.x + dx * f;
    this.y = this.y + dy * f;
    this.invalidate();
    fireEvent(this, 'DisplayChanged');
  };

  Map.prototype.setScale = function(newscale, px, py) {
    var cw = this.container.offsetWidth,
        ch = this.container.offsetHeight;

    newscale = Math.max(Math.min(newscale, this.max_scale), this.min_scale);
    if (newscale !== this.scale) {
      // Mathmagic to preserve hover coordinates
      var f, hx, hy;
      if (arguments.length >= 3) {
        f = pow2(1 - this.scale) / this.tilesize;
        hx = this.x + (px - cw / 2) * f;
        hy = this.y + (py - ch / 2) * f;
      }

      this.scale = newscale;

      if (arguments.length >= 3) {
        f = pow2(1 - this.scale) / this.tilesize;
        this.x = hx - (px - cw / 2) * f;
        this.y = hy - (py - ch / 2) * f;
        fireEvent(this, 'DisplayChanged');
      }

      this.invalidate();
      fireEvent(this, 'ScaleChanged', this.GetScale());
    }
  };

  Map.prototype.invalidate = function() {
    this.dirty = true;
    var self = this;
    if (!self._raf_handle) {
      self._raf_handle = requestAnimationFrame(function () {
        self._raf_handle = null;
        self.redraw();
      });
    }
  };

  Map.prototype.redraw = function(force) {
    if (!this.dirty && !force)
      return;

    this.dirty = false;

    var self = this,

    // Integral scale (the tiles that will be used)
        tscale = Math.round(this.scale),

    // How the tiles themselves are scaled (naturally 1, unless pinched)
        tmult = pow2(this.scale - tscale),

    // From map space to tile space
    // (Traveller map coords change at each integral zoom level)
        cf = pow2(tscale - 1), // Coordinate factor (integral)

    // Compute edges in tile space
        cw = this.container.offsetWidth,
        ch = this.container.offsetHeight,

        l = this.x * cf - (cw / 2) / (this.tilesize * tmult),
        r = this.x * cf + (cw / 2) / (this.tilesize * tmult),
        t = this.y * cf - (ch / 2) / (this.tilesize * tmult),
        b = this.y * cf + (ch / 2) / (this.tilesize * tmult),

    // Initial z - leave room for lower/higher scale tiles
        z = 10 + this.max_scale - this.min_scale,
        child, next;

    if (this.canvas) {
      var pw = (cw * 2) | 0;
      var ph = (ch * 2) | 0;
      if (this.canvas.width !== pw && this.canvas.height !== ph) {
        this.canvas.width = pw;
        this.canvas.height = ph;
        this.canvas.offset_x = -(cw / 2);
        this.canvas.offset_y = -ch;
        this.canvas.style.width = cw * 2;
        this.canvas.style.height = ch * 2;
        this.canvas.style.left = this.canvas.offset_x + 'px';
        this.canvas.style.top = this.canvas.offset_y + 'px';
      }
    }

    // Quantize to bounding tiles
    l = Math.floor(l) - 1;
    t = Math.floor(t) - 1;
    r = Math.floor(r) + 1;
    b = Math.floor(b) + 1;

    // Add extra around l/t/r edges for "tilt" effect
    l -= 1;
    t -= 2;
    r += 1;

    // Mark used tiles with this
    this.pass = (this.pass + 1) % 256;

    if (!this._rd_cb)
      this._rd_cb = function() { self.invalidate(); };

    var tileCount = (r - l + 1) * (b - t + 1);
    this.cache.ensureCapacity(tileCount * 2);

    // TODO: Defer loading of new tiles while in the middle of a zoom gesture
    // Draw a rectanglular area of the map in a spiral from the center of the requested map outwards
    this.drawRectangle(l, t, r, b, tscale, tmult, ch, cw, cf, z, this._rd_cb);

    // Hide unused tiles
    child = this.container.firstChild;
    while (child) {
      next = child.nextSibling;
      if (child.tagName === 'IMG' && child.pass !== this.pass)
        this.container.removeChild(child);
      child = next;
    }

    // Reposition markers and overlays
    var i;
    for (i = 0; i < this.markers.length; i += 1)
      this.makeMarker(this.markers[i]);

    for (i = 0; i < this.overlays.length; i += 1)
      this.makeOverlay(this.overlays[i]);
  };

  // Draw a rectangle (x1, y1) to (x2, y2) (or,  (l,t) to (r,b))
  // Recursive. Base Cases are: single tile or vertical|horizontal line
  // Decreasingly find the next-smaller rectangle to draw, then start drawing outward from the smallest rect to draw
  Map.prototype.drawRectangle = function(x1, y1, x2, y2, scale, mult, ch, cw, cf, zIndex, callback) {
    var self = this;

    var sizeMult = this.tilesize * mult;

    var dw = sizeMult;
    var dh = sizeMult;

    if ((x2 - x1) < 2 || (y2 - y1) < 2) {
      // Base case
      fill(x1, y1, x2, y2);
    } else {
      // Recurse - draw inner rectangle 1 dimension smaller.
      this.drawRectangle(x1 + 1, y1 + 1, x2 - 1, y2 - 1, scale, mult, ch, cw, cf, zIndex, callback);

      // Now draw the perimeter of our own rect.
      fill(x1, y1, x2, y1);
      fill(x1, y2, x2, y2);
      fill(x1, y1 + 1, x1, y2 - 1);
      fill(x2, y1 + 1, x2, y2 - 1);
    }

    function fill(x1, y1, x2, y2) {
      for (var x = x1; x <= x2; ++x) {
        for (var y = y1; y <= y2; ++y) {
          var dx = (x - self.x * cf) * self.tilesize * mult + (cw / 2);
          var dy = (y - self.y * cf) * self.tilesize * mult + (ch / 2);
          self.drawTile(x, y, scale, dx, dy, dw, dh, zIndex, callback);
        }
      }
    }
  };

  //
  // Draw the specified tile (scale, x, y) into the rectangle (dx, dy, dw, dh);
  // if the tile is not available it is requested, and higher/lower rez tiles
  // are used to fill in the gap until it loads. When loaded, the callback
  // is called (which should redraw the whole map).
  //
  Map.prototype.drawTile = function(x, y, scale, dx, dy, dw, dh, zIndex, callback) {
    var self = this; // for closures

    function drawImage(img, x, y, w, h, z) {
      if (self.ctx) {
        x -= self.canvas.offset_x;
        y -= self.canvas.offset_y;
        self.ctx.drawImage(img, Math.round(x), Math.round(y), w, h);
        return;
      }

      if (img.parentNode !== self.container) self.container.appendChild(img);

      img.style.left = Math.floor(x) + 'px';
      img.style.top = Math.floor(y) + 'px';
      img.style.width = Math.ceil(w) + 'px';
      img.style.height = Math.ceil(h) + 'px';

      img.style.zIndex = z;
      img.pass = self.pass;
    }

    var img = this.getTile(x, y, scale, callback);

    if (img) {
      drawImage(img, dx, dy, dw, dh, zIndex);
      return;
    }

    // Otherwise, while we're waiting, see if we have upscale/downscale versions to draw instead

    function drawLower(x, y, scale, dx, dy, dw, dh, zIndex) {
      if (scale <= self.min_scale)
        return;

      var tscale = scale - 1;
      var factor = pow2(scale - tscale);

      var tx = Math.floor(x / factor);
      var ty = Math.floor(y / factor);

      var ax = dx - dw * (x - (tx * factor));
      var ay = dy - dh * (y - (ty * factor));
      var aw = dw * factor;
      var ah = dh * factor;

      var img = self.getTile(tx, ty, tscale);
      if (img)
        drawImage(img, ax, ay, aw, ah, zIndex);
      else
        drawLower(tx, ty, tscale, ax, ay, aw, ah, zIndex - 1);
    }
    drawLower(x, y, scale, dx, dy, dw, dh, zIndex - 1);

    function drawHigher(x, y, scale, dx, dy, dw, dh, zIndex) {
      if (scale >= self.max_scale)
        return;

      var tscale = scale + 1;
      var factor = pow2(scale - tscale);

      for (var oy = 0; oy < 2; oy += 1) {
        for (var ox = 0; ox < 2; ox += 1) {

          var tx = (x / factor) + ox;
          var ty = (y / factor) + oy;
          var img = self.getTile(tx, ty, tscale);

          var ax = dx + ox * dw * factor;
          var ay = dy + oy * dh * factor;
          var aw = dw * factor;
          var ah = dh * factor;

          if (img)
            drawImage(img, ax, ay, aw, ah, zIndex);
          // NOTE:  Don't recurse if not found as it would try an exponential number of tiles
          // e.g. drawHigher(tx, ty, tscale, ax, ay, aw, ah, zIndex + 1);
        }
      }
    }
    drawHigher(x, y, scale, dx, dy, dw, dh, zIndex + 1);
  };


  //
  // Looks in the tile cache for the specified tile. If found, it is
  // returned immediately. If not found and a callback is specified,
  // the image is requested and the callback is called with the image
  // once it has successfully loaded.
  //
  Map.prototype.getTile = function(x, y, scale, callback) {
    if ('onLine' in navigator && !navigator.onLine)
      return undefined;

    var params = {x: x, y: y, scale: pow2(scale - 1), options: this.options, style: this.style};
    Object.keys(this.tileOptions).forEach(function (key) {
      params[key] = this.tileOptions[key];
    }, this);

    if ('devicePixelRatio' in window && window.devicePixelRatio > 1)
      params.dpr = window.devicePixelRatio;

    var url = Util.makeURL(SERVICE_BASE + '/api/tile', params);

    // Have it? Great, get out fast!
    var img = this.cache.fetch(url);
    if (img)
      return img;

    // Load if missing?
    if (!callback)
      return undefined;

    // In progress?
    if (this.loading[url])
      return undefined;


    if (this.defer_loading)
      return undefined;

    // Nope, better try loading it
    this.loading[url] = true;
    var self = this; // for event handler closures
    img = document.createElement('img');
    img.onload = function() {
      delete self.loading[url];
      self.cache.insert(url, img);
      callback(img);
      img.onload = null;
      img.onerror = null;
    };
    img.onerror = function() {
      delete self.loading[url];
      img.onload = null;
      img.onerror = null;
    };
    img.className = 'tile';
    img.src = url;
    img.style.position = 'absolute';

    return undefined;
  };

  Map.prototype.shouldAnimateToSectorHex = function(scale, sx, sy, hx, hy) {
    // TODO: Allow scale changes if target is "visible" (zooming in)
    if (scale !== this.scale)
      return false;

    var target = sectorHexToLogical(sx, sy, hx, hy),
        dx = target.x - this.GetX(),
        dy = target.y - this.GetY();

    var THRESHOLD = 2 * Astrometrics.SectorHeight;
    return Math.sqrt(dx * dx + dy * dy) < THRESHOLD;
  };

  Map.prototype.cancelAnimation = function() {
    if (this.animation) {
      this.animation.cancel();
      this.animation = null;
    }
  };

  Map.prototype.animateToSectorHex = function(scale, sx, sy, hx, hy) {
    this.cancelAnimation();
    var target = sectorHexToLogical(sx, sy, hx, hy),
        os = this.GetScale(),
        ox = this.GetX(),
        oy = this.GetY(),
        ts = scale,
        tx = target.x,
        ty = target.y;
    if (ox === tx && oy === ty && os === ts)
      return;

    this.animation = new Animation(3.0, function(p) {
      return Animation.smooth(p, 1.0, 0.1, 0.25);
    });
    var self = this;
    this.animation.onanimate = function(p) {
      // Interpolate scale in log space.
      self.SetScale(Math.pow(2, Animation.interpolate(
        Math.log(os)/Math.LN2, Math.log(ts)/Math.LN2, p)));
      // TODO: If animating scale, this should follow an arc (parabola?) through 3space treating
      // scale as Z and computing a height such that the target is in view at the turnaround.
      self.SetPosition(Animation.interpolate(ox, tx, p), Animation.interpolate(oy, ty, p));
      self.redraw();
    };
  };

  Map.prototype.makeOverlay = function(overlay) {
    if (overlay === null)
      return;

    var div;
    if (overlay.element && overlay.element.parentNode) {
      overlay.element.parentNode.removeChild(overlay.element);
      div = overlay.element;
    } else {
      div = document.createElement('div');
      overlay.element = div;
    }

    // Compute physical location
    var pt1 = this.logicalToPixel(overlay.x, overlay.y);
    var pt2 = this.logicalToPixel(overlay.x + overlay.w, overlay.y + overlay.h);

    div.className = 'overlay';
    div.id = overlay.id;

    div.style.left = String(pt1.x) + 'px';
    div.style.top = String(pt1.y) + 'px';
    div.style.width = String(pt2.x - pt1.x) + 'px';
    div.style.height = String(pt1.y - pt2.y) + 'px';
    div.style.zIndex = overlay.z;

    overlay.element = div;
    this.container.appendChild(div);
  };

  Map.prototype.makeMarker = function(marker) {
    if (marker === null)
      return;

    var div;
    if (marker.element && marker.element.parentNode) {
      marker.element.parentNode.removeChild(marker.element);
      div = marker.element;
    } else {
      div = document.createElement('div');
      marker.element = div;
      if (marker.url) {
        var img = document.createElement('img');
        img.src = marker.url;
        div.appendChild(img);
      }
    }

    var pt = sectorHexToLogical(marker.sx, marker.sy, marker.hx, marker.hy);
    pt = this.logicalToPixel(pt.x, pt.y);

    div.className = 'marker';
    div.id = marker.id;

    div.style.left = String(pt.x) + 'px';
    div.style.top = String(pt.y) + 'px';
    div.style.zIndex = String(marker.z);

    marker.element = div;
    this.container.appendChild(div);
  };

  Map.prototype.logicalToPixel = function(lx, ly) {
    var f = pow2(1 - this.scale) / this.tilesize;
    return {
      x: ((lx / this.tilesize - this.x) / f) + this.container.offsetWidth / 2,
      y: ((ly / -this.tilesize - this.y) / f) + this.container.offsetHeight / 2
    };
  };

  // ======================================================================
  // Public API
  // ======================================================================

  Map.prototype.GetHexX = function() {
    return logicalToHex(this.GetX(), this.GetY()).hx;
  };

  Map.prototype.GetHexY = function() {
    return logicalToHex(this.GetX(), this.GetY()).hy;
  };

  Map.prototype.GetScale = function() {
    return pow2(this.scale - 1);
  };

  Map.prototype.SetScale = function(scale) {
    scale = 1 + log2(Number(scale));
    if (scale === this.scale)
      return;
    this.setScale(scale);
  };

  Map.prototype.GetOptions = function() {
    return this.options;
  };

  Map.prototype.SetOptions = function(options) {
    if (LEGACY_STYLES) {
      // Handle legacy styles specified in options bits
      if ((options & MapOptions.StyleMaskDeprecated) === MapOptions.PrintStyleDeprecated)
        this.SetStyle('atlas');
      else if ((options & MapOptions.StyleMaskDeprecated) === MapOptions.CandyStyleDeprecated)
        this.SetStyle('candy');
      options = options & ~MapOptions.StyleMaskDeprecated;
    }

    if (options === this.options)
      return;

    this.options = options & MapOptions.Mask;
    this.cache.clear();
    this.invalidate();
    fireEvent(this, 'OptionsChanged', this.options);
  };

  Map.prototype.SetNamedOption = function(name, value) {
    this.tileOptions[name] = value;
    this.cache.clear();
    this.invalidate();
    fireEvent(this, 'OptionsChanged', this.options);
  };
  Map.prototype.GetNamedOption = function(name) {
    return this.tileOptions[name];
  };
  Map.prototype.ClearNamedOption = function(name) {
    delete this.tileOptions[name];
    this.cache.clear();
    this.invalidate();
    fireEvent(this, 'OptionsChanged', this.options);
  };
  Map.prototype.GetNamedOptionNames = function() {
    return Object.keys(this.tileOptions);
  };

  Map.prototype.GetStyle = function() {
    return this.style;
  };

  Map.prototype.SetStyle = function(style) {
    if (style === this.style)
      return;

    this.style = style;
    this.cache.clear();
    fireEvent(this, 'StyleChanged', this.style);
    this.invalidate();
  };

  Map.prototype.GetX = function() {
    return this.x * this.tilesize;
  };

  Map.prototype.GetY = function() {
    return this.y * -this.tilesize;
  };

  Map.prototype.SetPosition = function(x, y) {
    x /= this.tilesize;
    y /= -this.tilesize;
    if (x === this.x && y === this.y) {
      return;
    }
    this.x = x;
    this.y = y;
    fireEvent(this, 'DisplayChanged');
    this.invalidate();
  };


  // This places the specified Sector, Hex coordinates (parsec)
  // at the center of the viewport, with a specific scale.
  Map.prototype.ScaleCenterAtSectorHex = function(scale, sx, sy, hx, hy) {
    this.cancelAnimation();

    if (this.shouldAnimateToSectorHex(1 + log2(Number(scale)), sx, sy, hx, hy)) {
      this.animateToSectorHex(scale, sx, sy, hx, hy);
    } else {
      this.SetScale(scale);
      this.CenterAtSectorHex(sx, sy, hx, hy);
    }
  };


  // This places the specified Sector, Hex coordinates (parsec)
  // at the center of the viewport
  Map.prototype.CenterAtSectorHex = function(sx, sy, hx, hy) {
    var target = sectorHexToLogical(sx, sy, hx, hy);
    this.SetPosition(target.x, target.y);
  };


  // Scroll the map view by the specified dx/dy (in pixels)
  Map.prototype.Scroll = function(dx, dy, fAnimate) {
    if (!fAnimate) {
      this.offset(dx, dy);
      return;
    }

    this.cancelAnimation();
    var f = pow2(1 - this.scale) / this.tilesize,
        ox = this.x,
        oy = this.y,
        tx = ox + dx * f,
        ty = oy + dy * f;

    this.animation = new Animation(1.0, function(p) {
      return Animation.smooth(p, 1.0, 0.1, 0.25);
    });
    var self = this;
    this.animation.onanimate = function(p) {
      self.x = Animation.interpolate(ox, tx, p);
      self.y = Animation.interpolate(oy, ty, p);
      self.redraw(true);
      fireEvent(self, 'DisplayChanged');
    };
  };

  var ZOOM_DELTA = 0.5;
  function roundScale(s) {
    return Math.round(s / ZOOM_DELTA) * ZOOM_DELTA;
  }

  Map.prototype.ZoomIn = function() {
    this.setScale(roundScale(this.scale) + ZOOM_DELTA);
  };

  Map.prototype.ZoomOut = function() {
    this.setScale(roundScale(this.scale) - ZOOM_DELTA);
  };


  // NOTE: This API is subject to change
  Map.prototype.AddMarker = function(id, sx, sy, hx, hy, opt_url) {
    var marker = {
      sx: sx,
      sy: sy,
      hx: hx,
      hy: hy,

      id: id,
      url: opt_url,
      z: 909
    };

    this.markers.push(marker);
    this.makeMarker(marker);
  };


  Map.prototype.AddOverlay = function(x, y, w, h) {
    // TODO: Take id, like AddMarker
    var overlay = {
      x: x,
      y: y,
      w: w,
      h: h,

      id: 'overlay',
      z: 910
    };

    this.overlays.push(overlay);
    this.makeOverlay(overlay);
  };

  Map.prototype.ApplyURLParameters = function() {
    var self = this;
    var params = Util.parseURLQuery(document.location);

    function float(prop) {
      var n = parseFloat(params[prop]);
      return isNaN(n) ? 0 : n;
    }

    function int(prop) {
      var n = parseInt(params[prop], 10);
      return isNaN(n) ? 0 : n;
    }

    function has(params, list) {
      return list.every(function(item) { return item in params; });
    }

    if ('scale' in params)
      this.SetScale(float('scale'));

    if ('options' in params)
      this.SetOptions(int('options'));

    if ('style' in params)
      this.SetStyle(params.style);

    if (has(params, ['yah_sx', 'yah_sy', 'yah_hx', 'yah_hx']))
      this.AddMarker('you_are_here', int('yah_sx'), int('yah_sy'), int('yah_hx'), int('yah_hy'));

    if (has(params, ['yah_sector'])) {
      MapService.coordinates(
        params.yah_sector, params.yah_hex,
        function(location) {
          if (!(location.hx && location.hy)) {
            location.hx = Astrometrics.SectorWidth / 2;
            location.hy = Astrometrics.SectorHeight / 2;
          }
          self.AddMarker('you_are_here', location.sx, location.sy, location.hx, location.hy);
        },
        function() {
          alert('The requested marker location "' + params.yah_sector + ('yah_hex' in params ? (' ' + params.yah_hex) : '') + '" was not found.');
        });
    }
    if (has(params, ['marker_sector', 'marker_url'])) {
      MapService.coordinates(
        params.marker_sector, params.marker_hex,
        function(location) {
          if (!(location.hx && location.hy)) {
            location.hx = Astrometrics.SectorWidth / 2;
            location.hy = Astrometrics.SectorHeight / 2;
          }
          self.AddMarker('custom', location.sx, location.sy, location.hx, location.hy, params.marker_url);
        },
        function() {
          alert('The requested marker location "' + params.marker_sector + ('marker_hex' in params ? (' ' + params.marker_hex) : '') + '" was not found.');
        });
    }

    for (var i = 0; ; ++i) {
      var n = (i === 0) ? '' : i, oxs = 'ox' + n, oys = 'oy' + n, ows = 'ow' + n, ohs = 'oh' + n;
      if (has(params, [oxs, oys, ows, ohs])) {
        var x = float(oxs);
        var y = float(oys);
        var w = float(ows);
        var h = float(ohs);
        this.AddOverlay(x, y, w, h);
      } else {
        break;
      }
    }

    // Various coordinate schemes - ordered by priority
    if (has(params, ['x', 'y'])) {
      this.SetPosition(float('x'), float('y'));
    } else if (has(params, ['sx', 'sy', 'hx', 'hy', 'scale'])) {
      this.ScaleCenterAtSectorHex(
        float('scale'), float('sx'), float('sy'), float('hx'), float('hy'));
    } else if ('sector' in params) {
      MapService.coordinates(
        params.sector, params.hex,
        function(location) {
          if (location.hx && location.hy) { // NOTE: Test for undefined -or- zero
            self.ScaleCenterAtSectorHex(64, location.sx, location.sy, location.hx, location.hy);
          } else {
            self.ScaleCenterAtSectorHex(16, location.sx, location.sy, Astrometrics.SectorWidth / 2, Astrometrics.SectorHeight / 2);
          }
        },
        function() {
          alert('The requested location "' + params.sector + ('hex' in params ? (' ' + params.hex) : '') + '" was not found.');
        });
    }

    ['silly', 'routes', 'dimunofficial'].forEach(function(name) {
      if (name in params)
        self.tileOptions[name] = int(name);
    });

    return params;
  };

  //----------------------------------------------------------------------
  // Exports
  //----------------------------------------------------------------------

  global.Traveller = {
    SERVICE_BASE: SERVICE_BASE,
    LEGACY_STYLES: LEGACY_STYLES,
    Astrometrics: Astrometrics,
    Map: Map,
    MapOptions: MapOptions,
    Styles: Styles,
    MapService: MapService,
    fromHex: fromHex
  };

}(this));
