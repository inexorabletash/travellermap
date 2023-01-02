/*global Restellarator, Request*/
'use strict';

const EHEX = '0123456789ABCDEFGHJKLMNPQRSTUV';
function fromEHex(c) { return EHEX.indexOf(c); }
function toEHex(n) { return EHEX.substr(n, 1); }
function toSInt(n) { return n < 0 ? String(n) : '+' + String(n); }
function roll1D() { return Math.floor(Math.random() * 6) + 1; }
function roll2D() { return roll1D() + roll1D(); }
function roll1D10() { return Math.floor(Math.random() * 10) + 1; }
function flux() { return roll1D() - roll1D(); }

Number.prototype.in = function(min, max) {
  if (Array.isArray(min))
    return min.indexOf(this) !== -1;
  return min <= this && this <= max;
};

function parse(text) {
  const lines = text.split(/\r\n|\r|\n/).filter(s => s.length > 0);
  const header = lines.shift().split('\t');
  const worlds = lines.map(line => {
    const world = {};
    const cols = line.split('\t');
    cols.forEach((value, index) => {
      world[header[index]] = value === '-' ? '' : value;
    });
    return world;
  });
  return worlds;
}

function process(world) {
  world.St  = world.UWP.substr(0, 1);
  world.Siz = fromEHex(world.UWP.substr(1, 1));
  world.Atm = fromEHex(world.UWP.substr(2, 1));
  world.Hyd = fromEHex(world.UWP.substr(3, 1));
  world.Pop = fromEHex(world.UWP.substr(4, 1));
  world.Gov = fromEHex(world.UWP.substr(5, 1));
  world.Law = fromEHex(world.UWP.substr(6, 1));
  world.TL  = fromEHex(world.UWP.substr(8, 1));

  world.PMult  = fromEHex(world.PBG.substr(0, 1));
  world.Belts  = fromEHex(world.PBG.substr(1, 1));
  world.GG     = fromEHex(world.PBG.substr(2, 1));

  const codes = world.Remarks.split(/\s+/g)
          .filter(s => s.length)
          .map(c => c === 'Cr' ? 'Cx' : c);
  const codeSet = new Set(codes);

  if (world.Pop === 0)
    world.Gov = world.Law = world.PMult = 0;

  // Correct over-abundance of Dieback worlds
  if (world.Pop === 0 && world.TL > 0 && roll2D() > 2)
    world.TL = 0;

  world.UWP = `${world.St}${toEHex(world.Siz)}${toEHex(world.Atm)}${toEHex(world.Hyd)}${toEHex(world.Pop)}${toEHex(world.Gov)}${toEHex(world.Law)}-${toEHex(world.TL)}`;
  world.PBG = `${toEHex(world.PMult)}${toEHex(world.Belts)}${toEHex(world.GG)}`;

  // Planetary
  world.As = world.Siz === 0;
  world.De = world.Atm.in(2, 9) && world.Hyd === 0;
  world.Fl = world.Atm.in(10, 12) && world.Hyd.in(1, 10);
  world.Ga = world.Siz.in(6, 8) && world.Atm.in([5, 6, 8]) && world.Hyd.in(5, 7);
  world.He = world.Siz.in([3, 4, 5, 6, 7, 8, 9, 10, 11, 12])
    && world.Atm.in([2, 4, 7, 9, 10, 11, 12]) && world.Hyd.in(0, 2);
  world.Ic = world.Atm.in(0, 1) && world.Hyd.in(1, 10);
  world.Oc = world.Siz.in(10, 15) && world.Atm.in(3,9) && world.Hyd === 10;
  world.Va = world.Atm === 0;
  world.Wa = world.Siz.in(3, 9) && world.Atm.in(3,9) && world.Hyd === 10;

  const PLANETARY_CODES = ['As', 'De', 'Fl', 'Ga', 'He', 'Ic', 'Oc', 'Va', 'Wa'];
  PLANETARY_CODES.forEach(c => {
    codeSet.delete(c);
  });

  // Population
  world.Di = world.Pop === 0 && world.Gov === 0 && world.Law === 0 && world.TL > 0;
  world.Ba = world.Pop === 0 && world.Gov === 0 && world.Law === 0 && world.TL === 0;
  world.Lo = world.Pop.in(1, 3);
  world.Ni = world.Pop.in(4, 6);
  world.Ph = world.Pop === 8;
  world.Hi = world.Pop.in(9, 12);

  const POPULATION_CODES = ['Di', 'Ba', 'Lo', 'Ni', 'Ph', 'Hi'];
  POPULATION_CODES.forEach(c => {
    codeSet.delete(c);
  });

  // Economic
  world.Pa = world.Atm.in(4, 9) && world.Hyd.in(4, 8) && world.Pop.in([4,8]);
  world.Ag = world.Atm.in(4, 9) && world.Hyd.in(4, 8) && world.Pop.in(5, 7);
  world.Na = world.Atm.in(0, 3) && world.Hyd.in(0, 3) && world.Pop.in(6, 12);
  world.Pi = world.Atm.in([0,1,2,4,7,9]) && world.Pop.in(7, 8);
  world.In = world.Atm.in([0,1,2,4,7,9,10,11,12]) && world.Pop.in(9, 15);
  world.Po = world.Atm.in(2, 5) && world.Hyd.in(0, 3);
  world.Pr = world.Atm.in([6,8]) && world.Pop.in([5,9]);
  world.Ri = world.Atm.in([6,8]) && world.Pop.in(6, 8);

  const ECONOMIC_CODES = ['Pa', 'Ag', 'Na', 'Pi', 'In', 'Po', 'Pr', 'Ri'];
  ECONOMIC_CODES.forEach(c => {
    codeSet.delete(c);
  });

  // Climate
  const CLIMATE_CODES = ['Fr', 'Ho', 'Co', 'Lk', 'Tr', 'Tu', 'Tz'];
  CLIMATE_CODES.forEach(c => {
    world[c] = codes.includes(c);
    if (world[c]) codeSet.delete(c);
  });

  // Secondary
  const SECONDARY_CODES = ['Fa', 'Mi', 'Mr', 'Px', 'Pe', 'Re'];
  SECONDARY_CODES.forEach(c => {
    world[c] = codes.includes(c);
    if (world[c]) codeSet.delete(c);
  });

  // Political
  const POLITICAL_CODES = ['Cp', 'Cs', 'Cx', 'Cy'];
  POLITICAL_CODES.forEach(c => {
    world[c] = codes.includes(c);
    if (world[c]) codeSet.delete(c);
  });

  // Special
  const SPECIAL_CODES = ['Fo', 'Pz', 'Da'];
  SPECIAL_CODES.forEach(c => {
    codeSet.delete(c);
  });
  const ADDITIONAL_CODES = ['Sa', 'Ab', 'An'];
  ADDITIONAL_CODES.forEach(c => {
    world[c] = codes.includes(c);
    if (world[c]) codeSet.delete(c);
  });

  world.Sophonts = codes.filter(
    c =>
      /^....[0-9W]$/.test(c) ||
      /^(Di)?\(.*\)[0-9W]?$/.test(c)
  ).map(c => {
    codeSet.delete(c);
    return c;
  }).join(' ');


  world.Details = codes.filter(
    c => /^Rs/.test(c) || /^Mr.+/.test(c) || /^O:/.test(c)
  ).map(c => {
    codeSet.delete(c);
    return c;
  }).concat([
    'Fr', 'Ho', 'Co', 'Lk', 'Tr', 'Tu', 'Tz',
    'Fa', 'Mi', 'Mr', 'Px', 'Pe', 'Re',
    'Sa', 'Ab'
    // NOTE: Not An
  ].filter(c => world[c]))
    .join(' ');

  // Stellar configuration - just remove
  codes.filter(c => /^S[0-9A-F]+/.test(c)).forEach(c => {
    codeSet.delete(c);
  });

  // Report unmatched codes
  if (codeSet.size)
    console.warn(`Unmatched codes (${world.Hex}): ` + Array.from(codeSet).map(s=>JSON.stringify(s)).join(' '));


  world.Remarks = ([]
                   .concat(PLANETARY_CODES,POPULATION_CODES,ECONOMIC_CODES)
                   .map(code => world[code] ? code : '')
                   .join(' ')
                   + ' ' + world.Sophonts
                   + ' ' + world.Details).trim().replace(/\s{2,}/g, ' ');
}

