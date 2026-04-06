export class Util {
  /**
 * Element selector shorthand.
 * @param {string} s 
 * @returns {any}
 */
  static $(s) {
    if (s[0] === '#') {
      return document.getElementById(s.slice(1));
    }
    return document.querySelector(s);
  }

  /**
   * Element selector shorthand for multiple elements.
   * @param {string} s 
   * @returns {any[]}
   */
  static $$(s) {
    return Array.from(document.querySelectorAll(s));
  }

  /**
   * Constructs a URL by appending query parameters to a base URL.
   * Existing query parameters on the base URL are discarded.
   * Relative URLs are resolved against the current page (`location.href`).
   *
   * @param {string|URL|Location} base - The base URL. May be absolute, root-relative, or page-relative.
   * @param {Object.<string, string|number|boolean|Array.<string|number|boolean>>} [params] - Query parameters to append. Null/undefined values are skipped.
   * @returns {string} The constructed URL with query parameters.
   *
   * @example
   * makeURL('./print/route', { scale: 2, layer: ['A', 'B'] });
   * // => './print/route?scale=2&layer=A&layer=B' (resolved absolute URL)
   */
  static makeURL(base, params) {
    const url = new URL(String(base), location.href);
    url.search = '';
    if (params) {
      const searchParams = new URLSearchParams();
      for (const [key, value] of Object.entries(params)) {
        if (value == null) continue;
        for (const v of (Array.isArray(value) ? value : [value])) {
          searchParams.append(key, String(v));
        }
      }
      url.search = searchParams.toString();
    }
    return url.toString();
  }

  // Replace with URL/searchParams
  static parseURLQuery(url) {
    const o = Object.create(null);
    if (url.search && url.search.length > 1) {
      for (const pair of url.search.substring(1).split('&')) {
        if (!pair) continue;
        const kv = pair.split('=', 2);
        if (kv.length === 2)
          o[kv[0]] = decodeURIComponent(kv[1].replace(/\+/g, ' '));
        else
          o[kv[0]] = true;
      }
    }
    return o;
  }

  static escapeHTML(s) {
    return String(s).replace(/[&<>"']/g, c => {
      switch (c) {
        case '&': return '&amp;';
        case '<': return '&lt;';
        case '>': return '&gt;';
        case '"': return '&quot;';
        case "'": return '&#39;';
        default: return c;
      }
    });
  }

  static once(func) {
    let run = false;
    return /** @this {unknown} */ function () {
      if (run) return;
      run = true;
      func.apply(this, arguments);
    };
  }

  /**
   * Returns a debounced version of the provided function that delays until the sp;ecified time has elapsed since the last call.
   * @param {function} func - The function to debounce. 
   * @param {number} delay - The delay in milliseconds to wait before invoking the function after the last call.
   * @param {boolean} [immediate=false] - Whether to execute the function immediately on the leading edge instead of the trailing edge.
   * @returns {function}
   */
  static debounce(func, delay, immediate = false) {
    let timeoutId = null;
    /** @this {unknown} */
    return function (...args) {
      const callNow = immediate && !timeoutId;
      clearTimeout(timeoutId);
      timeoutId = setTimeout(() => {
        timeoutId = null;
        if (!immediate) func.apply(this, args);
      }, delay);
      if (callNow) func.apply(this, args);
    };
  }

  static memoize(f) {
    const cache = Object.create(null);
    /** @this {unknown} */ 
    return function () {
      const key = JSON.stringify([].slice.call(arguments));
      return (key in cache) ? cache[key] : cache[key] = f.apply(this, arguments);
    };
  }

  /**
    * Fetches an image from the specified URL, returning a promise that resolves with an image element.
    * @param {string} url 
    * @param {Object} [options]
    * @param {AbortSignal} [options.signal] - Optional AbortSignal to cancel the image request.
    * @param {HTMLImageElement} [options.imageElement] - Optional existing image element to reuse.
    * @returns {Promise<HTMLImageElement>} A promise that resolves with the loaded image.
    */
  static fetchImage(url, options = {}) {
    return new Promise((resolve, reject) => {
      options.signal?.addEventListener('abort', () => {
        img.src = '';
        reject(new DOMException('Aborted', 'AbortError'));
      });
      const img = options.imageElement ?? document.createElement('img');
      img.decoding = 'async';
      img.src = url;
      img.onload = () => { resolve(img); };
      img.onerror = () => { reject(Error('Image failed to load')); };
    });
  }

  /** @returns {object} */
  static parseCookies() {
    const cookies = {};
    for (const pair of document.cookie.split(/; +/g)) {
      const i = pair.indexOf('=');
      if (i === -1) cookies[''] = pair;
      else cookies[pair.substring(0, i)] = pair.substring(i + 1);
    }
    return cookies;
  }

  /** @returns {undefined} */
  static copyTextToClipboard(text) {
    const ta = document.createElement('textarea');
    ta.value = text;
    document.body.append(ta);
    if (navigator.userAgent.match(/iPad|iPhone|iPod/)) {
      ta.contentEditable = "true";
      ta.readOnly = true;
      const range = document.createRange();
      range.selectNodeContents(ta);
      const sel = window.getSelection();
      sel?.removeAllRanges();
      sel?.addRange(range);
      ta.setSelectionRange(0, text.length);

    } else {
      ta.select();
    }
    document.execCommand('copy');
    ta.remove();
  }

  static fromHex(c) {
    return '0123456789ABCDEFGHJKLMNPQRSTUVW'.indexOf(c.toUpperCase());
  }

  /**
 * Parses a sector from tab-delimited data.
 * @param {string} tabDelimitedData 
 * @returns {{ worlds: { [hex: string]: any } }} sector
 */
  static parseSector(tabDelimitedData) {
    const sector = {
      worlds: {}
    };
    const lines = tabDelimitedData.split(/\r?\n/);
    // @ts-ignore
    const header = lines
      .shift()
      .toLowerCase()
      .split('\t')
      .map(h => h.replace(/[^a-z]/g, ''));
    for (const line of lines) {
      if (!line.length) break;
      const world = {};
      for (const [index, field] of line.split('\t').entries()) {
        world[header[index]] = field;
      }
      sector.worlds[world.hex] = world;
    }
    return sector;
  }
}

//----------------------------------------------------------------------
// General Traveller stuff
//----------------------------------------------------------------------

const SERVICE_BASE = ((l) => {
  if ((l.hostname === 'localhost' && l.pathname.indexOf('~') !== -1) ||
    (l.protocol === 'file:'))
    return 'https://travellermap.com';
  return '';
})(window.location);

const LEGACY_STYLES = true;

//----------------------------------------------------------------------
// Enumerated types
//----------------------------------------------------------------------

