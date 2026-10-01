import './setup.js';
import assert from 'node:assert/strict';
import {test} from 'node:test';
import {prepareWorld, splitUWP} from '../../world_util.js';

// Shape of a world as returned by /api/jumpworlds.
function makeWorld(overrides = {}) {
  return {
    Name: 'Regina',
    Hex: '1910',
    Sector: 'Spinward Marches',
    SectorAbbreviation: 'Spin',
    SubsectorName: 'Regina',
    UWP: 'A788899-C',
    PBG: '703',
    Zone: '',
    Bases: 'NS',
    Allegiance: 'ImDd',
    Stellar: 'F7 V BD M3 V',
    Ix: '{ 4 }',
    Ex: '(D7E+5)',
    Cx: '[9C6D]',
    Nobility: 'BcCeF',
    Worlds: '8',
    Remarks: 'Ri Pa Ph An Cp (Amindii)2 Varg0 Asla0 Sa',
    ...overrides,
  };
}

test('splitUWP splits the UWP into its digits', () => {
  assert.deepEqual(splitUWP('A788899-C'), {
    Starport: 'A', Siz: '7', Atm: '8', Hyd: '8',
    Pop: '8', Gov: '9', Law: '9', Tech: 'C',
  });
});

test('prepareWorld decodes UWP, PBG, extensions and zone', async () => {
  const world = await prepareWorld(makeWorld());

  assert.equal(world.UWP.Starport, 'A');
  assert.equal(world.UWP.Tech, 'C');
  assert.ok(world.UWP.StarportBlurb);
  assert.equal(world.TotalPopulation, '700,000,000');
  assert.equal(world.PBG.Belts, 0);
  assert.equal(world.PBG.GG, 3);
  assert.equal(world.OtherWorlds, 4);  // 8 - main world - 0 belts - 3 GG
  assert.equal(world.Ix.Imp, '4');
  assert.equal(world.Ex.Eff, '+5');
  assert.equal(world.Cx.Het, '9');
  assert.equal(world.Zone.rating, 'Green');
  assert.ok(Array.isArray(world.Bases));
  assert.equal(world.Stars.length, 3);
  assert.ok(world.Remarks.some(r => r.code === 'Ri'));
  assert.equal(world.isPlaceholder, false);
  assert.equal(world.raw.UWP, 'A788899-C');
});

test('prepareWorld handles amber/red zones and placeholders', async () => {
  assert.equal((await prepareWorld(makeWorld({Zone: 'A'}))).Zone.rating, 'Amber');
  assert.equal((await prepareWorld(makeWorld({Zone: 'R'}))).Zone.rating, 'Red');
  assert.equal((await prepareWorld(makeWorld({UWP: '???????-?'}))).isPlaceholder, true);
  assert.equal(await prepareWorld(undefined), undefined);
});

test('prepareWorld: Zhodani base plus base-like remark does not throw', async () => {
  // Previously Bases was a string for Zhodani bases, and appending a
  // remark-derived base (e.g. Px) threw, so the world card never appeared.
  const world = await prepareWorld(
      makeWorld({Allegiance: 'ZhCo', Bases: 'KM', Remarks: 'Ri Px'}));
  assert.ok(Array.isArray(world.Bases));
  assert.equal(world.Bases[0], 'Zhodani Base');
  assert.equal(world.Bases.length, 2);
});

test('prepareWorld survives unavailable localStorage', async () => {
  const saved = globalThis.localStorage;
  globalThis.localStorage = {
    getItem() { throw new Error('SecurityError'); },
    setItem() { throw new Error('SecurityError'); },
  };
  try {
    const world = await prepareWorld(makeWorld());
    assert.match(world.map_link, /redir\.html\?href=/);
  } finally {
    globalThis.localStorage = saved;
  }
});
