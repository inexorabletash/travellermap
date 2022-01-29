/*global Traveller, Util, getTextViaPOST, Handlebars, AllegianceMap, walk, neighbor, processMap, UNALIGNED, NON_ALIGNED */
document.addEventListener('DOMContentLoaded', function() {
  'use strict';
  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return document.querySelectorAll(s); };

  // TODO: other events
  var lastValue = null;
  setInterval(function() {
    var value = $('#data').value;
    if (value !== lastValue) {
      $('#metadata_generated').value = '';
      lastValue = value;
      if (!value) return;
      convertData(value)
        .then(parseSector)
        .then(buildMap)
        .catch(function(reason) { alert(reason); });
    }
  }, 500);

  $('#go').addEventListener('click', function() {
    run();
  });
  $('#edges').addEventListener('click', function() {
    if (claimEdges()) {
      updateDisplay();
      updateWalks();
    }
  });
  [$('#xml'), $('#msec')].forEach(function(e) {
    e.addEventListener('click', updateWalks);
  });

  function convertData(text) {
    return getTextViaPOST(
      Traveller.MapService.makeURL('/api/sec', {type: 'TabDelimited'}),
      text
    );
  }

  function parseSector(tabDelimitedData) {
    return new Promise(function(resolve, reject) {
      var sector = {
        worlds: []
      };

      var lines = tabDelimitedData.split(/\r?\n/);
      var header = lines.shift().toLowerCase().split(/\t/);
      lines.forEach(function (line) {
        if (!line.length)
          return;
        var world = {};
        line.split(/\t/).forEach(function (field, index) {
          var col = header[index].replace(/[^a-z]/g, '');
          world[col] = field;
        });
        sector.worlds.push(world);
      });

      resolve(sector);
    });
  }

  var SECTOR_WIDTH = 32, SECTOR_HEIGHT = 40;

  var ALLEGIANCE_COLORS = {
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
    var fragments = [], fragment;
    var sz = 15;
    var pad = 3;
    var top, left = -sz;

    var x, y;
    for (x = map.origin_x + inset; x < map.origin_x + map.width - inset; ++x) {
      left += (sz + pad);
      top = (x % 2 ? 0 : (sz + pad) / 2) - sz;

      for (y = map.origin_y + inset; y < map.origin_y + map.height - inset; ++y) {
        top += (sz + pad);

        var className = 'hex' +
              ((x < 1 || x > Traveller.Astrometrics.SectorWidth ||
               y < 1 || y > Traveller.Astrometrics.SectorHeight) ? ' outside' : '');

        fragment = '<div class="' + className +
          '" data-hex="' + hexLabel(x, y) + '" style="left: ' + left + 'px; top: ' + top + 'px;">';
        fragment += hexContents(x, y, map);
        fragment += '<' + '/div>';

        fragments.push(fragment);

      }
    }

    containerElement.innerHTML = fragments.join('');
    containerElement.style.width = (map.width * sz + (map.width + 1) * pad) + 'px';
    containerElement.style.height = ((map.height + 0.5) * sz + (map.height + 1.5) * pad) + 'px';

    [].slice.call($$('.hex')).forEach(function(e) {
      e.onclick = function() {
        var hex = this.getAttribute('data-hex');
        if (toggleAllegiance(hex)) {
          updateDisplay();
          updateWalks();
        }
      };
    });
  }

  var xml_template = Handlebars.compile($('#xml-template').innerHTML.trim());
  var msec_template = Handlebars.compile($('#msec-template').innerHTML.trim());

  function updateWalks() {
    var borders = [];
    var bounds = map.getBounds();
    var allegiance;
    var x, y, visited = {}, label, alleg, last_alleg, path;

    for (x = map.origin_x; x < map.origin_x + map.width; x += 1) {
      var lastalleg = UNALIGNED;
      for (y = map.origin_y; y < map.origin_y + map.height; y += 1) {
        label = hexLabel(x, y);
        alleg = map.getAllegiance(x, y);
        if (alleg !== UNALIGNED && alleg !== NON_ALIGNED &&
            alleg !== last_alleg && !(label in visited)) {

          path = walk(map, x, y, alleg);
          path = path.map(function(hex) { return hexLabel(hex[0], hex[1]); });
          path.forEach(function(label) { visited[label] = true; });

          // Filter out holes
          var len = path.length;
          if (len > 1) {
            var hex1 = path[len - 2], hex2 = path[len - 1];
            var x1 = Number(hex1.substring(0, 2));
            var x2 = Number(hex2.substring(0, 2));
            var y1 = Number(hex1.substring(2, 4));
            var y2 = Number(hex2.substring(2, 4));
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

    borders.sort(function(a, b) { return a.allegiance < b.allegiance ? -1 : a.allegiance > b.allegiance ? 1 : 0; });

    borders = borders.filter(function(border) { return border.path.length > 2; });

    var template = ($('#form').elements.metatype.value === 'xml') ? xml_template : msec_template;
    $('#metadata_generated').value = template({borders:borders});
  }

  function hexLabel(x, y) {
    function pad2(n) { return ('0' + String(n)).slice(-2); }
    return pad2(x) + pad2(y);
  }

  function hexContents(x, y, map) {
    var hexNumber = hexLabel(x, y);
    var occupied = map.isOccupied(x, y);
    var alleg = map.getTrueAllegiance(x, y);
    var color = (alleg == UNALIGNED) ? 'transparent' : colorFor(alleg);
    if (color === (void 0)) {
      color = 'gray';
      try {
        var channel = ('00' + (parseInt(alleg, 36) & 0xFF).toString(16)).slice(-2);
        color = '#' + channel + channel + channel;
      } catch (_) {}

    }
    return "<div class='hexContents' style='background-color: " + color + ";'>" +
      "<span class='hexNumber'>" + hexNumber + "<" + "/span>" +
      (occupied ? "<span class='world'>" + alleg + "</span>" : "") +
      "<" + "/div>";
  }

  var map = new AllegianceMap(SECTOR_WIDTH + 2, SECTOR_HEIGHT + 2, 0, 0);

  function buildMap(sector) {
    for (var hx = 0; hx <= Traveller.Astrometrics.SectorWidth + 1; ++hx) {
      for (var hy = 0; hy <= Traveller.Astrometrics.SectorHeight + 1; ++hy) {
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

    sector.worlds.forEach(function(world) {
      var hx = Number(world.hex.substring(0, 2));
      var hy = Number(world.hex.substring(2, 4));
      map.setOccupied(hx, hy, true);
      map.setAllegiance(hx, hy, effectiveAllegiance(world.allegiance), world.allegiance);
    });

    updateDisplay();
  };

  function toggleAllegiance(hex) {
    var hx = Number(hex.substring(0, 2));
    var hy = Number(hex.substring(2, 4));
    if (map.isOccupied(hx, hy))
      return false;

    var alleg = map.getAllegiance(hx, hy);
    if (alleg !== UNALIGNED) {
      map.setAllegiance(hx, hy, UNALIGNED);
      return true;
    }

    return claimByVotes(hx, hy);
  }

  function claimByVotes(hx, hy, ignoreOutSector) {
    var votes = {};
    for (var dir = 0; dir < 6; ++dir) {
      var nxy = neighbor(hx, hy, dir);
      if (ignoreOutSector &&
          (nxy[0] < 1 || nxy[0] > Traveller.Astrometrics.SectorWidth ||
           nxy[1] < 1 || nxy[1] > Traveller.Astrometrics.SectorHeight))
        continue;

      try {
        var nalleg = map.getAllegiance(nxy[0], nxy[1]);
        if (nalleg === UNALIGNED)
          continue;
        if (votes.hasOwnProperty(nalleg))
          ++votes[nalleg];
        else
          votes[nalleg] = 1;
      } catch(_) {}
    }
    var top = Object.keys(votes).reduce(function(cur, key) {
      return votes[key] > cur[1] ? [key, votes[key]] : cur;
    }, ['--', 0])[0];
    if (top === UNALIGNED)
      return false;
    map.setAllegiance(hx, hy, top);
    return true;
  }

  function claimEdges() {
    var dirty = false;
    for (var hx = 0; hx <= Traveller.Astrometrics.SectorWidth + 1; ++hx) {
      dirty = claimByVotes(hx, 0, true) || dirty;
      dirty = claimByVotes(hx, Traveller.Astrometrics.SectorHeight + 1, true) || dirty;
    }
    for (var hy = 0; hy <= Traveller.Astrometrics.SectorHeight + 1; ++hy) {
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
      function complete() {
        status('');
        updateDisplay();
        updateWalks();
      },
      function progress(message) {
        status(message);
        updateDisplay();
        updateWalks();
      });
  }

  function status(message) {
    $('#status').innerHTML = Util.escapeHTML(message);
  }
});