export const MapOptions = {
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

export const Styles = {
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

export class Astrometrics {
  static ParsecScaleX = Math.cos(Math.PI / 6); // cos(30)
  static ParsecScaleY = 1.0;
  static SectorWidth = 32;
  static SectorHeight = 40;
  static ReferenceHexX = 1; // Reference is at Core 0140
  static ReferenceHexY = 40;
  static TileWidth = 256;
  static TileHeight = 256;
  static MinScale = 0.0078125;
  static MaxScale = 512;
  static HexEdge = Math.tan(Math.PI / 6) / 4 / Math.cos(Math.PI / 6);

  // World-space: Hex coordinate, centered on Reference
  static sectorHexToWorld(sx, sy, hx, hy) {
    return {
      x: (sx * this.SectorWidth) + hx - this.ReferenceHexX,
      y: (sy * this.SectorHeight) + hy - this.ReferenceHexY
    };
  }

  static worldToSectorHex(x, y) {
    x += this.ReferenceHexX - 1;
    y += this.ReferenceHexY - 1;

    const sx = Math.floor(x / this.SectorWidth);
    const sy = Math.floor(y / this.SectorHeight);
    const hx = (x - (sx * this.SectorWidth) + 1);
    const hy = (y - (sy * this.SectorHeight) + 1);

    return { sx: sx, sy: sy, hx: hx, hy: hy };
  }

  // Map-space: Cartesian coordinates, centered on Reference
  static sectorHexToMap(sx, sy, hx, hy) {
    const world = this.sectorHexToWorld(sx, sy, hx, hy);
    return this.worldToMap(world.x, world.y);
  }

  static worldToMap(wx, wy) {
    let x = wx;
    let y = wy;

    // Offset from the "corner of the hex
    x -= 0.5;
    y -= ((wx % 2) !== 0) ? 0 : 0.5;

    // Scale to non-homogenous coordinates
    x *= this.ParsecScaleX;
    y *= -this.ParsecScaleY;

    // Drop precision (avoid animations, etc)
    x = Math.round(x * 1000) / 1000;
    y = Math.round(y * 1000) / 1000;

    return { x, y };
  }

  static mapToWorld(x, y) {
    const wx = Math.round((x / this.ParsecScaleX) + 0.5);
    const wy = Math.round((-y / this.ParsecScaleY) + ((wx % 2 === 0) ? 0.5 : 0));
    return { x: wx, y: wy };
  }

  // World-space Coordinates (Reference is 0,0)
  static hexDistance(ax, ay, bx, by) {
    function even(x) { return (x % 2) == 0; }
    function odd(x) { return (x % 2) != 0; }
    const dx = bx - ax;
    const dy = by - ay;
    let adx = Math.abs(dx);
    let ody = dy + Math.floor(adx / 2);
    if (even(ax) && odd(bx))
      ody += 1;
    return Math.max(adx - ody, ody, adx);
  }
};

const Defaults = {
  options:
    MapOptions.SectorGrid | MapOptions.SubsectorGrid |
    MapOptions.SectorsSelected |
    MapOptions.BordersMajor | MapOptions.BordersMinor |
    MapOptions.NamesMajor |
    MapOptions.WorldsCapitals | MapOptions.WorldsHomeworlds,
  scale: 2,
  style: Styles.Poster
};

const STYLE_DEFAULTS = {
  overlay_color: '#8080ff',
  route_color: '#048104',
  main_s_color: 'pink',
  main_m_color: '#FFCC00',
  main_l_color: 'cyan',
  main_opacity: 0.25,
  ew_color: '#FFCC00',
  you_are_here_url: 'res/ui/youarehere.svg',
};

const STYLE_SHEETS = new Map([
  [Styles.Poster, STYLE_DEFAULTS],
  [Styles.Candy, STYLE_DEFAULTS],
  [Styles.Draft, STYLE_DEFAULTS],
  [Styles.Atlas, { ...STYLE_DEFAULTS, overlay_color: '#808080', you_are_here_url: 'res/ui/youarehere-gray.svg' }],
  [Styles.FASA, { ...STYLE_DEFAULTS, you_are_here_url: 'res/ui/youarehere-gray.svg' }],
  [Styles.Print, { ...STYLE_DEFAULTS, you_are_here_url: 'res/ui/youarehere-gray.svg' }],
]);

function styleLookup(style, property) {
  const sheet = STYLE_SHEETS.get(style) ?? STYLE_SHEETS.get(Defaults.style) ?? STYLE_DEFAULTS;
  return sheet[property];
}


// ======================================================================
// Data Services
// ======================================================================

export class MapService {
  // Internal abort controllers for each service function
  static #abortControllers = {};

  static #getAbortController(key) {
    if (this.#abortControllers[key]) {
      this.#abortControllers[key].abort();
    }
    this.#abortControllers[key] = new AbortController();
    return this.#abortControllers[key];
  }

  /**
   * Generic service function to make HTTP requests. If options.abortKey and options.signal are provided, will use an abortSignal.
   * @param {string} url - The URL to send the request to.
   * @param {Object} [options] - Optional parameters for the request.
   * @param {string} [options.method] - HTTP method (default: 'GET')
   * @param {string} [options.accept] - Optional Accept ContentType header value to specify the desired response format. Defaults to 'application/json'.
   * @param {string} [options.abortKey] - Optional key when provided will automatically abort when a new request is made with the same key.
   * @param {AbortController} [options.abortController] - Optional AbortController to cancel the request.
   * @returns {Promise<any>} The response data, parsed as JSON if the response is application/json, or as text otherwise.
   */
  static async #service(url, options = {}) {
    let signal = undefined;
    let abortController = undefined;
    if(options.abortKey !== undefined && options.abortKey !== null) {
      abortController = options.abortController ?? this.#getAbortController(options.abortKey);
    } else {
      abortController = options.abortController;
    }
    if(abortController) {
      signal = abortController.signal;
    }
    const accept = options.accept ?? 'application/json';
    const response = await fetch(url, {
      method: options.method ?? 'GET',
      headers: { Accept: accept },
      signal
    });
    if (!response.ok)
      throw new Error(response.statusText);
    return (accept === 'application/json') ?
      await response.json() : await response.text();
  }

  static #makeServiceUrl(path, options = {}) {
    // remove non-query parameters from options without mutating options object
    const { signal, method, accept, abortKey, abortController, ...queryOptions } = options;
    return Util.makeURL(SERVICE_BASE + path, queryOptions);
  }

  static makeURL(path, options = {}) {
    return this.#makeServiceUrl(path, options);
  }

  static coordinates(sector, hex, options = {}) {
    const urlOptions = { ...options, sector, hex };
    const url = this.#makeServiceUrl('/api/coordinates', urlOptions);
    options.accept = options.accept ?? 'application/json';
    return this.#service(url, options);
  }

  /**
   * Fetch the credits for the world at the specified coordinates.
   * @param {number} worldX - The X coordinate of the world.
   * @param {number} worldY - The Y coordinate of the world.
   * @param {string} milieu - The milieu context for the credits request.
   * @param {Object} [options] - Optional parameters for the request.
   * @param {string} [options.method] - HTTP method (default: 'GET')
   * @param {string} [options.accept] - Optional Accept ContentType header value to specify the desired response format. Defaults to 'application/json'.
   * @param {string} [options.abortKey] - Optional key when provided will automatically abort when a new request is made with the same key.
   * @param {AbortController} [options.abortController] - Optional AbortController to cancel the request.
   * @returns {any} The credits data, parsed as JSON if the response is application/json, or as text otherwise.
   */
  static credits(worldX, worldY, milieu, options = {}) {
    const urlOptions = { ...options, x: worldX, y: worldY, milieu };
    const url = this.#makeServiceUrl('/api/credits', urlOptions);
    return this.#service(url, options);
  }

  static search(query, milieu, options = {}) {
    const urlOptions = { ...options, q: query, milieu };
    const url = this.#makeServiceUrl('/api/search', urlOptions);
    return this.#service(url, options);
  }

  static sectorData(sector, options = {}) {
    const urlOptions = { ...options, sector };
    const url = this.#makeServiceUrl('/api/sec', urlOptions);
    return this.#service(url, options);
  }

  static sectorDataTabDelimited(sector, options = {}) {
    const urlOptions = { ...options, sector, type: 'TabDelimited' };
    const url = this.#makeServiceUrl('/api/sec', urlOptions);
    options.accept = options.accept ?? 'text/plain';
    return this.#service(url, options);
  }

  static sectorMetaData(sector, options = {}) {
    const urlOptions = { ...options, sector };
    const url = this.#makeServiceUrl('/api/metadata', urlOptions);
    return this.#service(url, options);
  }

  static MSEC(sector, options = {}) {
    const urlOptions = { ...options, sector };
    const url = this.#makeServiceUrl('/api/msec', urlOptions);
    options.accept = options.accept ?? 'text/plain';
    return this.#service(url, options);
  }

  static universe(options = {}) {
    const urlOptions = { ...options };
    const url = this.#makeServiceUrl('/api/universe', urlOptions);
    return this.#service(url, options);
  }
}

// ======================================================================
// Least-Recently-Used Cache
// ======================================================================

class LRUCache {
  constructor(capacity) {
    this.capacity = capacity;
    this.map = {};
    this.queue = [];
  }

  ensureCapacity(capacity) {
    if (this.capacity < capacity)
      this.capacity = capacity;
  }

  clear() {
    this.map = {};
    this.queue = [];
  }

  fetch(key) {
    key = '$' + key;
    const value = this.map[key];
    if (value === undefined)
      return undefined;

    const index = this.queue.indexOf(key);
    if (index !== -1)
      this.queue.splice(index, 1);
    this.queue.push(key);
    return value;
  }

  insert(key, value) {
    key = '$' + key;
    // Remove previous instances
    const index = this.queue.indexOf(key);
    if (index !== -1)
      this.queue.splice(index, 1);

    this.map[key] = value;
    this.queue.push(key);

    while (this.queue.length > this.capacity) {
      key = this.queue.shift();
      delete this.map[key];
    }
  }
}

