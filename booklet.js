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
    statusText.innerHTML = escapeHtml(string);
    statusImage.style.display = pending ? '' : 'none';
  }

  function parseSector(tabDelimitedData, metadata) {
    var i, sector = {
      metadata: metadata,
      worlds: [],
      subsectors: []
    };

    for (i = 0; i < 16; i += 1) {
      sector.subsectors[i] = {
        worlds: [],
        index: (i in metadata.Subsectors) ? metadata.Subsectors[i].Index :
          String.fromCharCode('A'.charCodeAt(0) + i),
        name: (i in metadata.Subsectors) ? metadata.Subsectors[i].Name : ''
      };
    }

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
      world.population = Math.pow(10, Traveller.fromHex(world.uwp.charAt(4))) *
        Traveller.fromHex(world.pbg.charAt(0));
      sector.worlds.push(world);
    });

    // partition worlds into subsectors
    sector.index = [];
    sector.worlds.forEach(function(world) {
      var x = Math.floor(parseInt(world.hex, 10) / 100),
          y = Math.floor(parseInt(world.hex, 10) % 100),
          ss = Math.floor((x - 1) / (Astrometrics.SectorWidth / 4)) +
            Math.floor((y - 1) / (Astrometrics.SectorHeight / 4)) * 4;

      sector.subsectors[ss].worlds.push(world);

      if (world.name.length) {
        sector.index.push({
          name: world.name,
          location: world.ss + world.hex
        });
      }
    });
    sector.index.sort(function (a, b) {
      try {
        return a.name.localeCompare(b.name, 'en-us');
      } catch (e) {
        // Workaround for http://crbug.com/314210
        return (a.name < b.name) ? -1 : (a.name > b.name) ? 1 : -1;
      }
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

  // |data| can be string (payload) or object (key/value form data)
  // Returns Promise<string>
  function getTextViaPOST(url, data) {
    return new Promise(function(resolve, reject) {
      status('Requesting data...', true);
      var xhr = new XMLHttpRequest(), async = true;
      xhr.open('POST', url, async);
      if (typeof data === 'string') {
        xhr.setRequestHeader('Content-Type', 'text/plain'); // Safari doesn't infer this.
        xhr.send(data);
      } else if (typeof data === 'object') {
        var fd  = new FormData();
        Object.keys(data).forEach(function(key) { fd.append(key, data[key]); });
        xhr.send(fd);
      }
      xhr.onreadystatechange = function() {
        status('Receiving data...', true);
        if (xhr.readyState !== XMLHttpRequest.DONE)
          return;
        if (xhr.status === 200)
          resolve(xhr.response);
        else
          reject(xhr.responseText);
      };
    });
  }

  function sectorData(params) {
    return new Promise(function(resolve, reject) {
      if ('sector' in params) {
        MapService.sectorDataTabDelimited(params.sector, resolve, reject);
      } else if ('data' in params) {
        resolve(getTextViaPOST(
          makeURL(SERVICE_BASE + '/api/sec', {type: 'TabDelimited'}),
          params.data
        ));
      } else {
        reject('No sector or data specified.');
      }
    });
  }

  function sectorMetaData(params) {
    return new Promise(function(resolve, reject) {
      if ('sector' in params) {
        MapService.sectorMetaData(params.sector, resolve, reject);
        return;
      } else if ('metadata' in params) {
        getTextViaPOST(
          makeURL(SERVICE_BASE + '/api/metadata', {accept: 'application/json'}),
          params.metadata
        ).then(function(text) {
          resolve(JSON.parse(text));
        }, function(error) {
          reject(error);
        });
      } else {
        reject('No sector or metadata specified.');
      }
    });
  }

  document.addEventListener('DOMContentLoaded', function() {
    var params = getUrlParameters();
    if (params.sector) {
      $('#input').style.display = 'none';
      render(params);
      return;
    }

    $('#compose').addEventListener('click', function() {
      var form = $('#form');
      if (!form['data'].value.length) {
        alert('Sector data must be specified.');
        return;
      }
      $('#input').style.display = 'none';
      render({
        data: form['data'].value,
        metadata: form['metadata'].value
      });
    });

    status();
  });

  function render(params) {
    var hash = window.location.hash;
    window.location.hash = '';

    var options = params.options !== (void 0) ? Number(params.options) : MapOptions.BordersMask,
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
      document.title = sector.title;
      var imageURL, url_params = {
          rotation: 3,
          scale: 64,
          options: options | MapOptions.SubsectorGrid | MapOptions.NamesMask,
          style: style
      };
      if ('sector' in params) {
        url_params.sector = params.sector;
        imageURL = Promise.resolve(makeURL(SERVICE_BASE + '/api/poster', url_params));
      } else {
        url_params.data = params.data;
        url_params.metadata = params.metadata;
        url_params.datauri = 1;
        imageURL = getTextViaPOST(SERVICE_BASE + '/api/poster', url_params);
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
        });

        subsector.blurb = [];
        if (subsector.worlds.length > 1) {
          subsector.blurb.push(capitalize(subsector.article) + ' contains ' +
                               subsector.worlds.length + ' worlds with a population of ' +
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
        } else if (subsector.worlds.length === 0) {
          subsector.blurb.push(capitalize(subsector.article) + ' contains no charted worlds.');
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
            subsector: subsector.index,
            scale: 64,
            options: options,
            style: style
        };
        if ('sector' in params) {
          url_params.sector = params.sector;
          imageURL = Promise.resolve(makeURL(SERVICE_BASE + '/api/poster', url_params));
        } else {
          url_params.data = params.data;
          url_params.metadata = params.metadata;
          url_params.datauri = 1;
          imageURL = getTextViaPOST(SERVICE_BASE + '/api/poster', url_params);
        }
        pending_promises.push(imageURL.then(function(url) {
          return function() { $('#ss' + subsector.index  + ' img.subsector-image').src = url; };
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
      $('#output').innerHTML = template(sector);

      // Other results are tasks to run.
      results.forEach(function(result) { result(); });

      status();
      window.location.hash = hash;
    }, function(error) {
      status('Failed: ' + error);
    });
  };

}(this));
