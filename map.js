// ======================================================================
// Exported Functionality
// ======================================================================

var SERVICE_BASE = (window.location.hostname.indexOf("travellermap.com") !== -1) ? "" :
      (window.location.hostname === "localhost") ? "" : "http://travellermap.com/";
var LEGACY_STYLES = true;

(function (global) {
  "use strict";

  //----------------------------------------------------------------------
  // General Traveller stuff
  //----------------------------------------------------------------------

  global.Traveller = {
    fromHex: function(c) {
      return "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ".indexOf(c.toUpperCase());
    }
  };

  //----------------------------------------------------------------------
  // Enumerated types
  //----------------------------------------------------------------------

  global.MapOptions = {
    SectorGrid: 0x0001,
    SubsectorGrid: 0x0002,
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

  global.Styles = {
    Poster: "poster",
    Atlas: "atlas",
    Print: "print",
    Candy: "candy"
  };

  //----------------------------------------------------------------------
  // Astrometric Constants
  //----------------------------------------------------------------------

  global.Astrometrics = {
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

  function service(url, contentType, callback, errback) {
    if (typeof callback !== 'function') { throw new TypeError(); }

    var xhr = new XMLHttpRequest();
    try {
      xhr.open('GET', url, true);
      // Due to proxies/user-agents not respecting Vary tags, switch to URL params instead of headers.
      xhr.setRequestHeader("Accept", contentType);
      xhr.onreadystatechange = function() {
        if (xhr.readyState === XMLHttpRequest.DONE) {
          try {
            if (xhr.status === 0) {
              return; // aborted, no callback
            }
          } catch (e) {
            // IE9 throws if xhr.status accessed after an explicit abort() call
            return;
          }
          if (xhr.status === 200) {
            callback(contentType === "application/json" ? JSON.parse(xhr.responseText) : xhr.responseText);
          } else {
            if (errback) {
              errback(xhr.status);
            }
            else {
              callback(xhr.responseText);
            }
          }
        }
      };
      xhr.send();
      return xhr;
    } catch (ex) {
      // If cross-domain, blocked by browsers that don't implement CORS
      setTimeout(function () {
        if (errback) { errback(ex.message); } else { callback(null); }
      }, 0);
      return {
        abort: function() {},
        readyState: XMLHttpRequest.DONE,
        status: 0,
        statusText: "Forbidden",
        responseText: "Connection error"
      };
    }
  }

  // TODO: Make these Futures (or at least callback/errback)
  global.MapService = {
    coordinates: function(sector, hex, callback, errback) {
      return service(
        SERVICE_BASE + '/api/coordinates?sector=' + encodeURIComponent(sector) + (hex ? '&hex=' + encodeURIComponent(hex) : ''),
        'application/json', callback, errback);
    },

    credits: function (hexX, hexY, callback, errback) {
      return service(SERVICE_BASE + '/api/credits?x=' + encodeURIComponent(hexX) + '&y=' + encodeURIComponent(hexY),
      'application/json', callback, errback);
    },

    search: function (query, callback, errback) {
      return service(SERVICE_BASE + '/api/search?q=' + encodeURIComponent(query),
        'application/json', callback, errback);
    },

    sectorData: function (sector, callback, errback) {
      return service(SERVICE_BASE + '/api/sec?sector=' + encodeURIComponent(sector),
        'text/plain', callback, errback);
    },

    sectorDataTabDelimited: function (sector, callback, errback) {
      return service(SERVICE_BASE + '/api/sec?sector=' + encodeURIComponent(sector) + '&type=TabDelimited',
        'text/plain', callback, errback);
    },

    sectorMetaData: function (sector, callback, errback) {
      return service(SERVICE_BASE + '/api/metadata?sector=' + encodeURIComponent(sector),
        'application/json', callback, errback);
    },

    universe: function (callback, errback) {
      return service(SERVICE_BASE + '/api/universe',
        'application/json', callback, errback);
    }
  };

  // ======================================================================
  // Least-Recently-Used Cache
  // ======================================================================

  global.LRUCache = function(capacity) {
    this.capacity = capacity;
    this.cache = {};
    this.queue = [];

    this.clear = function() {
      this.cache = [];
      this.queue = [];
    };

    this.fetch = function(key) {
      var value = this.cache[key];
      if (typeof value === 'undefined') {
        return (void 0); // undefined
      }

      var index = this.queue.indexOf(key);
      if (index !== -1) {
        this.queue.splice(index, 1);
      }
      this.queue.push(key);
      return value;
    };

    this.insert = function(key, value) {
      // Remove previous instances
      var index = this.queue.indexOf(key);
      if (index !== -1) {
        this.queue.splice(index, 1);
      }

      this.cache[key] = value;
      this.queue.push(key);

      while (this.queue.length > this.capacity) {
        key = this.queue.shift();
        delete this.cache[key];
      }
    };
  };

  global.Defaults = {
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
  // Static functions to normalize behavior across event models
  // ======================================================================

  global.DOMHelpers = {
    setCapture: function(element) {
      if (element.setCapture) {
        element.setCapture(true);
      }
    },
    focus: function(element) {
      if (element.focus) {
        element.focus();
      }
    },
    releaseCapture: function(element) {
      if (element.releaseCapture) {
        element.releaseCapture();
      }
    },
    globalToLocal: function(x, y, element) {
      var rect = element.getBoundingClientRect();
      x -= rect.left;
      y -= rect.top;
      return { x: x, y: y };
    }
  };


  // ======================================================================
  // Animation Utilities
  // ======================================================================

  function isCallable(o) {
    return typeof o === 'function';
  }

  //
  // dur = total duration (seconds)
  // tick (UNUSED)
  // smooth = optional smoothing function
  // set onanimate to function called with animation position (0.0 ... 1.0)
  //
  global.Animation = function(dur, tick, smooth) {
    var start = Date.now();
    var self = this;
    this.timerid = requestAnimationFrame(tickFunc);

    function tickFunc() {
      var f = (Date.now() - start) / 1000 / dur;

      var p = f;
      if (isCallable(smooth)) {
        p = smooth(p);
      }

      if (isCallable(self.onanimate)) {
        self.onanimate(p);
      }

      // Next tick
      if (f >= 1.0) {
        if (isCallable(self.oncomplete)) {
          self.oncomplete();
        }
      } else {
        requestAnimationFrame(tickFunc);
      }

    }
  };

  Animation.prototype.cancel = function() {
    if (this.timerid) {
      cancelAnimationFrame(this.timerid);
      if (isCallable(this.oncancel)) {
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
    }
    else if (t <= (dur - ddec)) {
      return r * (t - dacc / 2);
    }
    else {
      tdec = t - (dur - ddec);
      pd = tdec / ddec;

      return r * (dur - dacc / 2 - ddec + tdec * (2 - pd) / 2);
    }
  };

}(this));


// TODO: Put this in a namespace
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

// TODO: Put this in a namespace
function logicalToHex(x, y) {
  var hx = Math.round((x / Astrometrics.ParsecScaleX) + 0.5);
  var hy = Math.round((-y / Astrometrics.ParsecScaleY) + ((hx % 2 === 0) ? 0.5 : 0));
  return {hx: hx, hy: hy};
}

// TODO: Put this in a namespace
function fireEvent(target, event, data) {
  if (typeof target["On" + event] === 'function') {
    try {
      target["On" + event](data);
    } catch (ex) {
      if (console && console.error) {
        console.error("Event handler for " + event + " threw:", ex);
      }
    }
  }
}

function getUrlParameters() {
  "use strict";
  var o = {};
  if (document.location.search && document.location.search.length > 1) {
    document.location.search.substring(1).split('&').forEach(function(pair) {
      var kv = pair.split('=', 2);
      if (kv.length === 2) {
        o[kv[0]] = decodeURIComponent(kv[1].replace(/\+/g, ' '));
      } else {
        o[kv[0]] = true;
      }
    });
  }
  return o;
}

function escapeHtml(s) {
  "use strict";
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function applyUrlParameters(map) {
  var params = getUrlParameters();

  function asFloat(prop) {
    var n = parseFloat(params[prop]);
    return isNaN(n) ? 0 : n;
  }

  function asInt(prop) {
    var n = parseInt(params[prop], 10);
    return isNaN(n) ? 0 : n;
  }

  if ("scale" in params) {
    map.SetScale(asFloat("scale"));
  }

  if ("options" in params) {
    map.SetOptions(asInt("options"));
  }

  if ("style" in params) {
    map.SetStyle(params.style);
  }

  if ("x" in params && "y" in params) {
    map.SetPosition(asFloat("x"), asFloat("y"));
  }

  if ("yah_sx" in params && "yah_sy" in params && "yah_hx" in params && "yah_hx" in params) {
    map.TEMP_AddMarker("you_are_here", asInt("yah_sx"), asInt("yah_sy"), asInt("yah_hx"), asInt("yah_hy"));
  }

  for (var i = 0; ; ++i) {
    var suffix = (i == 0) ? "" : i, oxs = "ox" + suffix, oys = "oy" + suffix, ows = "ow" + suffix, ohs = "oh" + suffix;
    if (oxs in params && oys in params && ows in params && ohs in params) {
      var x = asFloat(oxs);
      var y = asFloat(oys);
      var w = asFloat(ows);
      var h = asFloat(ohs);
      map.TEMP_AddOverlay(x, y, w, h);
    } else {
      break;
    }
  }

  if ("sector" in params) {
    MapService.coordinates(params.sector, params.hex, function(location) {
      if (location.hx && location.hy) { // NOTE: Test for undefined -or- zero
        map.ScaleCenterAtSectorHex(64, location.sx, location.sy, location.hx, location.hy);
      } else {
        map.ScaleCenterAtSectorHex(16, location.sx, location.sy, Astrometrics.SectorWidth / 2, Astrometrics.SectorHeight / 2);
      }
    }, function(error) {
      alert("The requested location \"" + params.sector + ("hex" in params ? (" " + params.hex) : "") + "\" was not found.");
    });
  }

  if ("silly" in params) {
    map.tileOptions["silly"] = asInt("silly");
  }

  return params;
}



//----------------------------------------------------------------------
//
// Usage:
//
//   var map = new Map( document.getElementById("YourMapDiv") );
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
// Experimental APIs - may change at any time:
//   map.TEMP_AddMarker(id, sx, sy, hx, hy); // should have CSS style for .marker#<id>
//   map.TEMP_AddOverlay(x, y, w, h); // should have CSS style for .overlay
//  
//----------------------------------------------------------------------

var Map;

(function(global) {
  "use strict";

  // ======================================================================
  // Slippy Map using Tiles
  // ======================================================================

  function log2(v) { return Math.log(v) / Math.LN2; }
  function pow2(v) { return Math.pow(2, v); }

  var Map = function(container) {

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

    this.cache = new LRUCache(128); // TODO: ensure enough to fill screen

    this.loading = {};
    this.pass = 0;

    this.defer_loading = false;

    var CLICK_SCALE_DELTA = -0.5;
    var SCROLL_SCALE_DELTA = -0.15;
    var KEY_SCROLL_DELTA = 25;
    var KEY_ZOOM_DELTA = 0.5;

    container.style.position = 'relative';
    container.style.overflow = 'hidden';

    // Event target, so it doesn't change during refreshes
    var sink = document.createElement('div');
    sink.style.position = 'absolute';
    sink.style.left = 0;
    sink.style.top = 0;
    sink.style.right = 0;
    sink.style.bottom = 0;
    sink.style.zIndex = 1000;
    container.appendChild(sink);

    this.markers = [];
    this.overlays = [];

    // ======================================================================
    // Event Handlers
    // ======================================================================

    var dragging, drag_x, drag_y;
    container.addEventListener('mousedown', function(e) {
      self.cancelAnimation();
      DOMHelpers.focus(container);
      dragging = true;
      drag_x = e.clientX;
      drag_y = e.clientY;
      DOMHelpers.setCapture(container);
      container.classList.add('dragging');

      e.preventDefault();
      e.stopPropagation();
    }, true);

    var hover_x, hover_y;
    container.addEventListener('mousemove', function(e) {
      if (dragging) {
        var dx = drag_x - e.clientX;
        var dy = drag_y - e.clientY;

        self.offset(dx, dy);

        drag_x = e.clientX;
        drag_y = e.clientY;
        e.preventDefault();
        e.stopPropagation();
      }

      // Compute the physical coordinates
      var f = pow2(1 - self.scale) / self.tilesize,
          coords = DOMHelpers.globalToLocal(e.clientX, e.clientY, container),
          rect = self.container.getBoundingClientRect(),
          cx = self.x + f * (coords.x - rect.width / 2),
          cy = self.y + f * (coords.y - rect.height / 2),
          hex = logicalToHex(cx * self.tilesize, cy * -self.tilesize);

      // Throttle the events
      if (hover_x !== hex.hx || hover_y !== hex.hy) {
        hover_x = hex.hx;
        hover_y = hex.hy;
        fireEvent(self, "Hover", { x: hex.hx, y: hex.hy });
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

      // Compute the physical coordinates
      var f = pow2(1 - self.scale) / self.tilesize,
          coords = DOMHelpers.globalToLocal(e.clientX, e.clientY, container),
          rect = self.container.getBoundingClientRect(),
          cx = self.x + f * (coords.x - rect.width / 2),
          cy = self.y + f * (coords.y - rect.height / 2),
          hex = logicalToHex(cx * self.tilesize, cy * -self.tilesize);

      fireEvent(self, "Click", { x: hex.hx, y: hex.hy });
    });

    container.addEventListener('dblclick', function(e) {
      self.cancelAnimation();

      e.preventDefault();
      e.stopPropagation();

      var f, coords, rect, cx, cy, hex;

      var MAX_DOUBLECLICK_SCALE = 7;
      if (self.scale < MAX_DOUBLECLICK_SCALE) {
        var newscale = self.scale + CLICK_SCALE_DELTA * ((e.altKey) ? 1 : -1);
        newscale = Math.min(newscale, MAX_DOUBLECLICK_SCALE);

        coords = DOMHelpers.globalToLocal(e.clientX, e.clientY, container);
        self.setScale(newscale, coords.x, coords.y);

        // Compute the physical coordinates
        f = pow2(1 - self.scale) / self.tilesize;
        coords = DOMHelpers.globalToLocal(e.clientX, e.clientY, container);
        rect = self.container.getBoundingClientRect();
        cx = self.x + f * (coords.x - rect.width / 2);
        cy = self.y + f * (coords.y - rect.height / 2);
        hex = logicalToHex(cx * self.tilesize, cy * -self.tilesize);
      } else {
        // Compute the physical coordinates
        f = pow2(1 - self.scale) / self.tilesize;
        coords = DOMHelpers.globalToLocal(e.clientX, e.clientY, container);
        rect = self.container.getBoundingClientRect();
        cx = self.x + f * (coords.x - rect.width / 2);
        cy = self.y + f * (coords.y - rect.height / 2);
        hex = logicalToHex(cx * self.tilesize, cy * -self.tilesize);

        self.x = cx;
        self.y = cy;
        self.invalidate();
      }

      fireEvent(self, "DoubleClick", { x: hex.hx, y: hex.hy });
    });

    var wheelListener = function(e) {
      self.cancelAnimation();
      var delta = e.detail ? e.detail * -40 : e.wheelDelta;

      var newscale = self.scale + SCROLL_SCALE_DELTA * ((delta > 0) ? -1 : (delta < 0) ? 1 : 0);

      var coords = DOMHelpers.globalToLocal(e.clientX, e.clientY, container);
      self.setScale(newscale, coords.x, coords.y);

      e.preventDefault();
      e.stopPropagation();
    };
    container.addEventListener('mousewheel', wheelListener); // IE/Chrome/Safari/Opera
    container.addEventListener('DOMMouseScroll', wheelListener); // FF

    window.addEventListener('resize', function(e) {
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

        var coords = DOMHelpers.globalToLocal(e.touches[0].clientX, e.touches[0].clientY, container);
        var dx = touch_x - coords.x;
        var dy = touch_y - coords.y;

        self.offset(dx, dy);

        touch_x = coords.x;
        touch_y = coords.y;

      } else if (e.touches.length === 2) {

        var od = dist(pinch_x1, pinch_y1, pinch_x2, pinch_y2),
            ocx = (pinch_x1 + pinch_x2) / 2,
            ocy = (pinch_y1 + pinch_y2) / 2;

        var coords0 = DOMHelpers.globalToLocal(e.touches[0].clientX, e.touches[0].clientY, container),
            coords1 = DOMHelpers.globalToLocal(e.touches[1].clientX, e.touches[1].clientY, container);
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
        var coords = DOMHelpers.globalToLocal(e.touches[0].clientX, e.touches[0].clientY, container);
        touch_x = coords.x;
        touch_y = coords.y;
      }

      e.preventDefault();
      e.stopPropagation();
    }, true);

    container.addEventListener('touchstart', function(e) {
      if (e.touches.length === 1) {
        var coords = DOMHelpers.globalToLocal(e.touches[0].clientX, e.touches[0].clientY, container);
        touch_x = coords.x;
        touch_y = coords.y;
      } else if (e.touches.length === 2) {
        self.defer_loading = true;
        var coords0 = DOMHelpers.globalToLocal(e.touches[0].clientX, e.touches[0].clientY, container),
            coords1 = DOMHelpers.globalToLocal(e.touches[1].clientX, e.touches[1].clientY, container);
        pinch_x1 = coords0.x;
        pinch_y1 = coords0.y;
        pinch_x2 = coords1.x;
        pinch_y2 = coords1.y;
      }

      e.preventDefault();
      e.stopPropagation();
    }, true);

    container.addEventListener('keydown', function(e) {
      if (e.ctrlKey || e.altKey || e.metaKey) {
        return;
      }

      var isMoz = navigator.userAgent.indexOf('Gecko/') !== -1,
          VK_I = 73,
          VK_J = 74,
          VK_K = 75,
          VK_L = 76,
          VK_SUBTRACT = isMoz ? 109 : 189,
          VK_EQUALS = isMoz ? 61 : 187;

      switch (e.keyCode) {
        case VK_I: self.Scroll(0, -KEY_SCROLL_DELTA); break;
        case VK_J: self.Scroll(-KEY_SCROLL_DELTA, 0); break;
        case VK_K: self.Scroll(0, KEY_SCROLL_DELTA); break;
        case VK_L: self.Scroll(KEY_SCROLL_DELTA, 0); break;
        case VK_SUBTRACT: self.setScale(self.scale - KEY_ZOOM_DELTA); break;
        case VK_EQUALS: self.setScale(self.scale + KEY_ZOOM_DELTA); break;
        default: return;
      }

      e.preventDefault();
      e.stopPropagation();
    });

    self.invalidate();

    if (window === window.top) {
      DOMHelpers.focus(container);
    }
  };

  // ======================================================================
  // Private Methods
  // ======================================================================

  Map.prototype.offset = function(dx, dy) {
    if (dx !== 0 || dy !== 0) {
      var f = pow2(1 - this.scale) / this.tilesize;

      this.x = this.x + dx * f;
      this.y = this.y + dy * f;
      this.invalidate();
      fireEvent(this, "DisplayChanged");
    }
  };

  Map.prototype.setScale = function(newscale, px, py) {
    var rect = this.container.getBoundingClientRect(),
      cw = rect.width,
      ch = rect.height;

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
        fireEvent(this, "DisplayChanged");
      }

      this.invalidate();
      fireEvent(this, "ScaleChanged", this.GetScale());
    }
  };

  Map.prototype.invalidate = function (delay) {
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
    if (!this.dirty && !force) {
      return;
    }
    this.dirty = false;

    // *FUTURE: Spiral outwards so requests for central
    // images are serviced first

    var self = this,

    // Integral scale (the tiles that will be used)
        tscale = Math.round(this.scale),

    // How the tiles themselves are scaled (naturally 1, unless pinched)
        tmult = pow2(this.scale - tscale),

    // From map space to tile space
    // (Traveller map coords change at each integral zoom level)
        cf = pow2(tscale - 1), // Coordinate factor (integral)

    // Compute edges in tile space
        rect = this.container.getBoundingClientRect(),
        cw = rect.width,
        ch = rect.height,

        l = this.x * cf - (cw / 2) / (this.tilesize * tmult),
        r = this.x * cf + (cw / 2) / (this.tilesize * tmult),
        t = this.y * cf - (ch / 2) / (this.tilesize * tmult),
        b = this.y * cf + (ch / 2) / (this.tilesize * tmult),

    // Initial z - leave room for lower/higher scale tiles
        z = 10 + this.max_scale - this.min_scale,
        x, y, dx, dy, dw, dh,
        child, next;


    // Quantize to bounding tiles
    l = Math.floor(l) - 1;
    t = Math.floor(t) - 1;
    r = Math.floor(r) + 1;
    b = Math.floor(b) + 1;

    // Mark used tiles with this
    this.pass = (this.pass + 1) % 256;

    if (!this._rd_cb) {
      this._rd_cb = function() { self.invalidate(100); };
    }

    // TODO: Defer loading of new tiles while in the middle of a zoom gesture
    // Draw a rectanglular area of the map in a spiral from the center of the requested map outwards
    this.drawRectangle(l, t, r, b, tscale, tmult, ch, cw, cf, z, this._rb_cb);

    // Hide unused tiles
    child = this.container.firstChild;
    while (child) {
      next = child.nextSibling;
      if (child.tagName === 'IMG' && child.pass !== this.pass) {
        this.container.removeChild(child);
      }
      child = next;
    }

    // Reposition markers and overlays
    var i;
    for (i = 0; i < this.markers.length; i += 1) {
      this.makeMarker(this.markers[i]);
    }
    for (i = 0; i < this.overlays.length; i += 1) {
      this.makeOverlay(this.overlays[i]);
    }
  };

  // Draw a rectangle (x1, y1) to (x2, y2) (or,  (l,t) to (r,b))
  // Don't draw the corners twice :-) 
  // Recursive. Base Cases are: single tile or vertical|horizontal line
  // Decreasingly find the next-smaller rectangle to draw, then start drawing outward from the smallest rect to draw
  Map.prototype.drawRectangle = function (x1, y1, x2, y2, scale, mult, ch, cw, cf, zIndex, callback) {

    var sizeMult = this.tilesize * mult;
    var dx = (x1 - this.x * cf) * sizeMult;
    var dy = (y1 - this.y * cf) * sizeMult;
    var dw = sizeMult;
    var dh = sizeMult;

    // Catches base cases and handily catches very simple requests without recursion
    if ((x1 >= x2) && (y1 >= y2)) {
      // for base case of 0, just draw a single tile

      this.drawTile(x1, y1, scale, (cw / 2) + dx, (ch / 2) + dy, dw, dh, zIndex, callback);
    } else {
      if ((x1 >= x2) && (y1 != y2)) {
        // this is a vertical line

        this.drawVerticalLine(x1, y1, y2, scale, mult, ch, cf, (cw / 2) + dx, dw, dh, zIndex, callback);
      } else if ((x1 != x2) && (y1 >= y2)) {
        // this is a horizontal line

        this.drawHorizontalLine(x1, y1, x2, scale, mult, cw, cf, (ch / 2) + dy, dw, dh, zIndex, callback);
      } else {
        // this is a full rectangle

        // recurse. draw a rectangle 1 dimension smaller than what we are looking at now 
        // stop recursing when we hit a base case as mentioned above
        this.drawRectangle(x1 + 1, y1 + 1, x2 - 1, y2 - 1, scale, mult, ch, cw, cf, zIndex, callback);

        // we have drawn all smaller rectangles inside our own. Now draw the perimeter of our own rect.
        // draw across at the top
        this.drawHorizontalLine(x1, y1, x2, scale, mult, cw, cf, (ch / 2) + dy, dw, dh, zIndex, callback);

        // draw the left side 
        // y1 + 1 and y2 - 1 ensure we do not draw overlapping tiles
        this.drawVerticalLine(x1, y1 + 1, y2 - 1, scale, mult, ch, cf, (cw / 2) + dx, dw, dh, zIndex, callback);

        // draw across at the bottom
        dy = (y2 - this.y * cf) * sizeMult;
        this.drawHorizontalLine(x1, y2, x2, scale, mult, cw, cf, (ch / 2) + dy, dw, dh, zIndex, callback);

        // draw the right side
        // again with the non-overlapping tile math
        dx = (x2 - this.x * cf) * sizeMult;
        this.drawVerticalLine(x2, y1 + 1, y2 - 1, scale, mult, ch, cf, (cw / 2) + dx, dw, dh, zIndex, callback);
      }
    }
  }

  // Draw tiles from x1, y to x2, y
  Map.prototype.drawHorizontalLine = function (x1, y, x2, scale, mult, cw, cf, dy, dw, dh, zIndex, callback) {
    var self = this; // for closures

    var x = x1;
    var dx;
    for (; x <= x2; x += 1) {
      dx = (x - this.x * cf) * this.tilesize * mult;
      this.drawTile(x, y, scale, (cw / 2) + dx, dy, dw, dh, zIndex, this._rd_cb);
    }
  };

  // Draw tiles from x, y1 to x, y2
  Map.prototype.drawVerticalLine = function (x, y1, y2, scale, mult, ch, cf, dx, dw, dh, zIndex, callback) {
    var self = this; // for closures

    var y = y1;
    var dy;
    for (; y <= y2; y += 1) {
      dy = (y - this.y * cf) * this.tilesize * mult;
      this.drawTile(x, y, scale, dx, (ch / 2) + dy, dw, dh, zIndex, this._rd_cb);
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

      if (img.parentNode !== self.container) { self.container.appendChild(img); }

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

      // lower scale versions?
      if (scale > self.min_scale) {

        var tscale = scale - 1;
        var factor = pow2(scale - tscale);

        var tx = Math.floor(x / factor);
        var ty = Math.floor(y / factor);

        var ax = dx - dw * (x - (tx * factor));
        var ay = dy - dh * (y - (ty * factor));
        var aw = dw * factor;
        var ah = dh * factor;

        var img = self.getTile(tx, ty, tscale);
        if (img) {
          drawImage(img, ax, ay, aw, ah, zIndex);
        } else {
          drawLower(tx, ty, tscale, ax, ay, aw, ah, zIndex - 1);
        }
      }
    }
    drawLower(x, y, scale, dx, dy, dw, dh, zIndex - 1);

    function drawHigher(x, y, scale, dx, dy, dw, dh, zIndex) {
      var tscale, factor, ox, oy, tx, ty, img, ax, ay, aw, ah;

      if (scale < self.max_scale) {
        tscale = scale + 1;
        factor = pow2(scale - tscale);

        for (oy = 0; oy < 2; oy += 1) {
          for (ox = 0; ox < 2; ox += 1) {

            tx = (x / factor) + ox;
            ty = (y / factor) + oy;
            img = self.getTile(tx, ty, tscale);

            ax = dx + ox * dw * factor;
            ay = dy + oy * dh * factor;
            aw = dw * factor;
            ah = dh * factor;

            if (img) {
              drawImage(img, ax, ay, aw, ah, zIndex);
            }
            //else {
            // Don't recurse as it would try an exponential
            // number of tiles
            //    //drawHigher(tx, ty, tscale, ax, ay, aw, ah, zIndex + 1);
            //}
          }
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

    var tscale = pow2(scale - 1);
    var options = this.options;
    var uri = SERVICE_BASE + "/api/tile?x=" + x + "&y=" + y + "&scale=" + tscale + "&options=" + options + "&style=" + this.style;
    Object.keys(this.tileOptions).forEach(function (key) {
      uri += '&' + encodeURIComponent(key) + '=' + encodeURIComponent(this.tileOptions[key]);
    }, this);

    if ('devicePixelRatio' in window && devicePixelRatio > 1) {
      uri += '&dpr=' + devicePixelRatio;
    }

    // Have it? Great, get out fast!
    var img = this.cache.fetch(uri);
    if (typeof img !== 'undefined') {
      return img;
    }

    // Load if missing?
    if (!callback) {
      return (void 0); // undefined
    }

    // In progress?
    if (this.loading[uri]) {
      return (void 0); // undefined
    }

    if (this.defer_loading) {
      return (void 0); // undefined
    }

    // Nope, better try loading it
    this.loading[uri] = true;
    var self = this; // for event handler closures
    img = new Image();
    img.onload = function() {
      delete self.loading[uri];
      self.cache.insert(uri, img);
      callback(img);
      img.onload = null; img.onerror = null;
    };
    img.onerror = function() {
      delete self.loading[uri];
      img.onload = null; img.onerror = null;
    };
    img.src = uri;
    img.style.position = "absolute";
    img.style.webkitTransform = "translate3d(0,0,0)"; // force hardware compositing

    return (void 0); // undefined
  };

  Map.prototype.shouldAnimateToSectorHex = function(scale, sx, sy, hx, hy) {
    if (scale !== this.scale) {
      return false;
    }

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

  Map.prototype.animateToSectorHex = function(sx, sy, hx, hy) {
    this.cancelAnimation();
    var target = sectorHexToLogical(sx, sy, hx, hy),
        ox = this.GetX(),
        oy = this.GetY(),
        tx = target.x,
        ty = target.y;

    if (ox === tx && oy === ty) {
      return;
    }

    this.animation = new Animation(3.0, 1000 / 40, function(p) {
      return Animation.smooth(p, 1.0, 0.1, 0.25);
    });
    var self = this;
    this.animation.onanimate = function(p) {
      self.SetPosition(Animation.interpolate(ox, tx, p), Animation.interpolate(oy, ty, p));
      self.redraw();
    };
  };

  Map.prototype.makeOverlay = function(overlay) {
    if (overlay === null) {
      return;
    }

    var div;
    if (overlay.element && overlay.element.parentNode) {
      overlay.element.parentNode.removeChild(overlay.element);
      div = overlay.element;
    } else {
      div = document.createElement("div");
      overlay.element = div;
    }

    // Compute physical location
    var pt1 = this.logicalToPixel(overlay.x, overlay.y);
    var pt2 = this.logicalToPixel(overlay.x + overlay.w, overlay.y + overlay.h);

    div.className = "overlay";
    div.id = overlay.id;

    div.style.left = String(pt1.x) + "px";
    div.style.top = String(pt1.y) + "px";
    div.style.width = String(pt2.x - pt1.x) + "px";
    div.style.height = String(pt1.y - pt2.y) + "px";
    div.style.zIndex = overlay.z;

    overlay.element = div;
    this.container.appendChild(div);
  };

  Map.prototype.makeMarker = function(marker) {
    if (marker === null) {
      return;
    }

    // TODO: Consider preserving existing item
    var div;
    if (marker.element && marker.element.parentNode) {
      marker.element.parentNode.removeChild(marker.element);
      div = marker.element;
    } else {
      div = document.createElement("div");
      marker.element = div;
    }

    var pt = sectorHexToLogical(marker.sx, marker.sy, marker.hx, marker.hy);
    pt = this.logicalToPixel(pt.x, pt.y);

    div.className = "marker";
    div.id = marker.id;

    div.style.left = String(pt.x) + "px";
    div.style.top = String(pt.y) + "px";
    div.style.zIndex = String(marker.z);

    marker.element = div;
    this.container.appendChild(div);
  };

  Map.prototype.logicalToPixel = function(lx, ly) {
    var f = pow2(1 - this.scale) / this.tilesize,
      rect = this.container.getBoundingClientRect();

    return {
      x: ((lx / this.tilesize - this.x) / f) + rect.width / 2,
      y: ((ly / -this.tilesize - this.y) / f) + rect.height / 2
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
    if (scale === this.scale) {
      return;
    }
    this.setScale(scale);
  };

  Map.prototype.GetOptions = function() {
    return this.options;
  };

  Map.prototype.SetOptions = function(options) {
    if (LEGACY_STYLES) {
      // Handy legacy styles specified in options bits
      if ((options & MapOptions.StyleMaskDeprecated) === MapOptions.PrintStyleDeprecated) {
        this.SetStyle("atlas", refresh);
      } else if ((options & MapOptions.StyleMaskDeprecated) === MapOptions.CandyStyleDeprecated) {
        this.SetStyle("candy", refresh);
      }
      options = options & ~MapOptions.StyleMaskDeprecated;
    }

    if (options === this.options) {
      return;
    }

    this.options = options & MapOptions.Mask;
    this.cache.clear();
    this.invalidate();
    fireEvent(this, "OptionsChanged", this.options);
  };


  Map.prototype.GetStyle = function() {
    return this.style;
  };

  Map.prototype.SetStyle = function(style) {
    if (style === this.style) {
      return;
    }

    this.style = style;
    this.cache.clear();
    fireEvent(this, "StyleChanged", this.style);
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
    fireEvent(this, "DisplayChanged");
    this.invalidate();
  };


  // This places the specified Sector, Hex coordinates (parsec)
  // at the center of the viewport, with a specific scale.
  Map.prototype.ScaleCenterAtSectorHex = function(scale, sx, sy, hx, hy) {
    this.cancelAnimation();

    if (this.shouldAnimateToSectorHex(1 + log2(Number(scale)), sx, sy, hx, hy)) {
      this.animateToSectorHex(sx, sy, hx, hy);
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

    this.animation = new Animation(1.0, 1000 / 40, function(p) {
      return Animation.smooth(p, 1.0, 0.1, 0.25);
    });
    var self = this;
    this.animation.onanimate = function(p) {
      self.x = Animation.interpolate(ox, tx, p);
      self.y = Animation.interpolate(oy, ty, p);
      self.redraw(true);
      fireEvent(self, "DisplayChanged");
    };
  };

  Map.prototype.ZoomIn = function() {
    this.setScale(this.scale + 1);
  };

  Map.prototype.ZoomOut = function() {
    this.setScale(this.scale - 1);
  };


  // NOTE: This API is subject to change
  Map.prototype.TEMP_AddMarker = function(id, sx, sy, hx, hy) {
    var marker = {
      "sx": sx,
      "sy": sy,
      "hx": hx,
      "hy": hy,

      "id": id,
      "z": 1009
    };

    this.markers.push(marker);
    this.makeMarker(marker);
  };


  Map.prototype.TEMP_AddOverlay = function(x, y, w, h) {
    // TODO: Take id, like AddMarker
    var overlay = {
      "x": x,
      "y": y,
      "w": w,
      "h": h,

      "id": "overlay",
      "z": 1010
    };

    this.overlays.push(overlay);
    this.makeOverlay(overlay);
  };

  global.Map = Map;

} (this));