// ======================================================================
// Image Stash
// ======================================================================

class ImageStash {
  constructor() {
    this.map = new Map();
  }

  get(url, callback) {
    if (this.map.has(url))
      return this.map.get(url);

    this.map.set(url, undefined);
    Util.fetchImage(url).then(img => {
      this.map.set(url, img);
      callback(img);
    });

    return undefined;
  }
}
const stash = new ImageStash();


// ======================================================================
// Animation Utilities
// ======================================================================

function isCallable(o) {
  return typeof o === 'function';
}

class Animation {
  // dur = total duration (seconds)
  // smooth = optional smoothing function
  // set onanimate to function called with animation position (0.0 ... 1.0)
  constructor(dur, smooth) {
    const start = Date.now();

    this.onanimate = null;
    this.oncancel = null;
    this.oncomplete = null;

    const tickFunc = () => {
      const f = (Date.now() - start) / 1000 / dur;
      if (f < 1.0)
        this.timerid = requestAnimationFrame(tickFunc);

      let p = f;
      if (isCallable(smooth))
        p = smooth(p);

      if (isCallable(this.onanimate))
        this.onanimate(p);

      if (f >= 1.0 && isCallable(this.oncomplete))
        this.oncomplete();
    };

    this.timerid = requestAnimationFrame(tickFunc);
  }

  cancel() {
    if (this.timerid) {
      cancelAnimationFrame(this.timerid);
      if (isCallable(this.oncancel))
        this.oncancel();
    }
  }
}

Animation.interpolate = (a, b, p) => {
  return a * (1.0 - p) + b * p;
};