function t5ify(world) {
  // Allegiance Fixups
  world.Allegiance = ({
    'Ga': '3EoG',
    'Jm': 'JMen',
    'JP': 'JuPr',
    'VN': 'VDrN',
    'VQ': 'VYoe',
    'VT': 'VTrA',
    'Zc': 'CsZh',
    'Zh': 'ZhMe'
  })[world.Allegiance] || world.Allegiance;

  // Derived from:
  // Hlakhoi Ealiyasiyw Staihaia'yo Iwahfuah Riftspan Reaches
  const NA_TABLE = [
    {freq: 29, entry: 'NaAs'},
    {freq:  2, entry: 'NaXX'}
  ];
  const AS_TABLE = [
    {freq: 274, entry: 'AsSc'},
    {freq: 274, entry: 'AsMw'},
    {freq: 204, entry: 'AsTv'},
    {freq: 195, entry: 'AsVc'},
    {freq: 156, entry: 'AsWc'},
    {freq:  76, entry: 'AsT9'},
    {freq:  70, entry: 'AsT1'},
    {freq:  68, entry: 'AsT0'},
    {freq:  64, entry: 'AsT6'},
    {freq:  60, entry: 'AsT4'},
    {freq:  56, entry: 'AsT8'},
    {freq:  56, entry: 'AsT3'},
    {freq:  53, entry: 'AsT2'},
    {freq:  48, entry: 'AsT5'},
    {freq:  45, entry: 'AsT7'},
    {freq:  26, entry: 'AsXX'},
    {freq:  10, entry: 'AsTz'}
  ];
  if (world.Allegiance === 'Na') world.Allegiance = world.Pop === 0 ? 'NaXX' : pickFromFrequencyTable(NA_TABLE);
  if (world.Allegiance === 'As') world.Allegiance = pickFromFrequencyTable(AS_TABLE);

  if (/^As/.test(world.Allegiance) && /[NS]/.test(world.Bases)) {
    world.Bases = /^AsT/.test(world.Allegiance) ? 'T' : 'R';
  }

  // Importance Extension
  world.Importance = 0 +
    (world.St === 'A' || world.St === 'B' ? 1 : 0) +
    (world.St === 'D' || world.St === 'E' || world.St === 'X' ? -1 : 0) +
    (world.TL >= 10 ? 1 : 0) +
    (world.TL >= 16 ? 1 : 0) +
    (world.TL <= 8 ? -1 : 0) +
    (world.Pop <= 6 ? -1 : 0) +
    (world.Pop >= 9 ? 1 : 0) +
    (world.Ag ? 1 : 0) +
    (world.Ri ? 1 : 0) +
    (world.In ? 1 : 0) +
    (world.Bases === 'NS' || world.Bases === 'NW' || world.Bases === 'W' ||
     world.Bases === 'X' || world.Bases === 'D' || world.Bases === 'RT' ||
     world.Bases === 'CK' || world.Bases === 'KM' ? 1 : 0);
  world._Ix_ = `{ ${world.Importance} }`;

  // Economics Extension
  if (world['(Ex)'] && world['(Ex)'] !== '(000+1)') {
    var ex = world['(Ex)'];
    world.Resources = fromEHex(ex.substr(1, 1));
    world.Labor = fromEHex(ex.substr(2, 1));
    world.Infrastructure = fromEHex(ex.substr(3, 1));
    world.Efficiency = parseFloat(ex.substr(4, 2));
  } else {
    world.Resources =
      Math.max(0,
               roll2D() + (world.TL >= 8 ? world.GG + world.Belts : 0));
    world.Labor =
      Math.max(0,
               world.Pop - 1);
    world.Infrastructure =
      Math.max(0,
               world.Ba/* || world.Di*/ ? 0 : // Per Errata "Di should not impact Infrastructure"
               world.Lo ? 1 :
               world.Ni ? roll1D()  + world.Importance:
               roll2D() + world.Importance);
    world.Efficiency = flux();
  }
  world._Ex_ = `(${toEHex(world.Resources)}${toEHex(world.Labor)}${toEHex(world.Infrastructure)}${toSInt(world.Efficiency)})`;

  // Cultural Extension
  if (world['[Cx]'] && world['[Cx]'] !== '[0000]') {
    var cx = world['[Cx]'];
    world.Heterogeneity = fromEHex(cx.substr(1, 1));
    world.Acceptance = fromEHex(cx.substr(2, 1));
    world.Strangeness = fromEHex(cx.substr(3, 1));
    world.Symbols = fromEHex(cx.substr(4, 1));
  } else if (world.Pop === 0) {
    world.Heterogeneity = 0;
    world.Acceptance = 0;
    world.Strangeness = 0;
    world.Symbols = 0;
  } else {
    world.Heterogeneity = world.Pop === 0 ? 0 : Math.max(1, world.Pop + flux());
    world.Acceptance = world.Pop === 0 ? 0 : Math.max(1, world.Pop + world.Importance);
    world.Strangeness = world.Pop === 0 ? 0 : Math.max(1, flux() + 5);
    world.Symbols = world.Pop === 0 ? 0 : Math.max(1, world.TL + flux());
  }
  world._Cx_ = `[${toEHex(world.Heterogeneity)}${toEHex(world.Acceptance)}${toEHex(world.Strangeness)}${toEHex(world.Symbols)}]`;

  // Worlds
  world.Worlds = world.Worlds || world.W || (1/*MW*/ + world.GG + world.Belts + roll2D());

  world.Stars = Restellarator.fix(world.Stars) || Restellarator.generate();
}

