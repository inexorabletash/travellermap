/*global Traveller, Util, getTextViaPOST, Handlebars, AllegianceMap, walk, neighbor, processMap, UNALIGNED, NON_ALIGNED */
document.addEventListener('DOMContentLoaded', () => {
  'use strict';
  const $ = s => document.querySelector(s);
  const $$ = s => Array.from(document.querySelectorAll(s));

  // TODO: other events
  let lastValue = null;
  setInterval(async () => {
    const value = $('#data').value;
    if (value !== lastValue) {
      $('#metadata_generated').value = '';
      lastValue = value;
      if (!value) return;
      try {
        const converted = await convertData(value);
        const sector = parseSector(converted);
        await buildMap(sector);
      } catch (reason) {
        alert(reason);
      }
    }
  }, 500);

  $('#go').addEventListener('click', () => {
    run();
  });
  $('#edges').addEventListener('click', () => {
    if (claimEdges()) {
      updateDisplay();
      updateWalks();
    }
  });
  [$('#xml'), $('#msec')].forEach(e => {
    e.addEventListener('click', updateWalks);
  });

  function convertData(text) {
    return getTextViaPOST(
      Traveller.MapService.makeURL('/api/sec', {type: 'TabDelimited'}),
      text
    );
  }

  function parseSector(tabDelimitedData) {
    const sector = {
      worlds: []
    };

    const lines = tabDelimitedData.split(/\r?\n/);
    const header = lines.shift().toLowerCase().split(/\t/);
    lines.forEach(line => {
      if (!line.length)
        return;
      const world = {};
      line.split(/\t/).forEach( (field, index) => {
        const col = header[index].replace(/[^a-z]/g, '');
        world[col] = field;
      });
      sector.worlds.push(world);
    });
    return sector;
  }

  const SECTOR_WIDTH = 32, SECTOR_HEIGHT = 40;

  const ALLEGIANCE_COLORS = {
    im: 'red',
    cs: 'pink',
    imcs: 'pink',
    as: 'yellow',
    kk: 'green',
    kc: 'lightgreen',
    va: 'olive',
    zh: 'blue',
    zc: 'lightblue',
    so: 'orange',
    socf: 'orange',
    hv: 'purple',
    hc: 'pink',
    fa: 'green',
    da: 'lightblue',
    dacf: 'lightblue',
    sw: 'blue',
    swcf: 'blue',
    bw: 'cyan',
    ga: 'yellow',
    jp: 'blue',
    na: 'transparent',
    nahu: 'transparent',
    naxx: 'transparent'
  };

  function colorFor(alleg) {
    alleg = String(alleg).toLowerCase();
    if (/Im../i.test(alleg)) return ALLEGIANCE_COLORS['im'];
    if (/Zh../i.test(alleg)) return ALLEGIANCE_COLORS['zh'];
    if (/As../i.test(alleg)) return ALLEGIANCE_COLORS['as'];
    if (/V./i.test(alleg)) return ALLEGIANCE_COLORS['va'];
    if (/J./i.test(alleg)) return ALLEGIANCE_COLORS['jp'];
    if (/A\d/i.test(alleg)) return ALLEGIANCE_COLORS['as'];
    if (/XXXX/i.test(alleg)) return ALLEGIANCE_COLORS['na'];
    if (/----/i.test(alleg)) return ALLEGIANCE_COLORS['na'];
    return ALLEGIANCE_COLORS[alleg];
  }

  function makeMapDisplay(containerElement, map, inset) {
    const sz = 15;
    const pad = 3;

    const fragments = [];
    let fragment;
    let top, left = -sz;

    for (let x = map.origin_x + inset; x < map.origin_x + map.width - inset; ++x) {
      left += (sz + pad);
      top = (x % 2 ? 0 : (sz + pad) / 2) - sz;

      for (let y = map.origin_y + inset; y < map.origin_y + map.height - inset; ++y) {
        top += (sz + pad);

        const className = 'hex' +
              ((x < 1 || x > Traveller.Astrometrics.SectorWidth ||
               y < 1 || y > Traveller.Astrometrics.SectorHeight) ? ' outside' : '');

        fragment = `<div class="${className}" data-hex="${hexLabel(x, y)}"
                         style="left: ${left}px; top: ${top}px;">
                      ${hexContents(x, y, map)}
                    </div>`;
        fragments.push(fragment);
      }
    }

    containerElement.innerHTML = fragments.join('');
    containerElement.style.width = (map.width * sz + (map.width + 1) * pad) + 'px';
    containerElement.style.height = ((map.height + 0.5) * sz + (map.height + 1.5) * pad) + 'px';

    $$('.hex').forEach(e => {
      e.onclick = () => {
        const hex = e.getAttribute('data-hex');
        if (toggleAllegiance(hex)) {
          updateDisplay();
          updateWalks();
        }
      };
    });
  }

  const xml_template = Handlebars.compile($('#xml-template').innerHTML.trim());
  const msec_template = Handlebars.compile($('#msec-template').innerHTML.trim());

  function updateWalks() {
    let borders = [];
    const bounds = map.getBounds();
    const visited = {};
    let last_alleg = UNALIGNED;

    for (let x = map.origin_x; x < map.origin_x + map.width; x += 1) {
      for (let y = map.origin_y; y < map.origin_y + map.height; y += 1) {
        const label = hexLabel(x, y);
        const alleg = map.getAllegiance(x, y);
        if (alleg !== UNALIGNED && alleg !== NON_ALIGNED &&
            alleg !== last_alleg && !(label in visited)) {

          let path = walk(map, x, y, alleg);
          path = path.map(hex => hexLabel(hex[0], hex[1]));
          path.forEach(label => { visited[label] = true; });

          // Filter out holes
          const len = path.length;
          if (len > 1) {
            const hex1 = path[len - 2], hex2 = path[len - 1];
            const x1 = Number(hex1.substring(0, 2));
            const x2 = Number(hex2.substring(0, 2));
            const y1 = Number(hex1.substring(2, 4));
            const y2 = Number(hex2.substring(2, 4));
            if ((x1 < x2) || (x1 === x2 && y1 < y2))
              continue;
          }

          borders.push({
            allegiance: alleg,
            path: path
          });
        }
        last_alleg = alleg;
      }
    }

    borders.sort((a, b) => a.allegiance < b.allegiance ? -1 : a.allegiance > b.allegiance ? 1 : 0);

    borders = borders.filter(border => border.path.length > 2);

    const template = ($('#form').elements.metatype.value === 'xml') ? xml_template : msec_template;
    $('#metadata_generated').value = template({borders:borders});
  }

  function hexLabel(x, y) {
    const pad2 = n => ('0' + String(n)).slice(-2);
    return pad2(x) + pad2(y);
  }

  const clamp = (n, min, max) => Math.max(Math.min(n, max), min);

  function hexContents(x, y, map) {
    const hexNumber = hexLabel(x, y);
    const occupied = map.isOccupied(x, y);
    const alleg = map.getTrueAllegiance(x, y);
    let color = (alleg == UNALIGNED) ? 'transparent' : colorFor(alleg);
    if (color === (void 0)) {
      color = 'gray';
      try {
        const channel = ('00' + clamp(parseInt(alleg, 36) & 0xFF, 0x60, 0xC0).toString(16))
              .slice(-2);
        color = '#' + channel + channel + channel;
      } catch (_) {}

    }
    return `` +
      `<div class='hexContents' style='background-color: ${color};'>` +
      `<span class='hexNumber'>${hexNumber}</span>` +
      (occupied ? `<span class='world'>${alleg}</span>` : "") +
      `</div>`;
  }

  const map = new AllegianceMap(SECTOR_WIDTH + 2, SECTOR_HEIGHT + 2, 0, 0);

  function buildMap(sector) {
    for (let hx = 0; hx <= Traveller.Astrometrics.SectorWidth + 1; ++hx) {
      for (let hy = 0; hy <= Traveller.Astrometrics.SectorHeight + 1; ++hy) {
          map.setOccupied(hx, hy, false);
          map.setAllegiance(hx, hy, UNALIGNED);
      }
    }

    function effectiveAllegiance(a) {
      if (/^Im..$/.test(a)) return 'Im';
      if (/^As..$/.test(a)) return 'As';
      if (/^Cs..$/.test(a)) return 'Na';
      if (/^Na..$/.test(a)) return 'Na';
      if (/^XXXX$/.test(a)) return 'Na';
      if (/^---$/.test(a)) return 'Na';
      if (/^ *$/.test(a)) return 'Na';

      switch (a) {
        case 'Cs': // Imperial Client
        case 'Cz': // Zhodani Client
        case 'Hc': // Hiver Client
        case 'Kc': // K'kree Client
        return 'Na';

        case 'A0': // Aslan Tlauku
        case 'A1':
        case 'A2':
        case 'A3':
        case 'A4':
        case 'A5':
        case 'A6':
        case 'A7':
        case 'A8':
        case 'A9':
        return 'As';

        case '--':
        return 'Na';
      }

      return a;
    }

    sector.worlds.forEach(world => {
      const hx = Number(world.hex.substring(0, 2));
      const hy = Number(world.hex.substring(2, 4));
      map.setOccupied(hx, hy, true);
      map.setAllegiance(hx, hy, effectiveAllegiance(world.allegiance), world.allegiance);
    });

    updateDisplay();
  };

  function toggleAllegiance(hex) {
    const hx = Number(hex.substring(0, 2));
    const hy = Number(hex.substring(2, 4));
    if (map.isOccupied(hx, hy))
      return false;

    const alleg = map.getAllegiance(hx, hy);
    if (alleg !== UNALIGNED) {
      map.setAllegiance(hx, hy, UNALIGNED);
      return true;
    }

    return claimByVotes(hx, hy);
  }

  function claimByVotes(hx, hy, ignoreOutSector) {
    const votes = {};
    for (let dir = 0; dir < 6; ++dir) {
      const nxy = neighbor(hx, hy, dir);
      if (ignoreOutSector &&
          (nxy[0] < 1 || nxy[0] > Traveller.Astrometrics.SectorWidth ||
           nxy[1] < 1 || nxy[1] > Traveller.Astrometrics.SectorHeight))
        continue;

      try {
        const nalleg = map.getAllegiance(nxy[0], nxy[1]);
        if (nalleg === UNALIGNED)
          continue;
        if (votes.hasOwnProperty(nalleg))
          ++votes[nalleg];
        else
          votes[nalleg] = 1;
      } catch(_) {}
    }
    const top = Object
          .keys(votes)
          .reduce(
            (cur, key) => votes[key] > cur[1] ? [key, votes[key]] : cur,
            ['--', 0])[0];
    if (top === UNALIGNED)
      return false;
    map.setAllegiance(hx, hy, top);
    return true;
  }

  function claimEdges() {
    let dirty = false;
    for (let hx = 0; hx <= Traveller.Astrometrics.SectorWidth + 1; ++hx) {
      dirty = claimByVotes(hx, 0, true) || dirty;
      dirty = claimByVotes(hx, Traveller.Astrometrics.SectorHeight + 1, true) || dirty;
    }
    for (let hy = 0; hy <= Traveller.Astrometrics.SectorHeight + 1; ++hy) {
      dirty = claimByVotes(0, hy, true) || dirty;
      dirty = claimByVotes(Traveller.Astrometrics.SectorWidth + 1, hy, true) || dirty;
    }
    return dirty;
  }

  function updateDisplay() {
    // TODO: Do this incrementally
    makeMapDisplay($('#map'), map, 0);
  }

  function run() {
    processMap(
      map,
      () => {
        status('');
        updateDisplay();
        updateWalks();
      },
      (message) => {
        status(message);
        updateDisplay();
        updateWalks();
      });
  }

  function status(message) {
    $('#status').innerHTML = Util.escapeHTML(message);
  }
});