// Time smoothing function - input time is t within duration dur.
// Acceleration period is a, deceleration period is d.
//
// Example:     t_filtered = smooth( t, 1.0, 0.25, 0.25 );
//
// Reference:   http://www.w3.org/TR/2005/REC-SMIL2-20050107/smil-timemanip.html
Animation.smooth = (t, dur, a, d) => {
  const dacc = dur * a;
  const ddec = dur * d;
  const r = 1 / (1 - a / 2 - d / 2);
  let r_t, tdec, pd;

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


// ======================================================================
// Observable name/value map
// ======================================================================

class NamedOptions {
  /** @type {string[]} */
  NAMES = [];
  constructor(notify) {
    this._options = {};
    this._notify = notify;
  }
  keys() { return Object.keys(this._options); }
  get(key) { return this._options[key]; }
  set(key, value) { this._options[key] = value; this._notify(key); }
  delete(key) { delete this._options[key]; this._notify(key); }
  /**
   * Iterates over each key/value pair in the options, invoking the provided callback function with the value, key, and index as arguments.
   * @param {function} fn 
   * @param {any} thisArg 
   */
  forEach(fn, thisArg = undefined) {
    const keys = Object.keys(this._options);
    for (let i = 0; i < keys.length; ++i) {
      const k = keys[i];
      fn.call(thisArg, this._options[k], k, i);
    }
  }
}

//----------------------------------------------------------------------
//
// Usage:
//
//   let map = new Map( document.getElementById('YourMapDiv') );
//
//   map.OnPositionChanged = () => { update permalink }
//   map.OnScaleChanged    = () => { update scale indicator }
//   map.OnStyleChanged    = () => { update control panel }
//   map.OnOptionsChanged  = () => { update control panel }
//
//   map.OnHover           = ( {x, y} ) => { show data }
//   map.OnClick           = ( {x, y} ) => { show data }
//   map.OnDoubleClick     = ( {x, y} ) => { show data }
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
//      .forEach((value, key, index) => { ... });
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

// ======================================================================
// Slippy Map using Tiles
// ======================================================================

function log2(v) { return Math.log(v) / Math.LN2; }
function pow2(v) { return Math.pow(2, v); }
function dist(x, y) { return Math.sqrt(x * x + y * y); }

const SINK_OFFSET = 1000;

const INT_OPTIONS = [
  'routes', 'rifts', 'dimunofficial',
  'sscoords', 'allhexes',
  'dw', 'an', 'mh', 'po', 'im', 'cp', 'stellar'
];
const STRING_OPTIONS = [
  'ew', 'qz', 'as', 'hw', 'milieu'
];

const ZOOM_DELTA = 0.5;
function roundScale(s) {
  return Math.round(s / ZOOM_DELTA) * ZOOM_DELTA;
}



export class TravellerMap {
  constructor(container, boundingElement) {
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

    this.namedOptions = new NamedOptions(Util.debounce((key) => {
      this.invalidate();
      this._optionsChanged(this.options);
    }, 1));
    this.namedOptions.NAMES = INT_OPTIONS.concat(STRING_OPTIONS);

    /**
     * Batch tile load redraws to avoid thrashing on rapid tile loads
     * @type {Function}
     */
    this._tileLoadDebounce = Util.debounce(() => this.invalidate(), 10);

    /**
     * Active tile requests and their abortControllers, keyed by tile URL
     * @type {Map<string, AbortController>}
     */
    this._activeTileRequests = new Map();
    this._maxConcurrentTileRequests = 8;
    this._tileRequestTimeout = 30000; // 30 seconds

    this.defer_loading = true;

    const CLICK_SCALE_DELTA = -0.5;
    const SCROLL_SCALE_DELTA = -0.15;
    const KEY_SCROLL_DELTA = 15;

    container.style.position = 'relative';

    // Event target, so it doesn't change during refreshes
    const sink = document.createElement('div');
    sink.style.position = 'absolute';
    sink.style.left = sink.style.top = sink.style.right = sink.style.bottom = (-SINK_OFFSET) + 'px';
    sink.style.zIndex = '1000';
    container.appendChild(sink);

    this.canvas = document.createElement('canvas');
    this.canvas.style.position = 'absolute';
    this.canvas.style.zIndex = '0';
    container.appendChild(this.canvas);

    this.ctx = this.canvas.getContext('2d');

    if (!this.ctx) {
      throw new Error('2D context not supported or canvas initialization failed.');
    }

    this.markers = [];
    this.overlays = [];
    /** @param {any[]|null} route */
    this.route = null;
    this.main = null;

    // ======================================================================
    // Event Handlers
    // ======================================================================

    // ----------------------------------------------------------------------
    // Mouse
    // ----------------------------------------------------------------------

    let dragging, drag_coords, was_dragged, previous_focus;
    container.addEventListener('mousedown', event => {
      if (event.button !== 0) return;
      this.cancelAnimation();
      previous_focus = document.activeElement;
      container.focus();
      dragging = true;
      was_dragged = false;
      drag_coords = this.eventCoords(event);
      container.classList.add('dragging');

      event.preventDefault();
      event.stopPropagation();
    }, true);

    let hover_coords;
    container.addEventListener('mousemove', event => {
      const coords = this.eventCoords(event);

      // Ignore mousemove immediately following mousedown with same coords.
      if (dragging && coords.x === drag_coords.x && coords.y === drag_coords.y)
        return;

      if (dragging) {
        was_dragged = true;

        this._offset(drag_coords.x - coords.x, drag_coords.y - coords.y);
        drag_coords = coords;
        event.preventDefault();
        event.stopPropagation();
      }

      const wc = this.eventToWorldCoords(event);

      // Throttle the events
      if (hover_coords && hover_coords.x === wc.x && hover_coords.y === wc.y)
        return;

      hover_coords = wc;
      this._hovered(hover_coords);
    }, true);

    document.addEventListener('mouseup', event => {
      if (event.button !== 0) return;
      if (dragging) {
        dragging = false;
        container.classList.remove('dragging');
        event.preventDefault();
        event.stopPropagation();
      }
    });

    container.addEventListener('click', event => {
      event.preventDefault();
      event.stopPropagation();

      if (!was_dragged) {
        this._clicked({ ...this.eventToWorldCoords(event), activeElement: previous_focus });
      }
    });

    container.addEventListener('dblclick', event => {
      event.preventDefault();
      event.stopPropagation();

      this.cancelAnimation();

      const MAX_DOUBLECLICK_SCALE = 9;
      if (this._logScale < MAX_DOUBLECLICK_SCALE) {
        let newscale = this._logScale + CLICK_SCALE_DELTA * (event.altKey ? 1 : -1);
        newscale = Math.min(newscale, MAX_DOUBLECLICK_SCALE);

        const coords = this.eventCoords(event);
        this._setScale(newscale, coords.x, coords.y);
      }

      this._doubleClicked(this.eventToWorldCoords(event));
    });

    container.addEventListener('wheel', event => {
      this.cancelAnimation();

      const newscale = this._logScale + SCROLL_SCALE_DELTA * Math.sign(event.deltaY);
      const coords = this.eventCoords(event);
      this._setScale(newscale, coords.x, coords.y);

      event.preventDefault();
      event.stopPropagation();
    });


    // ----------------------------------------------------------------------
    // Resize
    // ----------------------------------------------------------------------

    window.addEventListener('resize', () => {
      // Timeout to work around iOS Safari giving incorrect sizes while 'resize'
      // dispatched.
      setTimeout(() => {
        const rect = boundingElement.getBoundingClientRect();
        if (rect.left === this.rect.left &&
          rect.top === this.rect.top &&
          rect.width === this.rect.width &&
          rect.height === this.rect.height) return;
        this.rect = rect;
        this.resetCanvas();
      }, 150);
    });


    // ----------------------------------------------------------------------
    // Touch
    // ----------------------------------------------------------------------

    const DRAG_THRESHOLD = 8;
    let touch_start_coords;
    let pinch1, pinch2;
    let touch_coords, touch_wx, touch_wc, was_touch_dragged;

    container.addEventListener('touchmove', event => {
      if (event.touches.length === 1) {
        const coords = this.eventCoords(event.touches[0]);
        if (touch_coords.x !== coords.x || touch_coords.y !== coords.y) {

          // Only flag as dragged if movement exceeds threshold from starting point
          const dx = touch_start_coords.x - coords.x;
          const dy = touch_start_coords.y - coords.y;
          if (!was_touch_dragged && Math.sqrt(dx * dx + dy * dy) > DRAG_THRESHOLD) {
            was_touch_dragged = true;
          }

          this._offset(touch_coords.x - coords.x, touch_coords.y - coords.y);
          touch_coords = coords;
          touch_wc = this.eventToWorldCoords(event.touches[0]);
        }
      } else if (event.touches.length === 2) {
        was_touch_dragged = true;

        const od = dist(pinch2.x - pinch1.x, pinch2.y - pinch1.y),
          ocx = (pinch1.x + pinch2.x) / 2,
          ocy = (pinch1.y + pinch2.y) / 2;

        pinch1 = this.eventCoords(event.touches[0]);
        pinch2 = this.eventCoords(event.touches[1]);

        const nd = dist(pinch2.x - pinch1.x, pinch2.y - pinch1.y),
          ncx = (pinch1.x + pinch2.x) / 2,
          ncy = (pinch1.y + pinch2.y) / 2;

        this._offset(ocx - ncx, ocy - ncy);

        const newscale = this._logScale + log2(nd / od);
        this._setScale(newscale, ncx, ncy);
      }

      event.preventDefault();
      event.stopPropagation();
    }, true);

    container.addEventListener('touchend', event => {
      if (event.touches.length < 2) {
        this.defer_loading = false;
        this.invalidate();
      }

      if (event.touches.length === 1)
        touch_coords = this.eventCoords(event.touches[0]);

      if (event.touches.length === 0 && !was_touch_dragged) {
        this._clicked({ ...touch_wc, activeElement: previous_focus });
      }
      event.preventDefault();
      event.stopPropagation();
    }, true);

    container.addEventListener('touchstart', event => {
      was_touch_dragged = false;
      previous_focus = document.activeElement;

      if (event.touches.length === 1) {
        touch_coords = this.eventCoords(event.touches[0]);
        touch_start_coords = touch_coords;
        touch_wc = this.eventToWorldCoords(event.touches[0]);
      } else if (event.touches.length === 2) {
        this.defer_loading = true;
        pinch1 = this.eventCoords(event.touches[0]);
        pinch2 = this.eventCoords(event.touches[1]);
      }

      event.preventDefault();
      event.stopPropagation();
    }, true);


    // ----------------------------------------------------------------------
    // Keyboard
    // ----------------------------------------------------------------------

    // Scrolling - track key down/up state and scroll with RAF.
    const key_state = {};
    let keyscroll_timerid;
    const keyScroll = () => {
      let dx = 0, dy = 0;

      if (key_state['ArrowUp'] || key_state['i'])
        dy -= KEY_SCROLL_DELTA;
      if (key_state['ArrowDown'] || key_state['k'])
        dy += KEY_SCROLL_DELTA;
      if (key_state['ArrowLeft'] || key_state['j'])
        dx -= KEY_SCROLL_DELTA;
      if (key_state['ArrowRight'] || key_state['l'])
        dx += KEY_SCROLL_DELTA;

      if (dx || dy) {
        this.Scroll(dx, dy);
        requestAnimationFrame(keyScroll);
      } else {
        keyscroll_timerid = 0;
      }
    };
    container.addEventListener('keydown', event => {
      if (event.ctrlKey || event.altKey || event.metaKey)
        return;
      key_state[event.key] = true;
      if (!keyscroll_timerid)
        keyscroll_timerid = requestAnimationFrame(keyScroll);
    });
    container.addEventListener('keyup', event => {
      key_state[event.key] = false;
      if (!keyscroll_timerid)
        keyscroll_timerid = requestAnimationFrame(keyScroll);
    });

    container.addEventListener('keydown', event => {
      if (event.ctrlKey || event.altKey || event.metaKey)
        return;

      switch (event.key) {
        case '-': this.ZoomOut(); break;
        case '=': this.ZoomIn(); break;
        default: return;
      }

      event.preventDefault();
      event.stopPropagation();
    });

    // Final initialization.
    this.resetCanvas();
    this.defer_loading = false;
    this.invalidate();

    if (window == window.top) // == for IE
      container.focus();
  }

  // ======================================================================
  // Internal Methods
  // ======================================================================

  _offset(dx, dy) {
    this.position = [this.x + dx / this.scale, this.y - dy / this.scale];
  }

  _setScale(newscale, px, py) {
    newscale = Math.max(Math.min(newscale, this.max_scale), this.min_scale);
    if (newscale === this._logScale)
      return;

    this._logScale = newscale;

    const cw = this.rect.width,
      ch = this.rect.height;

    // Mathmagic to preserve hover coordinates
    if (arguments.length >= 3) {
      const hx = (this.x + (px - cw / 2) / this.scale) / this.tilesize;
      const hy = (-this.y + (py - ch / 2) / this.scale) / this.tilesize;
      this.position = [hx * this.tilesize - (px - cw / 2) / this.scale,
      -(hy * this.tilesize - (py - ch / 2) / this.scale)];
    }

    this.invalidate();
    this._scaleChanged(this.scale);
  }

  resetCanvas() {
    const cw = this.rect.width;
    const ch = this.rect.height;

    let dpr = 'devicePixelRatio' in window ? window.devicePixelRatio : 1;

    // iOS devices have a limit of 3 or 5 megapixels for canvas backing
    // store; given screen resolution * ~3x size for "tilt" display this
    // can easily be reached, so reduce effective dpr.
    if (dpr > 1 && /\biPad\b/.test(navigator.userAgent) &&
      this.tilt_enabled &&
      (cw * ch * dpr * dpr * 2 * 2) > 3e6) {
      dpr = 1;
    }

    // Scale factor for canvas to accomodate tilt.
    let sx = 1, sy = 1;
    if (this.tilt_enabled) {
      sx = 1.75;
      sy = 1.85;
    }

    // Pixel size of the canvas backing store.
    const pw = (cw * sx * dpr) | 0;
    const ph = (ch * sy * dpr) | 0;

    // Offset of the canvas against the container.
    let ox = 0, oy = 0;
    if (this.tilt_enabled) {
      ox = (-((cw * sx) - cw) / 2) | 0;
      oy = (-((ch * sy) - ch) * 0.8) | 0;
    }

    this.canvas.width = pw;
    this.canvas.height = ph;
    this.canvas.style.width = ((cw * sx) | 0) + 'px';
    this.canvas.style.height = ((ch * sy) | 0) + 'px';
    // @ts-ignore
    this.canvas.offset_x = ox;
    // @ts-ignore
    this.canvas.offset_y = oy;
    this.canvas.style.left = ox + 'px';
    this.canvas.style.top = oy + 'px';
    // @ts-ignore
    this.ctx.setTransform(1, 0, 0, 1, 0, 0);
    // @ts-ignore
    this.ctx.scale(dpr, dpr);

    this.redraw(true);
  }

  invalidate() {
    this.dirty = true;
    if (this._raf_handle) return;

    this._raf_handle = requestAnimationFrame(ms => {
      this._raf_handle = null;
      this.redraw();
    });
  }

  redraw(force) {
    if (!this.dirty && !force)
      return;

    this.dirty = false;

    // Integral scale (the tiles that will be used)
    const tscale = Math.round(this._logScale);

    // Tile URL (apart from x/y/scale)
    const params = { options: this.options, style: this.style };
    this.namedOptions.forEach((value, key) => {
      if (['ew', 'qz', 'as'].includes(key)) return;
      params[key] = value;
    });
    if ('devicePixelRatio' in window && window.devicePixelRatio > 1)
      params.dpr = window.devicePixelRatio;
    this._tile_url_base = Util.makeURL(SERVICE_BASE + '/api/tile', params);

    // How the tiles themselves are scaled (naturally 1, unless pinched)
    const tmult = pow2(this._logScale - tscale);

    // From map space to tile space
    // (Traveller map coords change at each integral zoom level)
    const cf = pow2(tscale - 1); // Coordinate factor (integral)

    // Compute edges in tile space
    const cw = this.rect.width,
      ch = this.rect.height;

    let l = this._tx * cf - (cw / 2) / (this.tilesize * tmult),
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

    const tileCount = (r - l + 1) * (b - t + 1);
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
    for (const marker of this.markers) this.drawMarker(marker);
    for (const overlay of this.overlays) this.drawOverlay(overlay);

    if (this.main)
      this.drawMain(this.main);
    if (this.route)
      this.drawRoute(this.route);

    if (this.namedOptions.get('ew'))
      this.drawWave(this.namedOptions.get('ew'));

    if (this.namedOptions.get('qz'))
      this.drawQZ();

    if (this.namedOptions.get('as'))
      this.drawAS(this.namedOptions.get('as'));
  }

  // Draw a rectangle (x1, y1) to (x2, y2)
  drawRectangle(x1, y1, x2, y2, scale, mult, ch, cw, cf) {
    const $this = this;
    const sizeMult = this.tilesize * mult;

    const dw = sizeMult;
    const dh = sizeMult;

    const ox = $this._tx * -cf * dw + (cw / 2);
    const oy = $this._ty * -cf * dh + (ch / 2);

    // Start from the center, work outwards, so center tiles load first.
    for (let dd = Math.floor((Math.min(x2 - x1 + 1, y2 - y1 + 1) + 1) / 2) - 1; dd >= 0; --dd)
      frame(x1 + dd, y1 + dd, x2 - dd, y2 - dd);

    function frame(x1, y1, x2, y2) {
      let x, y;
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
      const dx = x * dw + ox;
      const dy = y * dh + oy;
      $this.drawTile(x, y, scale, dx, dy, dw, dh);
    }
  }

  //
  // Draw the specified tile (scale, x, y) into the rectangle (dx, dy, dw, dh);
  // if the tile is not available it is requested, and higher/lower rez tiles
  // are used to fill in the gap until it loads.
  //
  drawTile(x, y, scale, dx, dy, dw, dh) {
    const $this = this; // for closures

    function drawImage(img, x, y, w, h) {
      // @ts-ignore
      x -= $this.canvas.offset_x;
      // @ts-ignore
      y -= $this.canvas.offset_y;
      const px = x | 0;
      const py = y | 0;
      const pw = ((x + w) | 0) - px;
      const ph = ((y + h) | 0) - py;
      $this.ctx.drawImage(img, px, py, pw, ph);
    }

    const img = this.getTile(x, y, scale, true);

    if (img) {
      drawImage(img, dx, dy, dw, dh);
      return;
    }

    // Otherwise, while we're waiting, see if we have upscale/downscale versions to draw instead

    function drawLower(x, y, scale, dx, dy, dw, dh) {
      if (scale <= $this.min_scale)
        return;

      const tscale = scale - 1;
      const factor = pow2(scale - tscale);

      const tx = Math.floor(x / factor);
      const ty = Math.floor(y / factor);

      const ax = dx - dw * (x - (tx * factor));
      const ay = dy - dh * (y - (ty * factor));
      const aw = dw * factor;
      const ah = dh * factor;

      const img = $this.getTile(tx, ty, tscale);
      if (img)
        drawImage(img, ax, ay, aw, ah);
      else
        drawLower(tx, ty, tscale, ax, ay, aw, ah);
    }
    drawLower(x, y, scale, dx, dy, dw, dh);

    function drawHigher(x, y, scale, dx, dy, dw, dh) {
      if (scale >= $this.max_scale)
        return;

      const tscale = scale + 1;
      const factor = pow2(scale - tscale);

      for (let oy = 0; oy < 2; oy += 1) {
        for (let ox = 0; ox < 2; ox += 1) {

          const tx = (x / factor) + ox;
          const ty = (y / factor) + oy;
          const img = $this.getTile(tx, ty, tscale);

          const ax = dx + ox * dw * factor;
          const ay = dy + oy * dh * factor;
          const aw = dw * factor;
          const ah = dh * factor;

          if (img)
            drawImage(img, ax, ay, aw, ah);
          // NOTE:  Don't recurse if not found as it would try an exponential number of tiles
          // e.g. drawHigher(tx, ty, tscale, ax, ay, aw, ah);
        }
      }
    }
    drawHigher(x, y, scale, dx, dy, dw, dh);
  }


  /**
   * Perform the tile request for the specified URL, with timeout and error handling.
   * @param {string} url - The URL of the tile to request.
   * @param {AbortController} abortController - The AbortController used if the request is timed-out.
   * @returns 
   */
  async _doTileRequest(url, abortController) {
    const timeout = setTimeout(() => abortController.abort(), this._tileRequestTimeout);
    try {
      const img = await Util.fetchImage(url, { signal: abortController.signal });
      this.cache.insert(url, img);
      this._tileLoadDebounce();
    } catch (err) {
      if (err?.name === 'AbortError') return;
      console.error(`Error loading tile ${url}:`, err);
    } finally {
      clearTimeout(timeout);
      this._activeTileRequests.delete(url);
    }
  }

  /**
  * Looks in the tile cache for the specified tile.
  * @param {number} x - The x coordinate of the tile.
  * @param {number} y - The y coordinate of the tile.
  * @param {number} scale - The zoom level of the tile.
  * @param {boolean} [allowFetch=false] - If fetching the tile from the server is allowed if not found in cache.
  * @returns {HTMLImageElement|undefined} The tile image if found in cache, or undefined if not found.
  */
  getTile(x, y, scale, allowFetch = false) {
    const url = this._tile_url_base + `&x=${x}&y=${y}&scale=${pow2(scale - 1)}`;

    const cached = this.cache.fetch(url);
    if (cached) return cached;

    const canRequest =
      allowFetch &&
      !this._activeTileRequests.has(url) &&
      !this.defer_loading &&
      navigator.onLine &&
      this._activeTileRequests.size < this._maxConcurrentTileRequests;

    if (!canRequest) return undefined;

    const abortController = new AbortController();
    this._activeTileRequests.set(url, abortController);
    this._doTileRequest(url, abortController);
    return undefined;
  }

  shouldAnimateTo(scale, x, y) {
    // TODO: Allow scale changes if target is "visible" (zooming in)
    if (scale !== this.scale)
      return false;

    const threshold = Astrometrics.SectorHeight * 64 / this.scale;
    return dist(x - this.x, y - this.y) < threshold;
  }

  cancelAnimation() {
    if (this.animation) {
      this.animation.cancel();
      this.animation = null;
    }
  }

  animateTo(scale, x, y, sec) {
    return /** @type {Promise<void>} */(new Promise((resolve, reject) => {
      this.cancelAnimation();
      sec = sec || 2.0;
      const os = this.scale,
        ox = this.x,
        oy = this.y;
      if (ox === x && oy === y && os === scale) {
        resolve();
        return;
      }

      this.animation = new Animation(sec, p => Animation.smooth(p, 1.0, 0.1, 0.25));

      this.animation.onanimate = p => {
        // Interpolate scale in log space.
        this.scale = pow2(Animation.interpolate(log2(os), log2(scale), p));
        // TODO: If animating scale, this should follow an arc (parabola?) through 3space treating
        // scale as Z and computing a height such that the target is in view at the turnaround?

        // For now, animate to position in 1/2 the overall animation time, so we spend
        // much of the animation time centered over the target.
        const hp = Math.min(p * 2, 1);
        const p2 = 1 - ((1 - hp) * (1 - hp));
        this.position = [Animation.interpolate(ox, x, p2), Animation.interpolate(oy, y, p2)];
      };

      this.animation.oncomplete = resolve;
      this.animation.oncancel = reject;
    }));
  }

  drawOverlay(overlay) {
    const ctx = this.ctx;
    ctx.save();
    // @ts-ignore
    ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 0.5;
    ctx.fillStyle = overlay.style || styleLookup(this.style, 'overlay_color');
    if (overlay.type === 'rectangle') {
      // Compute physical location
      const pt1 = this.mapToPixel(overlay.x, overlay.y);
      const pt2 = this.mapToPixel(overlay.x + overlay.w, overlay.y + overlay.h);
      ctx.fillRect(pt1.x, pt1.y, pt2.x - pt1.x, pt1.y - pt2.y);
    } else if (overlay.type === 'circle') {
      const pt = this.mapToPixel(overlay.x, overlay.y);
      const r = Math.abs(this.mapToPixel(overlay.x, overlay.y + overlay.r).y - pt.y);
      ctx.beginPath();
      ctx.ellipse(pt.x, pt.y, r, r, 0, 0, Math.PI * 2);
      ctx.fill();
    } else if (overlay.type === 'hex') {
      const halfH = 0.5;
      const halfMinW = (0.5 - Astrometrics.HexEdge) * Astrometrics.ParsecScaleX;
      const halfMaxW = (0.5 + Astrometrics.HexEdge) * Astrometrics.ParsecScaleX;
      const pts = [
        this.mapToPixel(overlay.x - halfMinW, overlay.y - halfH),
        this.mapToPixel(overlay.x + halfMinW, overlay.y - halfH),
        this.mapToPixel(overlay.x + halfMaxW, overlay.y),
        this.mapToPixel(overlay.x + halfMinW, overlay.y + halfH),
        this.mapToPixel(overlay.x - halfMinW, overlay.y + halfH),
        this.mapToPixel(overlay.x - halfMaxW, overlay.y)
      ];
      ctx.beginPath();
      let pt = pts[pts.length - 1];
      ctx.moveTo(pt.x, pt.y);
      for (let i = 0; i < pts.length; ++i) {
        pt = pts[i];
        ctx.lineTo(pt.x, pt.y);
      }
      ctx.fill();
    } else if (overlay.type === 'polygon') {
      let pts = overlay.points;
      let pt = this.mapToPixel(pts[0].x, pts[0].y);
      ctx.beginPath();
      ctx.moveTo(pt.x, pt.y);
      for (let i = 1; i < pts.length; ++i) {
        let pt = this.mapToPixel(pts[i].x, pts[i].y);
        ctx.lineTo(pt.x, pt.y);
      }
      ctx.closePath();

      if (('line' in overlay) || ('fill' in overlay)) {
        if ('fill' in overlay) {
          ctx.fillStyle = overlay.fill;
          ctx.fill();
        }
        if ('line' in overlay) {
          ctx.strokeStyle = overlay.line;
          ctx.lineWidth = 'w' in overlay ? overlay.w : 1;
          ctx.stroke();
        }
      } else {
        ctx.fill();
      }
    }
    ctx.restore();
  }

  /** @param {any[]} route */
  drawRoute(route) {
    const ctx = this.ctx;
    ctx.save();
    // @ts-ignore
    ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 0.5;
    ctx.strokeStyle = styleLookup(this.style, 'route_color');
    if (this._logScale >= 7)
      ctx.lineWidth = 0.25 * this.scale;
    else
      ctx.lineWidth = 15;

    ctx.beginPath();
    for (const [index, stop] of route.entries()) {
      let pt = Astrometrics.sectorHexToMap(stop.sx, stop.sy, stop.hx, stop.hy);
      pt = this.mapToPixel(pt.x, pt.y);
      ctx[index ? 'lineTo' : 'moveTo'](pt.x, pt.y);
    }
    const dots = (this._logScale >= 7) ? route : [route[0], route[route.length - 1]];
    for (const stop of dots) {
      let pt = Astrometrics.sectorHexToMap(stop.sx, stop.sy, stop.hx, stop.hy);
      pt = this.mapToPixel(pt.x, pt.y);
      ctx.moveTo(pt.x + ctx.lineWidth / 2, pt.y);
      ctx.arc(pt.x, pt.y, ctx.lineWidth / 2, 0, Math.PI * 2);
    }

    ctx.stroke();
    ctx.restore();
  }

  drawMain(main) {
    const ctx = this.ctx;
    ctx.save();
    // @ts-ignore
    ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = styleLookup(this.style, 'main_opacity');
    ctx.fillStyle = styleLookup(this.style,
      main.length <= 10 ? 'main_s_color' :
        main.length <= 50 ? 'main_m_color' : 'main_l_color');
    ctx.beginPath();
    const radius = 1.15 * this.scale / 2;
    for (const world of main) {
      let pt = Astrometrics.sectorHexToMap(world.sx, world.sy, world.hx, world.hy);
      pt = this.mapToPixel(pt.x, pt.y);
      ctx.moveTo(pt.x + radius, pt.y);
      ctx.arc(pt.x, pt.y, radius, 0, Math.PI * 2);
    }
    ctx.fill();
    ctx.restore();
  }

  drawMarker(marker) {
    const pt = this.mapToPixel(marker.x, marker.y);

    const ctx = this.ctx;
    let image;

    if (marker.url) {
      image = stash.get(marker.url, () => this.invalidate());
      if (!image) return;

      const MARKER_SIZE = 128;
      ctx.save();
      // @ts-ignore
      ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
      ctx.globalCompositeOperation = 'source-over';
      ctx.drawImage(image,
        pt.x - MARKER_SIZE / 2, pt.y - MARKER_SIZE / 2,
        MARKER_SIZE, MARKER_SIZE);
      ctx.restore();
      return;
    }

    if (styleLookup(this.style, marker.id + '_url')) {
      const url = styleLookup(this.style, marker.id + '_url');
      image = stash.get(url, () => this.invalidate());
      if (!image) return;

      ctx.save();
      // @ts-ignore
      ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
      ctx.globalCompositeOperation = 'source-over';
      ctx.drawImage(image, pt.x, pt.y);
      ctx.restore();
    }
  }

  currentYear(date) {
    let year = 1105;
    let m;
    if (date === 'milieu') {
      const milieu = this.namedOptions.get('milieu') || 'M1105';
      year = (milieu === 'IW') ? -2404 : Number(milieu.replace('M', ''));
    } else if ((m = /^(-?\d+)-(\d+)$/.exec(date))) {
      // day-year, e.g. 001-1105
      year = Number(m[2]) + (Number(m[1]) - 1) / 365;
    } else if (/^(-?\d+)\.(\d*)$/.test(date)) {
      // decimal year, e.g. 1105.5
      year = Number(date);
    } else if (/^-?\d+$/.test(date)) {
      // year
      year = Number(date) + 0.5;
    }
    return year;
  }

  // Empress Wave
  drawWave(date) {
    const year = this.currentYear(date);

    // Width?
    let w = 1; /*pc*/
    let m;
    if ((m = /^(-?\d+)-(\d+)$/.exec(date))) {
      // day-year, e.g. 001-1105
      w = 0.1;
    } else if (/^(-?\d+)\.(\d*)$/.test(date)) {
      // decimal year, e.g. 1105.5
      w = 0.1;
    }

    // Per MWM: Velocity of wave is PI * c
    const vel /*pc/y*/ = Math.PI /*ly/y*/ / 3.26 /*ly/pc*/;

    // Per MWM: center is 10000pc coreward
    const x = 0, y = 10000;

    // Per MWM: Wave crosses Ring 10,000 [Reference] on 045-1281
    const radius = (year - (1281 + (45 - 1) / 365)) * vel + y;
    if (radius < 0)
      return;

    const ctx = this.ctx;
    ctx.save();
    // @ts-ignore
    ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 0.3;
    ctx.lineWidth = Math.max(w * this.scale, 5);
    ctx.strokeStyle = styleLookup(this.style, 'ew_color');
    ctx.beginPath();
    const px_offset = 0.5; // offset from corner to center of hex
    const pt = this.mapToPixel(x + px_offset, y + px_offset);
    ctx.arc(pt.x,
      pt.y,
      this.scale * radius,
      Math.PI / 2 - Math.PI / 12,
      Math.PI / 2 + Math.PI / 12);
    ctx.stroke();
    ctx.restore();
  }

  // Qrekrsha Zone
  drawQZ() {
    const x = -179.4, y = 131, radius = 30 * Astrometrics.ParsecScaleX, w = 1;
    const ctx = this.ctx;
    ctx.save();
    // @ts-ignore
    ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 0.3;
    ctx.lineWidth = Math.max(w * this.scale, 5);
    ctx.strokeStyle = styleLookup(this.style, 'ew_color');
    ctx.beginPath();
    const px_offset = 0.5; // offset from corner to center of hex
    const pt = this.mapToPixel(x + px_offset, y + px_offset);
    ctx.arc(pt.x,
      pt.y,
      this.scale * radius, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
  }

  // Antares Supernova
  drawAS(date) {
    const year = this.currentYear(date);

    // Velocity of effect is light speed (so 1 ly/y)
    const vel /*pc/y*/ = 1 /*ly/y*/ / 3.26 /*ly/pc*/;

    // Center is Antares (ANT 2421)*/
    const x = 46.698, y = 58.5;

    // Different effect radii
    for (const max of [0.5, 4, 8, 12]) {

      // Date of supernova: 1270
      let radius = (year - 1270) * vel;
      if (radius < 0)
        return;

      // Maximum area of effect: 12 parsecs
      if (radius > max)
        radius = max;

      const ctx = this.ctx;
      ctx.save();
      // @ts-ignore
      ctx.translate(-this.canvas.offset_x, -this.canvas.offset_y);
      ctx.globalCompositeOperation = 'source-over';
      ctx.globalAlpha = 0.3 / 2;
      ctx.fillStyle = styleLookup(this.style, 'ew_color');
      ctx.beginPath();
      const px_offset = 0.5; // offset from corner to center of hex
      const pt = this.mapToPixel(x + px_offset, y + px_offset);
      ctx.arc(pt.x,
        pt.y,
        this.scale * radius, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
  }

  mapToPixel(mx, my) {
    return {
      x: (mx - this._tx * this.tilesize) * this.scale + this.rect.width / 2,
      y: (-my - this._ty * this.tilesize) * this.scale + this.rect.height / 2
    };
  }

  pixelToMap(px, py) {
    return {
      x: this._tx * this.tilesize + (px - this.rect.width / 2) / this.scale,
      y: -(this._ty * this.tilesize + (py - this.rect.height / 2) / this.scale)
    };
  }

  eventCoords(event) {
    // Attempt to get transformed coords; offsetX/Y for Chrome/Safari/IE,
    // layerX/Y for Firefox. Touch events lack these, so compute untransformed
    // coords.
    // TODO: Map touch coordinates back into world-space.
    const offsetX = 'offsetX' in event ? event.offsetX :
      'layerX' in event ? event.layerX :
        event.pageX - event.target.offsetLeft;
    const offsetY = 'offsetY' in event ? event.offsetY :
      'layerY' in event ? event.layerY :
        event.pageY - event.target.offsetTop;

    return {
      x: offsetX - SINK_OFFSET - this.rect.left,
      y: offsetY - SINK_OFFSET - this.rect.top
    };
  }

  eventToWorldCoords(event) {
    const coords = this.eventCoords(event);
    const map = this.pixelToMap(coords.x, coords.y);
    return Astrometrics.mapToWorld(map.x, map.y);
  }


  // ======================================================================
  // Public API
  // ======================================================================

  get scale() { return pow2(this._logScale - 1); }
  set scale(value) {
    value = 1 + log2(Number(value));
    if (value === this._logScale)
      return;
    this._setScale(value);
  }

  get logScale() { return this._logScale; }
  set logScale(value) {
    if (value === this._logScale)
      return;
    this._setScale(value);
  }

  get options() { return this._options; }
  set options(value) {
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
    this._optionsChanged(this._options);
  }

  get style() { return this._style; }
  set style(value) {
    if (value === this._style) return;

    this._style = value;
    this.cache.clear();
    this.invalidate();
    this._styleChanged(this._style);
  }

  get x() { return this._tx * this.tilesize; }
  set x(value) { this.position = [value, this.y]; }

  get y() { return this._ty * -this.tilesize; }
  set y(value) { this.position = [this.x, value]; }

  get position() { return [this._tx * this.tilesize, this._ty * -this.tilesize]; }
  set position(value) {
    const x = value[0] / this.tilesize,
      y = value[1] / -this.tilesize;
    if (x === this._tx && y === this._ty) return;
    this._tx = x;
    this._ty = y;
    this.invalidate();
    this._positionChanged();
  }

  get worldX() { return Astrometrics.mapToWorld(this.x, this.y).x; }

  get worldY() { return Astrometrics.mapToWorld(this.x, this.y).y; }

  // This places the specified Sector, Hex coordinates (parsec)
  // at the center of the viewport.
  CenterAtSectorHex(sx, sy, hx, hy, options) {
    options = { ...options };

    this.cancelAnimation();
    const target = Astrometrics.sectorHexToMap(sx, sy, hx, hy);

    if (!options.immediate &&
      'scale' in options &&
      this.shouldAnimateTo(options.scale, target.x, target.y)) {
      this.animateTo(options.scale, target.x, target.y)
        .catch(function () { });
      return;
    }

    if ('scale' in options)
      this.scale = options.scale;
    this.position = [target.x, target.y];
  }

  // Move and scale display to show the specified world space area
  CenterOnArea(minWorldX, minWorldY, maxWorldX, maxWorldY) {
    const worldWidth = maxWorldX - minWorldX;
    const worldHeight = maxWorldY - minWorldY;
    // Calculate the hex to center the view around
    const centerWorldX = Math.round(worldWidth / 2) + minWorldX;
    const centerWorldY = Math.round(worldHeight / 2) + minWorldY;
    const centerSectorHex = Astrometrics.worldToSectorHex(centerWorldX, centerWorldY);
    // Calculate how large the area covered by the area is in current pixel coordinates
    const minMapPosition = Astrometrics.worldToMap(minWorldX, minWorldY);
    const maxMapPosition = Astrometrics.worldToMap(maxWorldX, maxWorldY);
    const minPixelPosition = this.mapToPixel(minMapPosition.x, minMapPosition.y);
    const maxPixelPosition = this.mapToPixel(maxMapPosition.x, maxMapPosition.y);
    const pixelWidth = maxPixelPosition.x - minPixelPosition.x;
    const pixelHeight = maxPixelPosition.y - minPixelPosition.y;
    // Calculate the new scale value based on the current scale value
    const pixelRatio = (pixelWidth > pixelHeight) ? (pixelWidth / this.rect.width) : (pixelHeight / this.rect.height);
    const divisor = Math.abs(pixelRatio) * 2;
    const newScale = this.scale / divisor;
    // Update the view
    this.CenterAtSectorHex(
      centerSectorHex.sx,
      centerSectorHex.sy,
      centerSectorHex.hx,
      centerSectorHex.hy,
      { scale: newScale });
  }

  // Scroll the map view by the specified dx/dy (in pixels)
  Scroll(dx, dy, fAnimate) {
    this.cancelAnimation();

    if (!fAnimate) {
      this._offset(dx, dy);
      return;
    }

    const s = this.scale * this.tilesize,
      ox = this.x,
      oy = this.y,
      tx = ox + dx / s,
      ty = oy + dy / s;

    this.animation = new Animation(1.0, p => Animation.smooth(p, 1.0, 0.1, 0.25));
    this.animation.onanimate = p => {
      this.position = [Animation.interpolate(ox, tx, p), Animation.interpolate(oy, ty, p)];
    };
  }


  ZoomIn() {
    this._setScale(roundScale(this._logScale) + ZOOM_DELTA);
  }

  ZoomOut() {
    this._setScale(roundScale(this._logScale) - ZOOM_DELTA);
  }


  // NOTE: This API is subject to change
  // |x| and |y| are map-space coordinates
  AddMarker(id, x, y, opt_url) {
    const marker = {
      x,
      y,
      id,
      url: opt_url,
      z: 909
    };

    this.markers.push(marker);
    this.invalidate();
  }


  AddOverlay(o) {
    // TODO: Take id, like AddMarker
    const overlay = {
      id: 'overlay',
      z: 910,
      ...o
    };

    this.overlays.push(overlay);
    this.invalidate();
  }

  // Remove overlays that don't match the specified filter
  FilterOverlays(filterCallback) {
    this.overlays = this.overlays.filter(filterCallback);
    this.invalidate();
  }

  ClearOverlays() {
    this.overlays = [];
    this.invalidate();
  }

  /** @param {any[]|null} route */
  SetRoute(route) {
    this.route = route;
    this.invalidate();
  }

  SetMain(main) {
    this.main = main;
    this.invalidate();
  }

  EnableTilt() {
    this.tilt_enabled = true;
    this.resetCanvas();
  }

  ApplyURLParameters() {
    const params = Util.parseURLQuery(document.location);

    function float(prop) {
      const n = parseFloat(params[prop]);
      return isNaN(n) ? 0 : n;
    }

    function int(prop) {
      const v = params[prop];
      if (typeof v === 'boolean') return v ? 1 : 0;
      const n = parseInt(v, 10);
      return isNaN(n) ? 0 : n;
    }

    function has(params, list) {
      return list.every(item => item in params);
    }

    if ('scale' in params)
      this.scale = float('scale');

    if ('options' in params)
      this.options = int('options');

    if ('style' in params)
      this.style = params.style;

    if (has(params, ['yah_sx', 'yah_sy', 'yah_hx', 'yah_hx'])) {
      const pt = Astrometrics.sectorHexToMap(int('yah_sx'), int('yah_sy'), int('yah_hx'), int('yah_hy'));
      this.AddMarker('you_are_here', pt.x, pt.y);
    } else if (has(params, ['yah_x', 'yah_y'])) {
      this.AddMarker('you_are_here', float('yah_x'), float('yah_y'));
    } else if (has(params, ['yah_sector'])) {
      (async () => {
        try {
          const location = await MapService.coordinates(params.yah_sector, params.yah_hex);
          const pt = Astrometrics.worldToMap(location.x, location.y);
          this.AddMarker('you_are_here', pt.x, pt.y);
        } catch (ex) {
          alert('The requested marker location "' + params.yah_sector +
            ('yah_hex' in params ? (' ' + params.yah_hex) : '') +
            '" was not found.');
        }
      })();
    }

    if (has(params, ['marker_sx', 'marker_sy', 'marker_hx', 'marker_hx', 'marker_url'])) {
      const pt = Astrometrics.sectorHexToMap(int('marker_sx'), int('marker_sy'), int('marker_hx'), int('marker_hy'));
      this.AddMarker('custom', pt.x, pt.y, params.marker_url);
    } else if (has(params, ['marker_x', 'marker_y', 'marker_url'])) {
      this.AddMarker('custom', float('marker_x'), float('marker_y'), params.marker_url);
    } else if (has(params, ['marker_sector', 'marker_url'])) {
      (async () => {
        try {
          const location = await MapService.coordinates(params.marker_sector, params.marker_hex);
          const pt = Astrometrics.worldToMap(location.x, location.y);
          this.AddMarker('custom', pt.x, pt.y, params.marker_url);
        } catch (ex) {
          alert('The requested marker location "' + params.marker_sector +
            ('marker_hex' in params ? (' ' + params.marker_hex) : '') +
            '" was not found.');
        }
      })();
    }

    // Rectangle overlays
    for (let i = 0; ; ++i) {
      const n = (i === 0) ? '' : i,
        oxs = 'ox' + n, oys = 'oy' + n, ows = 'ow' + n, ohs = 'oh' + n,
        oss = 'os' + n;
      if (has(params, [oxs, oys, ows, ohs])) {
        const x = float(oxs);
        const y = float(oys);
        const w = float(ows);
        const h = float(ohs);
        this.AddOverlay({ type: 'rectangle', x: x, y: y, w: w, h: h, style: params[oss] });
      } else {
        break;
      }
    }
    // Compact form
    if ('or' in params) {
      for (const or of params.or.split('~')) {
        function float(s) { const n = parseFloat(s); return isNaN(n) ? 0 : n; }
        const a = or.split('!');
        this.AddOverlay({
          type: 'rectangle',
          x: float(a[0]), y: float(a[1]), w: float(a[2]), h: float(a[3]),
          style: a[4]
        });
      }
    }

    // Circle overlays
    for (let i = 0; ; ++i) {
      const n = (i === 0) ? '' : i;
      const ocxs = 'ocx' + n, ocys = 'ocy' + n, ocrs = 'ocr' + n, ocss = 'ocs' + n;
      if (has(params, [ocxs, ocys, ocrs])) {
        const cx = float(ocxs);
        const cy = float(ocys);
        const cr = float(ocrs);
        this.AddOverlay({ type: 'circle', x: cx, y: cy, r: cr, style: params[ocss] });
      } else {
        break;
      }
    }
    // Compact form
    if ('oc' in params) {
      for (const oc of params.oc.split('~')) {
        function float(s) { const n = parseFloat(s); return isNaN(n) ? 0 : n; }
        const a = oc.split('!');
        this.AddOverlay({
          type: 'circle', x: float(a[0]), y: float(a[1]), r: float(a[2]), style: a[3]
        });
      }
    }

    // Various coordinate schemes - ordered by priority
    if ('p' in params) {
      const parts = params.p.split('!');
      this.logScale = parseFloat(parts[2]) || 0;
      this.position = [parseFloat(parts[0]) || 0, parseFloat(parts[1]) || 0];
    } else if (has(params, ['x', 'y'])) {
      this.position = [float('x'), float('y')];
    } else if (has(params, ['sx', 'sy', 'hx', 'hy', 'scale'])) {
      this.CenterAtSectorHex(
        float('sx'), float('sy'), float('hx'), float('hy'), { scale: float('scale') });
    } else if ('sector' in params) {
      (async () => {
        try {
          const location = await MapService.coordinates(params.sector, params.hex, { subsector: params.subsector });
          if (location.hx && location.hy) { // NOTE: Test for undefined -or- zero
            this.CenterAtSectorHex(location.sx, location.sy, location.hx, location.hy, { scale: 64 });
          } else {
            this.CenterAtSectorHex(location.sx, location.sy,
              Astrometrics.SectorWidth / 2, Astrometrics.SectorHeight / 2,
              { scale: 16 });
          }

          if ('yah' in params) {
            console.log('here!!!');
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
        } catch (ex) {
          alert('The requested location "' + params.sector +
            ('hex' in params ? (' ' + params.hex) : '') + '" was not found.');
        }
      })();
    }

    // Int/Boolean options
    for (const name of INT_OPTIONS) {
      if (name in params)
        this.namedOptions.set(name, int(name));
    }
    // String options
    for (const name of STRING_OPTIONS) {
      if (name in params)
        this.namedOptions.set(name, params[name]);
    };

    return params;
  }

  /** @type {Function|undefined} */
  onDoubleClick;
  /** @type {Function|undefined} */
  onClick;
  /** @type {Function|undefined} */
  onPositionChanged;
  /** @type {Function|undefined} */
  onScaleChanged;
  /** @type {Function|undefined} */
  onStyleChanged;
  /** @type {Function|undefined} */
  onOptionsChanged;
  /** @type {Function|undefined} */
  onHovered;

  /**
   * Fire the specified event handler with the specified data, if the handler is defined.
   * @param {Function|undefined} func 
   * @param {any} data 
   */
  _fireEvent(func, data = undefined) {
    if (func === undefined) return;
    const invoke = () => {
      if (func === undefined) return;
      try {
        func.call(this, data);
      }
      catch (err) {
        console.error('Event handler error', func?.name || '<handler>', err);
      }
    };
    queueMicrotask(invoke);
  }

  _doubleClicked(data) {
    this._fireEvent(this.onDoubleClick, data);
  }
  _clicked(data) {
    this._fireEvent(this.onClick, data);
  }
  _positionChanged() {
    this._fireEvent(this.onPositionChanged);
  }
  _scaleChanged(data) {
    this._fireEvent(this.onScaleChanged, data);
  }
  _styleChanged(data) {
    this._fireEvent(this.onStyleChanged, data);
  }
  _optionsChanged(data) {
    this._fireEvent(this.onOptionsChanged, data);
  }
  _hovered(data) {
    this._fireEvent(this.onHovered, data);
  }
}

