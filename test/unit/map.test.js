import {MemoryStorage} from './setup.js';
import assert from 'node:assert/strict';
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

test('Astrometrics world coordinates match the server', () => {
  // Reference (Core 0140) is the origin.
  assert.deepEqual(Astrometrics.sectorHexToWorld(0, 0, 1, 40), {x: 0, y: 0});
  // Regina (Spinward Marches 1910): server /api/coordinates gives
  // sx=-4, sy=-1, x=-110, y=-70.
  assert.deepEqual(Astrometrics.sectorHexToWorld(-4, -1, 19, 10), {x: -110, y: -70});
  assert.deepEqual(Astrometrics.worldToSectorHex(-110, -70), {sx: -4, sy: -1, hx: 19, hy: 10});

  // Round trip across sector corners, including negative sectors.
  for (const [sx, sy] of [[0, 0], [-1, -1], [3, -2]]) {
    for (const [hx, hy] of [[1, 1], [32, 40], [1, 40], [32, 1]]) {
      const {x, y} = Astrometrics.sectorHexToWorld(sx, sy, hx, hy);
      assert.deepEqual(Astrometrics.worldToSectorHex(x, y), {sx, sy, hx, hy});
    }
  }
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
