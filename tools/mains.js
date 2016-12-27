const origin = 'https://travellermap.com';

function parseTabDelimited(text) {
  const lines = text.split(/\r?\n/), header = lines.shift().split('\t');
  return lines.map(line => {
    const cols = line.split('\t');
    const world = {};
    header.forEach((name, index) => world[name] = cols[index]);
    return world;
  });
}

const universe = new Set();
fetch(`${origin}/data?tag=OTU&milieu=M1105`).then(r => r.json()).then(data => {
  console.log(`fetching: ${data.Sectors.length} sectors`);
  return Promise.all(
    data.Sectors
      .map(s => s.Abbreviation)
      .map(s => Promise.all([
        fetch(`${origin}/data/${s}/metadata?accept=application/json`).then(r => r.json()),
        fetch(`${origin}/data/${s}/tab`).then(r => r.text())
      ])));
}).then(s => {
  console.log(`parsing: ${s.length} sectors`);
  s.forEach(pair => {
    const [meta, tab] = pair;
    const x = meta.X, y = meta.Y;
    parseTabDelimited(tab).forEach(world => {
      universe.add(`${x}/${y}/${world.Hex}`);
    });
  });
}).then(() => {

  function neighbors(world) {
    const [sx, sy, hex] = world.split('/');
    const hx = (hex / 100) | 0;
    const hy = (hex % 100);

    const out = [];

    function sig(sx, sy, hx, hy) {
      const hex = ('0000' + (hx * 100 + hy)).slice(-4);
      return `${sx}/${sy}/${hex}`;
    }

    function at(hx, hy) {
      let nsx = sx, nsy = sy;
      if (hx === 0) { hx = 32; --nsx; }
      if (hx === 33) { hx = 1; ++nsx; }
      if (hy === 0) { hy = 40; --nsy; }
      if (hy === 41) { hy = 1; ++nsy; }
      return sig(nsx, nsy, hx, hy);
    }

    function tryHex(hx, hy) {
      const s = at(hx, hy);
      if (universe.has(s))
        out.push(s);
    }

    tryHex(hx, hy - 1);
    tryHex(hx + 1, hy + ((hx % 2) ? -1 : 0));
    tryHex(hx + 1, hy + ((hx % 2) ? 0 : 1));
    tryHex(hx, hy + 1);
    tryHex(hx - 1, hy + ((hx % 2) ? 0 : 1));
    tryHex(hx - 1, hy + ((hx % 2) ? -1 : 0));

    return out;
  }

  console.log(`exploring: ${universe.size} worlds`);
  let main = 0;
  const seen = new Map();
  for (let world of universe) {
    if (seen.has(world)) continue;
    ++main;
    const stack = [];
    stack.push(world);
    while (stack.length) {
      const entry = stack.pop();
      seen.set(entry, main);
      for (let neighbor of neighbors(entry)) {
        if (seen.has(neighbor)) continue;
        stack.push(neighbor);
      }
    }
  }

  const MIN_SIZE = 5;

  console.log('inverting mains');
  let mains = [];
  for (let i = 1; i <= main; ++i)
    mains[i] = [];
  for (let [world, main] of seen)
    mains[main].push(world);
  mains = mains.filter(m => m && (m.length >= MIN_SIZE));
  mains.sort((a, b) => b.length - a.length);

  console.log('done');

  window.mains = mains;
  const ta = document.createElement('textarea');
  ta.cols = 80; ta.rows = 24;
  document.body.appendChild(ta);
  ta.value = JSON.stringify(mains);
});