function pickFromFrequencyTable(table) {
  const sum = table.reduce((sum, entry) => sum + entry.freq, 0);
  let n = Math.floor(Math.random() * sum);
  for (let i = 0; i < table.length; ++i) {
    const row = table[i];
      if (n < row.freq)
        return row.entry;
    n -= row.freq;
  }
  throw new Error("Logic bug");
}

function format(world) {

  function codeIf(c) {
    return world[c] ? c + ' ' : '';
  }

  return [
    world.Sector,
    world.Hex,
    '', // Name
    world.UWP,
    '', // TC
    '', // Remarks
    world.Sophonts ? world.Sophonts + ' ' : '',
    world.Details ? world.Details + ' ' : '',
    '', // Ix,
    '', // Ex,
    '', // Cx,
    '', // Nobility
    world.Bases,
    world.Zone,
    world.PBG,
    world.Allegiance,
    world.Stars,
    '', // star count
    '', // SS
    world.Worlds,
    '', // RU
    '', // Tech-Mod
    '', // St
    '', // S
    '', // A
    '', // H
    '', // P
    '', // G
    '', // L
    '', // T
    '', // P
    '', // B
    '', // G
    '', // St
    1, // Count
    '', // Ag
    '', // As
    '', // Ba
    '', // De
    '', // Fl
    '', // He
    '', // Hi
    '', // Ic
    '', // In
    '', // Lo
    '', // Na
    '', // Ni
    '', // Oc
    '', // Po
    '', // Ga
    '', // Ri
    '', // Va
    '', // Wa
    '', // Pa
    '', // Pi
    '', // Pr
    '', // Ph
    codeIf('An'),
    codeIf('Cp') + codeIf('Cs') + codeIf('Cx'),
    '', // ???
    '', // Di
    '', // Fo
    '', // Pz
    '', // Da
    '', // B
    '', // c
    '', // C
    '', // D
    '', // e
    '', // E
    '', // f
    '', // F
    '', // G TODO: Parse out?
    '', // Location
    '', // Scout ID
    '', // Stars
    world.Name,
    '', // M0 Names
    '', // ROM Names
    '', // ZS Names
    toEHex(world.Resources),
    '', // Lab
    '', // Inf
    toEHex(world.Infrastructure),
    '', // Eff
    world.Efficiency,
    '', // Importance
    toEHex(world.Heterogeneity),
    '', // Acceptance
    toEHex(world.Strangeness),
    toEHex(world.Symbols),
  ];
}



