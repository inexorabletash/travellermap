'use strict';

const EHEX = '0123456789ABCDEFGHJKLMNPQRSTUV';
function fromEHex(c) { return EHEX.indexOf(c); }
function toEHex(n) { return EHEX.substr(n, 1); }

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
  const lines = text.split(/\r\n|\r|\n/);
  const header = lines.shift().split('\t');
  const worlds = lines.map(line => {
    const world = {};
    const cols = line.split('\t');
    cols.forEach((value, index) => {
      world[header[index]] = value;
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

  // Planetary
  world.As = world.Siz === 0;
  world.De = world.Atm.in(2, 9) && world.Hyd === 0;
  world.Fl = world.Atm.in(10, 12) && world.Hyd.in(1, 10);
  world.Ga = world.Siz.in(6, 8) && world.Atm.in([5, 6, 8]) && world.Hyd.in(5, 7);
  world.He = world.Siz.in([3, 4, 5, 9, 10, 11, 12])
    && world.Atm.in([2, 4, 7, 9, 10, 11, 12]) && world.Hyd.in(0, 2);
  world.Ic = world.Atm.in(0, 1) && world.Hyd.in(1, 10);
  world.Oc = world.Siz.in(10, 12) && world.Hyd === 10;
  world.Va = world.Atm === 0;
  world.Wa = world.Siz.in(5, 9) && world.Hyd === 10;

  ['As', 'De', 'Fl', 'Ga', 'He', 'Ic', 'Oc', 'Va', 'Wa'].forEach(c => {
    codeSet.delete(c);
  });


  // Population
  world.Di = world.Pop === 0 && world.Gov === 0 && world.Law === 0 && world.TL > 0;
  world.Ba = world.Pop === 0 && world.Gov === 0 && world.Law === 0 && world.TL === 0;
  world.Lo = world.Pop.in(1, 3);
  world.Ni = world.Pop.in(4, 6);
  world.Ph = world.Pop.in === 8;
  world.Hi = world.Pop.in(9, 12);

  ['Di', 'Ba', 'Lo', 'Ni', 'Ph', 'Hi'].forEach(c => {
    codeSet.delete(c);
  });


  // Economic
  world.Pa = world.Atm.in(4, 9) && world.Hyd.in(4, 8) && world.Pop.in([4,8]);
  world.Ag = world.Atm.in(4, 9) && world.Hyd.in(4, 8) && world.Pop.in(5, 7);
  world.Na = world.Atm.in(0, 3) && world.Hyd.in(0, 3) && world.Pop.in(6, 12);
  world.Pi = world.Atm.in([0,1,2,4,7,9]) && world.Pop.in(7, 8);
  world.In = world.Atm.in([0,1,2,4,7,9]) && world.Pop.in(9, 12);
  world.Po = world.Atm.in(2, 5) && world.Hyd.in(0, 3);
  world.Pr = world.Atm.in([6,8]) && world.Pop.in([5,9]);
  world.Ri = world.Atm.in([6,8]) && world.Pop.in(6, 8);

  ['Pa', 'Ag', 'Na', 'Pi', 'In', 'Po', 'Pr', 'Ri'].forEach(c => {
    codeSet.delete(c);
  });


  // Climate
  ['Fr', 'Ho', 'Co', 'Lk', 'Tr', 'Tu', 'Tz'].forEach(c => {
    world[c] = codes.includes(c);
    if (world[c]) codeSet.delete(c);
  });

  // Secondary
  ['Fa', 'Mi', 'Mr', 'Px', 'Pe', 'Re'].forEach(c => {
    world[c] = codes.includes(c);
    if (world[c]) codeSet.delete(c);
  });

  // Political
  ['Cp', 'Cs', 'Cx', 'Cy'].forEach(c => {
    world[c] = codes.includes(c);
    if (world[c]) codeSet.delete(c);
  });

  // Special
  ['Sa', 'Fo', 'Pz', 'Da', 'Ab', 'An'].forEach(c => {
    world[c] = codes.includes(c);
    if (world[c]) codeSet.delete(c);
  });

  world.Sophonts = codes.filter(
    c =>
      /^....[0-9W]$/.test(c) ||
      /^\(.*\)[0-9W]?$/.test(c)
  ).map(c => {
    codeSet.delete(c);
    return c;
  }).join(' ');


  world.Details = codes.filter(
    c => /^Rs/.test(c) || /^Mr.+/.test(c) || /^O:/.test(c)
  ).map(c => {
    codeSet.delete(c);
    return c;
  }).join(' ');
  [
    'Fr', 'Ho', 'Co', 'Lk', 'Tr', 'Tu', 'Tz',
    'Fa', 'Mi', 'Mr', 'Px', 'Pe', 'Re',
    'Sa', 'Fo', 'Pz', 'Da', 'Ab'
    // NOTE: Not An
  ].forEach(c => {
    if (world[c]) world.Details += ' ' + c;
  });

  // Stellar configuration - just remove
  codes.filter(c => /^S[0-9A-F]+/.test(c)).forEach(c => {
    codeSet.delete(c);
  });

  // Report unmatched codes
  if (codeSet.size)
    console.warn(`Unmatched codes (${world.Hex}): ` + Array.from(codeSet).map(s=>JSON.stringify(s)).join(' '));
}

function t5ify(world) {

  // Importance Extension
  world.Importance = 0 +
    (world.St === 'A' || world.St === 'B' ? 1 : 0) +
    (world.St === 'D' || world.St === 'E' || world.St === 'X' ? -1 : 0) +
    (world.TL >= 16 ? 1 : 0) +
    (world.TL >= 10 ? 1 : 0) +
    (world.TL <= 8 ? -1 : 0) +
    (world.Ag ? 1 : 0) +
    (world.Hi ? 1 : 0) +
    (world.In ? 1 : 0) +
    (world.Ri ? 1 : 0) +
    (world.Pop <= 6 ? -1 : 0) +
    (world.Bases.includes('N') && world.Bases.includes('S') ? 1 : 0) +
    (world.Bases.includes('W') ? 1 : 0);

  // Economics Extension
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
             world.Ni ? roll1D() :
             roll2D() + world.Importance);
  world.Efficiency = flux();

  // Cultural Extension
  world.Homogeneity = Math.max(1, world.Pop + flux());
  world.Acceptance = Math.max(1, world.Pop + world.Importance);
  world.Strangeness = Math.max(1, flux() + 5);
  world.Symbols = Math.max(1, world.TL + flux());

  // Worlds
  world.Worlds = 1/*MW*/ + world.GG + world.Belts + roll2D();

  // Allegiance Fixups
  /*
  if (world.Allegiance === 'Zh')
    world.Allegiance = 'ZhJp';
  else if (world.Allegiance === 'Ax')
    world.Allegiance = 'ZhAx';
  else if (world.Allegiance === 'Dr') {
    world.Allegiance = 'ZhJp';
    world.Sophonts += 'DroyW ';
  }*/
  world.Allegiance = ({
    'Ga': '3EoG',
    'Jm': 'JMen',
    'Na': 'NaHu',
    'JP': 'JuPr',

    // TODO: Add to docs
    'VN': 'VDrN',
    'VQ': 'VYoe',
    'VT': 'VTrA'
  })[world.Allegiance] || world.Allegiance;

  world.Stars = world.Stars || generateStars();
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
    toEHex(world.Homogeneity),
    '', // Acceptance
    toEHex(world.Strangeness),
    toEHex(world.Symbols),
  ];
}

function $(s) { return document.querySelector(s); }

$('#go').addEventListener('click', e => {

  const worlds = parse($('#in').value);
  worlds.forEach(world => process(world));
  worlds.forEach(world => t5ify(world));
  $('#out').value = worlds
    .map(world => format(world).join('\t'))
    .join('\n');

  window.worlds = worlds;
});


function generateStars() {

  let primarySpectralFlux = flux();
  let primarySizeFlux = flux();

  function generateStar(primary) {
    function clamp(a, min, max) { return a < min ? min : a > max ? max : a; }

    const table = {
      Sp: { '-6': 'OB', '-5': 'A', '-4': 'A', '-3': 'F', '-2': 'F', '-1': 'G',
            0: 'G', 1: 'K', 2: 'K', 3: 'M', 4: 'M', 5: 'M', 6: 'BD', 7: 'BD', 8: 'BD' },
      O: { '-6': 'Ia', '-5': 'Ia', '-4': 'Ib', '-3': 'II', '-2': 'III', '-1': 'III',
            0: 'III', 1: 'V', 2: 'V', 3: 'V', 4: 'IV', 5: 'D', 6: 'IV', 7: 'IV', 8: 'IV' },
      B: { '-6': 'Ia', '-5': 'Ia', '-4': 'Ib', '-3': 'II', '-2': 'III', '-1': 'III',
            0: 'III', 1: 'III', 2: 'V', 3: 'V', 4: 'IV', 5: 'D', 6: 'IV', 7: 'IV', 8: 'IV' },
      A: { '-6': 'Ia', '-5': 'Ia', '-4': 'Ib', '-3': 'II', '-2': 'III', '-1': 'IV',
            0: 'V', 1: 'V', 2: 'V', 3: 'V', 4: 'V', 5: 'D', 6: 'V', 7: 'V', 8: 'V' },
      F: { '-6': 'II', '-5': 'II', '-4': 'III', '-3': 'IV', '-2': 'V', '-1': 'V',
            0: 'V', 1: 'V', 2: 'V', 3: 'V', 4: 'VI', 5: 'D', 6: 'VI', 7: 'VI', 8: 'VI' },
      G: { '-6': 'II', '-5': 'II', '-4': 'III', '-3': 'IV', '-2': 'V', '-1': 'V',
            0: 'V', 1: 'V', 2: 'V', 3: 'V', 4: 'VI', 5: 'D', 6: 'VI', 7: 'VI', 8: 'VI' },
      K: { '-6': 'II', '-5': 'II', '-4': 'III', '-3': 'IV', '-2': 'V', '-1': 'V',
            0: 'V', 1: 'V', 2: 'V', 3: 'V', 4: 'VI', 5: 'D', 6: 'VI', 7: 'VI', 8: 'VI' },
      M: { '-6': 'II', '-5': 'II', '-4': 'II', '-3': 'II', '-2': 'III', '-1': 'V',
            0: 'V', 1: 'V', 2: 'V', 3: 'V', 4: 'VI', 5: 'D', 6: 'VI', 7: 'VI', 8: 'VI' }
    };

    while (true) {
      // "Spectral Type: Roll Flux for Primary. For all others, Primary Flux + (1D-1)."
      let spectral = table.Sp[clamp(
        primary ? primarySpectralFlux : primarySpectralFlux + roll1D() - 1, -6, 8)];

      // "Select further between O or B."
      if (spectral === 'OB') spectral = roll1D() <= 3 ? 'O' : 'B';

      // "If Spectral= BD ignore remaining rolls."
      if (spectral === 'BD') return spectral;

      // "Spectral Decimal. Roll decimal 0 to 9."
      let spectralDecimal = roll1D10() - 1;

      // "Stellar Size. Roll Flux for Primary. For all others, use Primary Flux + (1D+2)."
      let size = table[spectral][clamp(
        primary ? primarySizeFlux : primarySizeFlux + roll1D() + 2, -6, 8)];

      // T5SS: Disallow D as primary
      if (primary && size === 'D') {
        primarySizeFlux = flux();
        continue;
      }

      // "If Size= D, ignore Spectral Decimal."
      // T5SS: Use only 'D' not 'MD' etc.
      if (size === 'D') return size;

      // "Size IV not for K5-K9 and M0-M9."
      if (size === 'IV' && ((spectral === 'K' && spectralDecimal.in(5, 9)) ||
                            spectral === 'M'))
        continue;

      // "Size VI not for A0-A9 and F0-F4."
      if (size === 'VI' && (spectral === 'A' ||
                            (spectral === 'F' && spectralDecimal.in(0, 4))))
        continue;

      return `${spectral}${spectralDecimal} ${size}`;
    }
  }

  const stars = [];

  // Primary
  stars.push(generateStar(true));

  // Companion
  if (flux() >= 3) stars.push(generateStar());

  // Close
  if (flux() >= 3) {
    stars.push(generateStar());
    // Companion
    if (flux() >= 3) stars.push(generateStar());
  }

  // Near
  if (flux() >= 3) {
    stars.push(generateStar());
    // Companion
    if (flux() >= 3) stars.push(generateStar());
  }

  // Far
  if (flux() >= 3) {
    stars.push(generateStar());
    // Companion
    if (flux() >= 3) stars.push(generateStar());
  }

  return stars.join(' ');
}
