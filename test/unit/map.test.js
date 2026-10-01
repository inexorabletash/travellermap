import {MemoryStorage} from './setup.js';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {test} from 'node:test';
import {Astrometrics, LRUCache, Util} from '../../map.js';

test('LRUCache evicts least recently used', () => {
  const cache = new LRUCache(2);
  cache.insert('a', 1);
  cache.insert('b', 2);
  assert.equal(cache.fetch('a'), 1);  // 'a' is now most recent
  cache.insert('c', 3);               // evicts 'b'
  assert.equal(cache.fetch('b'), undefined);
  assert.equal(cache.fetch('a'), 1);
  assert.equal(cache.fetch('c'), 3);
  assert.equal(cache.size, 2);
});

test('LRUCache re-insert updates value and recency', () => {
  const cache = new LRUCache(2);
  cache.insert('a', 1);
  cache.insert('b', 2);
  cache.insert('a', 10);  // 'b' is now least recent
  cache.insert('c', 3);
  assert.equal(cache.fetch('a'), 10);
  assert.equal(cache.fetch('b'), undefined);
});

test('LRUCache capacity only grows; clear empties', () => {
  const cache = new LRUCache(2);
  cache.ensureCapacity(1);
  assert.equal(cache.capacity, 2);
  cache.ensureCapacity(4);
  for (const k of ['a', 'b', 'c', 'd'])
    cache.insert(k, k);
  assert.equal(cache.size, 4);
  cache.clear();
  assert.equal(cache.size, 0);
  assert.equal(cache.fetch('a'), undefined);
});

test('LRUCache keys are not confused with Object.prototype', () => {
  const cache = new LRUCache(4);
  assert.equal(cache.fetch('constructor'), undefined);
  assert.equal(cache.fetch('__proto__'), undefined);
});

test('Util.makeURL builds query strings', () => {
  assert.equal(Util.makeURL('/api/coordinates', {sector: 'Spinward Marches', hex: '1910'}),
               'https://travellermap.com/api/coordinates?sector=Spinward+Marches&hex=1910');
  // null/undefined dropped, arrays repeated, existing query replaced
  assert.equal(Util.makeURL('/x?old=1', {a: null, b: undefined, c: [1, 2]}),
               'https://travellermap.com/x?c=1&c=2');
  assert.equal(Util.makeURL('/x?old=1'), 'https://travellermap.com/x');
});

test('Util.fromHex uses Traveller eHex digits (no I or O)', () => {
  assert.equal(Util.fromHex('9'), 9);
  assert.equal(Util.fromHex('A'), 10);
  assert.equal(Util.fromHex('h'), 17);
  assert.equal(Util.fromHex('J'), 18);  // I is skipped
  assert.equal(Util.fromHex('P'), 23);  // O is skipped
  assert.equal(Util.fromHex('?'), -1);
});

// Shared with the C# unit tests so client and server Astrometrics agree.
const FIXTURE = JSON.parse(readFileSync(
    new URL('../fixtures/astrometrics.json', import.meta.url), 'utf8'));

test('Astrometrics world coordinates match the shared fixture', () => {
  for (const {sx, sy, hx, hy, x, y} of FIXTURE.locations) {
    assert.deepEqual(Astrometrics.sectorHexToWorld(sx, sy, hx, hy), {x, y});
    assert.deepEqual(Astrometrics.worldToSectorHex(x, y), {sx, sy, hx, hy});
  }
});

test('Astrometrics.hexDistance matches the shared fixture', () => {
  for (const {ax, ay, bx, by, d} of FIXTURE.distances)
    assert.equal(Astrometrics.hexDistance(ax, ay, bx, by), d, `${ax},${ay} -> ${bx},${by}`);
});

test('Util.storage* tolerate missing, corrupt, and throwing storage', () => {
  localStorage.clear();
  assert.equal(Util.storageGet('missing'), null);
  assert.equal(Util.storageGetJSON('missing'), undefined);

  Util.storageSet('prefs', JSON.stringify({style: 'candy'}));
  assert.deepEqual(Util.storageGetJSON('prefs'), {style: 'candy'});

  localStorage.setItem('prefs', '{not json');
  assert.equal(Util.storageGetJSON('prefs'), undefined);

  const saved = globalThis.localStorage;
  globalThis.localStorage = {
    getItem() { throw new Error('SecurityError'); },
    setItem() { throw new Error('QuotaExceededError'); },
  };
  try {
    assert.equal(Util.storageGet('prefs'), null);
    assert.doesNotThrow(() => Util.storageSet('prefs', 'x'));
  } finally {
    globalThis.localStorage = saved;
  }
});