function $(s) { return document.querySelector(s); }

async function convertAndParse(text) {
  const response = await fetch(new Request('https://travellermap.com/api/sec?type=TabDelimited',
                           {method: 'POST', body: text}));
  const tab = await response.text();
  return parse(tab);
}

$('#forss').addEventListener('click', async e => {
  const worlds = await convertAndParse($('#in').value);
  worlds.forEach(world => process(world));
  worlds.forEach(world => t5ify(world));

  worlds.sort((a, b) => a.Hex < b.Hex ? -1 : b.Hex < a.Hex ? 1 : 0);

  $('#out').value = worlds
    .map(world => format(world).join('\t'))
    .join('\n') + '\n';

  window.worlds = worlds;
});

$('#sectot5').addEventListener('click', async e => {
  const worlds = await convertAndParse($('#in').value);
  worlds.forEach(world => process(world));
  worlds.forEach(world => t5ify(world));
  const cols = ['Hex', 'Name', 'UWP', 'Bases', 'Remarks', 'Zone', 'PBG',
                'Allegiance', 'Stars', '{Ix}', '(Ex)', '[Cx]', 'Nobility', 'W'];

  worlds.sort((a, b) => a.Hex < b.Hex ? -1 : b.Hex < a.Hex ? 1 : 0);

  $('#out').value =
    cols.join('\t') + '\n' +
    worlds
    .map(world => [
      world.Hex,
      world.Name,
      world.UWP,
      world.Bases,
      world.Remarks,
      world.Zone,
      world.PBG,
      world.Allegiance,
      world.Stars,
      world._Ix_,
      world._Ex_,
      world._Cx_,
      '',
      world.Worlds
    ].join('\t'))
    .join('\n');

  window.worlds = worlds;
});
