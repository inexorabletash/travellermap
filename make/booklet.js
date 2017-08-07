/*global Traveller, Util, getTextViaPOST, getJSONViaPOST, Handlebars */ // for lint and IDEs
(function(global) {
  'use strict';

  var $ = function(s) { return document.querySelector(s); };

  function friendlyJoin(l) {
    if (l.length === 0)
      return null;
    if (l.length === 1)
      return l[0];
    var last = l.pop();
    return l.join(', ') + ' and ' + last;
  }

  function friendlyNumber(n) {
    if (n >= 1e12)
      return Math.round(n / 1e11) / 10 + ' trillion';
    if (n >= 1e9)
      return Math.round(n / 1e8) / 10 + ' billion';
    if (n >= 1e6)
      return Math.round(n / 1e5) / 10 + ' million';
    if (n >= 1e3)
      return Math.round(n / 1e2) / 10 + ' thousand';
    return n;
  }

  function capitalize(s) {
    return s.substring(0, 1).toUpperCase() + s.substring(1);
  }

  function titleCaps(s) {
    return s.split(/ /g).map(capitalize).join(' ');
  }

  function firstOrNull(a) {
    if (a && a.length > 0)
      return a[0];
    return null;
  }

  var finished = false;
  function status(string, pending) {
    if (finished) return;
    var statusElement = $('#status'),
        statusText = $('#statusText'),
        statusImage = $('#statusImage');

    if (!string && !pending) {
      statusElement.style.display = 'none';
      return;
    }

    finished = !pending;
    string = String(string);
    statusElement.style.display = '';
    statusText.innerHTML = Util.escapeHTML(string);
    statusImage.style.display = pending ? '' : 'none';
  }

  function parseSector(tabDelimitedData, metadata) {
    var i, sector = {
      metadata: metadata,
      worlds: [],
      subsectors: []
    };

    for (i = 0; i < 16; i += 1) {
      var index = String.fromCharCode('A'.charCodeAt(0) + i);
      var ss = firstOrNull((metadata.Subsectors || [])
            .filter(function(s) { return s.Index === index; }));

      sector.subsectors[i] = {
        worlds: [],
        index: index,
        name: ss ? ss.Name : ''
      };
    }

    var lines = tabDelimitedData.split(/\r?\n/);
    var header = lines.shift().toLowerCase().split('\t');
    lines.forEach(function (line) {
      if (!line.length)
        return;

      var world = {};
      line.split('\t').forEach(function (field, index) {
        var col = header[index].replace(/[^a-z]/g, '');
        world[col] = field;
      });
      var exp = Traveller.fromHex(world.uwp.charAt(4)),
          mult = Traveller.fromHex(world.pbg.charAt(0));
      world.population = exp >= 0 && mult >= 0 ? Math.pow(10, exp) * mult : 0;
      if (world.population >= 1e9)
        world.hipop = true;
      sector.worlds.push(world);
    });

    // partition worlds into subsectors
    sector.index = [];
    sector.worlds.forEach(function(world) {
      var x = Math.floor(parseInt(world.hex, 10) / 100),
          y = Math.floor(parseInt(world.hex, 10) % 100),
          ss = Math.floor((x - 1) / (Traveller.Astrometrics.SectorWidth / 4)) +
            Math.floor((y - 1) / (Traveller.Astrometrics.SectorHeight / 4)) * 4;

      sector.subsectors[ss].worlds.push(world);

      if (world.name.length) {
        sector.index.push({
          name: world.name,
          location: world.ss + world.hex
        });
      }
    });
    sector.index.sort(function (a, b) {
      return a.name.localeCompare(b.name, 'en-us');
    });
    var INDEX_COL_SIZE = 40;
    var columns = partition(sector.index, INDEX_COL_SIZE);
    var pairs = partition(columns, 3);
    if (pairs.length % 2 !== 0) {
      pairs.push([]);
    }
    sector.index_pages = partition(pairs, 2);
    var pp = 0;
    sector.index_pages.forEach(function (page) {
      page.forEach(function (half, index) {
        half.pp = ++pp;
        half.facing = index % 2 ? 'right' : 'left';
      });
    });

    if ('Allegiances' in metadata) {
      metadata.Allegiances.sort(function(a, b) { return a.Code < b.Code ? -1 : a.Code > b.Code ? 1 : 0; });
    }

    return sector;
  }

  function partition(list, count) {
    var result = [];
    var copy = list.slice();
    while (copy.length) {
      result.push(copy.splice(0, count));
    }
    return result;
  }

  function range(start, stop) {
    if (arguments.length === 1) {
      stop = start;
      start = 0;
    }

    var rv = [];
    while (start < stop) {
      rv.push(start);
      start += 1;
    }
    return rv;
  }

  function sectorData(params) {
    if ('sector' in params) {
      return Traveller.MapService.sectorDataTabDelimited(
        params.sector, {milieu: params.milieu});
    }

    if ('data' in params) {
      return getTextViaPOST(
        Traveller.MapService.makeURL('/api/sec', {type: 'TabDelimited'}),
        params.data);
    }

    return Promise.reject(new Error('No sector or data specified.'));
  }

  function sectorMetaData(params) {
    if ('sector' in params) {
      return Traveller.MapService.sectorMetaData(
        params.sector, {milieu: params.milieu});
    }

    if ('metadata' in params) {
      return getJSONViaPOST(
        Traveller.MapService.makeURL('/api/metadata', {accept: 'application/json'}),
        params.metadata
      );
    }

    return Promise.reject(new Error('No sector or metadata specified.'));
  }

  window.addEventListener('DOMContentLoaded', function() {
    var searchParams = new URL(document.location).searchParams;
    if (searchParams.has('sector')) {
      document.body.classList.add('render');
      render({
        sector: searchParams.get('sector'),
        milieu: searchParams.get('milieu'),
        style: searchParams.get('style'),
        options: searchParams.get('options')
      });
      return;
    }

    $('#compose').addEventListener('click', function(e) {
      e.preventDefault();
      var form = $('#form');
      if (!form['data'].value.length) {
        alert('Sector data must be specified.');
        return;
      }
      document.body.classList.add('render');
      document.body.classList.add('style-' + $('#data-style').value);
      render({
        data: form['data'].value,
        metadata: form['metadata'].value,
        style: form['map-style'].value
      });
    });

    status();
  });

  function render(params) {
    var hash = window.location.hash;
    window.location.hash = '';

    var options = (params.options !== undefined && params.options !== null)
          ? Number(params.options) : Traveller.MapOptions.BordersMask,
        style = params.style || 'print';

    status('Fetching data...', true);

    Promise.all(
      [sectorData(params), sectorMetaData(params)]
    ).then(function(results) {
      var data = results[0];
      var metadata = results[1];

      status('Processing data...', true);
      var pending_promises = [];

      // Step 1: Parse the sector data
      var sector = parseSector(data, metadata);

      // Step 2: Post-process the data
      if (sector.metadata.Names.length) {
        sector.name = sector.title = sector.metadata.Names[0].Text;
        if (!/^The /.test(sector.title)) {
          sector.title = 'The ' + sector.title;
          if (!/ (Sector|Marches|Reaches|Expanses|Rim)$/.test(sector.title))
            sector.title += ' Sector';
        }
      } else {
        sector.name = sector.title = 'Unnamed Sector';
      }
      if (sector.metadata.DataFile.Milieu) {
        var m = /^M(\d+)$/.exec(sector.metadata.DataFile.Milieu);
        if (m) sector.metadata.DataFile.Era = m[1];
      }
      document.title = sector.title;
      var imageURL, url_params = {
          accept: 'image/svg+xml',
          rotation: 3,
          scale: 64,
          options: options | Traveller.MapOptions.SubsectorGrid | Traveller.MapOptions.NamesMask,
          style: style
      };
      if ('sector' in params) {
        url_params.sector = params.sector;
        url_params.milieu = params.milieu;
        imageURL = Promise.resolve(Traveller.MapService.makeURL('/api/poster', url_params));
      } else {
        url_params.data = params.data;
        url_params.metadata = params.metadata;
        url_params.datauri = 1;
        imageURL = getTextViaPOST(Traveller.MapService.makeURL('/api/poster'), url_params);
      }
      pending_promises.push(imageURL.then(function(url) {
        return function() { $('img.sector-image').src = url; };
      }));

      range(16).forEach(function (i) {
        var subsector = sector.subsectors[i];

        if (subsector.name.length === 0) {
          subsector.article = 'subsector ' + String.fromCharCode('A'.charCodeAt(0) + i);
        } else if (/^District /i.test(subsector.name)) {
          subsector.article = subsector.name;
        } else if (/^The /i.test(subsector.name)) {
          subsector.article = subsector.name + ' subsector';
        } else {
          subsector.article = 'the ' + subsector.name + ' subsector';
        }
        subsector.title = titleCaps(subsector.article);

        function neighbor(n, dx, dy) {
          var x = (n % 4) + dx,
              y = Math.floor(n / 4) + dy;

          if (x < 0 || x >= 4 || y < 0 || y >= 4) {
            return '\xA0'; // Make sure the space doesn't collapse
          } else {
            n = x + (4 * y);
            return sector.subsectors[n].name;
          }
        }

        subsector.neighbor = [];
        subsector.neighbor[0] = neighbor(i, 0, -1);
        subsector.neighbor[1] = neighbor(i, 1, 0);
        subsector.neighbor[2] = neighbor(i, 0, 1);
        subsector.neighbor[3] = neighbor(i, -1, 0);

        subsector.population = 0;
        subsector.maxpop = null;
        subsector.maxtl = null;
        subsector.capital = null;
        subsector.unexplored = 0;

        subsector.worlds.forEach(function (world) {
          subsector.population += world.population;

          if (world.name !== '') {
            if (!subsector.maxpop || subsector.maxpop.population < world.population) {
              subsector.maxpop = world;
            }

            if (!subsector.maxtl || Traveller.fromHex(subsector.maxtl[0].uwp.charAt(8)) < Traveller.fromHex(world.uwp.charAt(8))) {
              subsector.maxtl = [world];
            } else if (Traveller.fromHex(subsector.maxtl[0].uwp.charAt(8)) === Traveller.fromHex(world.uwp.charAt(8))) {
              subsector.maxtl.push(world);
            }

            if (world.remarks.split(/ /).some(function (x) { return x === 'Cp'; })) {
              subsector.capital = world;
            }
          }

          if (world.uwp === 'XXXXXXX-X')
            subsector.unexplored += 1;
        });

        subsector.blurb = [];
        if (subsector.worlds.length > 1 && subsector.worlds.length > subsector.unexplored) {
          subsector.blurb.push(capitalize(subsector.article) + ' contains ' +
                               subsector.worlds.length + ' worlds with a ' +
                               (subsector.unexplored > 0 ? 'known population' : 'population') +
                               ' of ' +
                               friendlyNumber(subsector.population) + '.');

          if (subsector.maxpop && subsector.maxpop.population > 0) {
            subsector.blurb.push('The highest population is ' +
                                 friendlyNumber(subsector.maxpop.population) + ', at ' +
                                 subsector.maxpop.name + '.');
          }
        } else if (subsector.worlds.length === 1 && subsector.maxpop) {
          subsector.blurb.push(capitalize(subsector.article) + ' contains one world, ' +
                               subsector.maxpop.name + ', with a population of ' +
                               friendlyNumber(subsector.maxpop.population) + '.');
        } else if (subsector.worlds.length === 1) {
          subsector.blurb.push(capitalize(subsector.article) + ' contains one barren world.');
        } else if (subsector.worlds.length === 0) {
          subsector.blurb.push(capitalize(subsector.article) + ' contains no charted worlds.');
        }

        if (subsector.unexplored > 0) {
          subsector.blurb.push(capitalize(subsector.article) +
                               ' contains ' + subsector.unexplored + ' unexplored worlds.');
        }

        if (subsector.maxtl && subsector.maxtl.length > 0) {
          subsector.blurb.push('The highest tech level is ' + subsector.maxtl[0].uwp.charAt(8) +
                               ' at ' + friendlyJoin(subsector.maxtl.map(function (world) {
                                 return world.name; })) + '.');
        }

        if (subsector.capital) {
          subsector.blurb.push('The subsector capital is at ' + subsector.capital.name + '.');
        }

        subsector.blurb = subsector.blurb.join(' ');
        var imageURL, url_params = {
            accept: 'image/svg+xml',
            subsector: subsector.index,
            scale: 64,
            options: options,
            style: style
        };
        if ('sector' in params) {
          url_params.sector = params.sector;
          url_params.milieu = params.milieu;
          imageURL = Promise.resolve(Traveller.MapService.makeURL('/api/poster', url_params));
        } else {
          url_params.data = params.data;
          url_params.metadata = params.metadata;
          url_params.datauri = 1;
          imageURL = getTextViaPOST(Traveller.MapService.makeURL('/api/poster'), url_params);
        }
        pending_promises.push(imageURL.then(function(url) {
          return function() {
            var img = $('#ss' + subsector.index  + ' img.subsector-image');
            img.src = url;
            window['img_' + subsector.index] = img;
          };
        }));

        subsector.density = (subsector.worlds.length < 42) ? 'sparse' : 'dense';
      });

      // Step 3: Output the page
      status('Composing pages...', true);

      pending_promises.push(Promise.resolve(sector));

      return Promise.all(pending_promises);

    }).then(function (results) {
      var template = Handlebars.compile($('#template').innerHTML);

      // Last result is the sector data...
      var sector = results.pop();
      document.body.innerHTML = template(sector);

      // Other results are tasks to run.
      results.forEach(function(result) { result(); });

      window.location.hash = hash;
    }, function(error) {
      status('Failed: ' + error);
    });
  };

}(this));
