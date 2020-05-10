var Restellarator = {
  fix: function(s) {
    var stars = [], m;
    while ((m = /^([OBAFGKM][0-9] ?(?:Ia|Ib|II|III|IV|V|VI|VII|D)|[OAFGKML] ?D|D|BD|BH|D[OBAFGKM])\b\s*/.exec(s))) {
      stars.push(m[1]);
      s = s.substring(m[0].length);
    }
    if (s) console.log('leftover: ' + s);
    stars = stars.map(function(star, index) {
      if ((m = /^(D)([OBAFGKM])$/.exec(star)))
        star = m[2] + m[1];

      if ((m = (/^([OBAFGKM][0-9]) ?(Ia|Ib|II|III|IV|V|VI|VII|D)\b/.exec(star) || /^([OAFGKML]) ?(D)/.exec(star)))) {
        var spec = m[1], lum = m[2];

        // VII -> D
        if (lum === 'VII')
          lum = 'D';

        // D -> V, if not the last star (unless only star)
        if (lum === 'D' && (index < stars.length - 1 || stars.length === 1)) {
          lum = 'V';
        }

        // LD -> MD
        if (spec === 'L')
          spec = 'M';

        // Random fraction (for MD, etc)
        if (spec.length === 1)
          spec += String(Math.floor(Math.random() * 10));

        star = lum === 'D' ? lum : spec + ' ' + lum;
      }

      return star;
    });
    return stars.join(' ');
  },

  generate: function() {
    function roll1D() { return Math.floor(Math.random() * 6) + 1; }
    function roll1D10() { return Math.floor(Math.random() * 10) + 1; }
    function flux() { return roll1D() - roll1D(); }
    function inRange(a, min, max) { return min <= a && a <= max; }

    const primarySpectralFlux = flux();
    let primarySizeFlux = flux();

    function generateStar(primary) {
      function clamp(a, min, max) { return a < min ? min : a > max ? max : a; }

      const T510_TABLE = {
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

      const T5SS_TABLE = {
        Sp: { '-6': 'OB', '-5': 'A', '-4': 'F', '-3': 'G', '-2': 'G', '-1': 'K',
              0: 'K', 1: 'M', 2: 'M', 3: 'M', 4: 'M', 5: 'M', 6: 'M', 7: 'BD', 8: 'BD' },
        O: { '-6': 'Ia', '-5': 'Ia', '-4': 'Ib', '-3': 'II', '-2': 'III', '-1': 'III',
             0: 'III', 1: 'V', 2: 'V', 3: 'V', 4: 'IV', 5: 'D', 6: 'IV', 7: 'IV', 8: 'IV' },
        B: { '-6': 'Ia', '-5': 'Ia', '-4': 'Ib', '-3': 'II', '-2': 'III', '-1': 'III',
             0: 'III', 1: 'III', 2: 'V', 3: 'V', 4: 'IV', 5: 'D', 6: 'IV', 7: 'IV', 8: 'IV' },
        A: { '-6': 'Ib', '-5': 'II', '-4': 'III', '-3': 'IV', '-2': 'V', '-1': 'V',
             0: 'V', 1: 'V', 2: 'V', 3: 'V', 4: 'V', 5: 'D', 6: 'V', 7: 'V', 8: 'V' },
        F: { '-6': 'II', '-5': 'III', '-4': 'IV', '-3': 'V', '-2': 'V', '-1': 'V',
             0: 'V', 1: 'V', 2: 'V', 3: 'V', 4: 'V', 5: 'D', 6: 'IV', 7: 'V', 8: 'V' },
        G: { '-6': 'II', '-5': 'III', '-4': 'IV', '-3': 'V', '-2': 'V', '-1': 'V',
             0: 'V', 1: 'V', 2: 'V', 3: 'V', 4: 'V', 5: 'D', 6: 'IV', 7: 'V', 8: 'V' },
        K: { '-6': 'II', '-5': 'II', '-4': 'III', '-3': 'IV', '-2': 'V', '-1': 'V',
             0: 'V', 1: 'V', 2: 'V', 3: 'V', 4: 'V', 5: 'D', 6: 'V', 7: 'V', 8: 'V' },
        M: { '-6': 'II', '-5': 'II', '-4': 'III', '-3': 'V', '-2': 'V', '-1': 'V',
             0: 'V', 1: 'V', 2: 'V', 3: 'V', 4: 'V', 5: 'V', 6: 'V', 7: 'V', 8: 'V' }
      };

      const table = T5SS_TABLE;

      // "Spectral Type: Roll Flux for Primary. For all others, Primary Flux + (1D-1)."
      let spectral = table.Sp[clamp(
        primary ? primarySpectralFlux : primarySpectralFlux + roll1D() - 1, -6, 8)];

      // "Select further between O or B."
      if (spectral === 'OB') spectral = roll1D() <= 3 ? 'O' : 'B';

      // "If Spectral= BD ignore remaining rolls."
      if (spectral === 'BD') return spectral;

      let iter = 0;
      while (true) {
        if (++iter > 1000) { alert('too many iterations'); throw new Error('iterations'); }

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
        if (size === 'IV' && ((spectral === 'K' && inRange(spectralDecimal, 5, 9)) ||
                              spectral === 'M'))
          continue;

        // "Size VI not for A0-A9 and F0-F4."
        if (size === 'VI' && (spectral === 'A' ||
                              (spectral === 'F' && inRange(spectralDecimal, 0, 4))))
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
};
