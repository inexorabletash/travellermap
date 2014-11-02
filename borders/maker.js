document.addEventListener('DOMContentLoaded', function() {
  'use strict';
  var $ = function(s) { return document.querySelector(s); };

  // TODO: other events
  var lastValue = null;
  setInterval(function() {
    var value = $('#data').value;
    if (value !== lastValue) {
      $('#metadata_generated').value = '';
      lastValue = value;
      if (!value) return;
      convertData(value).then(parseSector).then(buildMap);
    }
  }, 500);

  $('#go').addEventListener('click', function() {
    run();
  });

  // |data| can be string (payload) or object (key/value form data)
  // Returns Promise<string>
  function getTextViaPOST(url, data) {
    return new Promise(function(resolve, reject) {
      var xhr = new XMLHttpRequest(), async = true;
      xhr.open('POST', url, async);
      xhr.setRequestHeader('Content-Type', 'text/plain'); // Safari doesn't infer this.
      xhr.send(data);
      xhr.onreadystatechange = function() {
        if (xhr.readyState !== XMLHttpRequest.DONE) return;
        if (xhr.status === 200)
          resolve(xhr.response);
        else
          reject(xhr.responseText);
      };
    });
  }

  function convertData(text) {
    return new Promise(function(resolve, reject) {
      resolve(getTextViaPOST(
        makeURL(SERVICE_BASE + '/api/sec', {type: 'TabDelimited'}),
        text
      ));
    });
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
    as: 'yellow',
    kk: 'green',
    kc: 'lightgreen',
    va: 'olive',
    zh: 'blue',
    zc: 'lightblue',
    so: 'orange',
    hv: 'purple',
    hc: 'pink',
    fa: 'green',
    da: 'lightblue',
    sw: 'blue',
    bw: 'cyan',
    ga: 'yellow',
    jp: 'blue',
    na: 'transparent'
  };

  function colorFor(alleg) {
    alleg = String(alleg).toLowerCase();
    if (/V./i.test(alleg)) return ALLEGIANCE_COLORS['va'];
    if (/J./i.test(alleg)) return ALLEGIANCE_COLORS['jp'];
    if (/A\d/i.test(alleg)) return ALLEGIANCE_COLORS['as'];
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

        fragment = '<div class="hex" id="hex_' + hexLabel(x, y) + '" style="left: ' + left + 'px; top: ' + top + 'px;">';
        fragment += hexContents(x, y, map);
        fragment += '<' + '/div>';

        fragments.push(fragment);

      }
    }

    containerElement.innerHTML = fragments.join('');
    containerElement.style.width = (map.width * sz + (map.width + 1) * pad) + 'px';
    containerElement.style.height = ((map.height + 0.5) * sz + (map.height + 1.5) * pad) + 'px';
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
        if (alleg !== UNALIGNED && alleg !== NON_ALIGNED && alleg !== last_alleg && !(label in visited)) {

          path = walk(map, x, y, alleg);
          path = path.map(function(hex) { return hexLabel(hex[0], hex[1]); });
          path.forEach(function(label) { visited[label] = true; });

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

  var map = new AllegianceMap(SECTOR_WIDTH, SECTOR_HEIGHT);

  function buildMap(sector) {
    for (var hx = 1; hx <= 32; ++hx) {
      for (var hy = 1; hy <= 40; ++hy) {
          map.setOccupied(hx, hy, false);
          map.setAllegiance(hx, hy, UNALIGNED);
      }
    }

    function effectiveAllegiance(a) {
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

  function updateDisplay() {
    // TODO: Do this incrementally
    makeMapDisplay($('#map'), map, 0);
  }

  function run() {
    processMap(
      map,
      function complete() {
        updateDisplay();
        updateWalks();
      },
      function progress(message) {
        updateDisplay();
        updateWalks();
      });
  }

});
