// Minimal browser globals so the client modules (map.js, world_util.js) can be
// imported under Node's test runner. Import this before those modules.

globalThis.window = globalThis;
globalThis.location = new URL('https://travellermap.com/');

// In-memory localStorage. Tests can replace globalThis.localStorage.
export class MemoryStorage {
  constructor() {
    this.items = new Map();
  }
  getItem(key) {
    return this.items.has(key) ? this.items.get(key) : null;
  }
  setItem(key, value) {
    this.items.set(key, String(value));
  }
  removeItem(key) {
    this.items.delete(key);
  }
  clear() {
    this.items.clear();
  }
}
globalThis.localStorage = new MemoryStorage();

// world_util.js fetches these tables at import time.
const FETCH_FIXTURES = {
  '/t5ss/sophonts': [{Code: 'Huma', Name: 'Human'}],
  '/res/maps/world_details.json': {},
};
globalThis.fetch = async url => {
  const path = new URL(url, location.href).pathname;
  if (path in FETCH_FIXTURES)
    return new Response(JSON.stringify(FETCH_FIXTURES[path]),
                        {headers: {'Content-Type': 'application/json'}});
  return new Response('Not Found', {status: 404, statusText: 'Not Found'});
};
