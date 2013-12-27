(function() {
  'use strict';

  function friendlyJoin(l) {
    if (l.length === 0) {
      return null;
    } else if (l.length === 1) {
      return l[0];
    } else {
      var last = l.pop();
      return l.join(', ') + ' and ' + last;
    }
  }

  function friendlyNumber(n) {
    if (n >= 1e12) {
      return Math.round(n / 1e11) / 10 + ' trillion';
    } else if (n >= 1e9) {
      return Math.round(n / 1e8) / 10 + ' billion';
    } else if (n >= 1e6) {
      return Math.round(n / 1e5) / 10 + ' million';
    } else if (n >= 1e3) {
      return Math.round(n / 1e2) / 10 + ' thousand';
    } else {
      return n;
    }
  }

  function capitalize(s) {
    return s.substring(0, 1).toUpperCase() + s.substring(1);
  }

  function titleCaps(s) {
    return s.split(/ /g).map(capitalize).join(' ');
  }

  function status(string, showImage) {
    var statusElement = document.getElementById('status'),
        statusText = document.getElementById('statusText'),
        statusImage = document.getElementById('statusImage');

    if (string !== undefined) {
      statusElement.style.display = '';
      statusText.innerHTML = escapeHtml(string);
      statusImage.style.display = showImage ? '' : 'none';
    } else {
      statusElement.style.display = 'none';
    }
  }

  function parseSector(text, metadata) {
    var i, sector = {
      metadata: metadata,
      worlds: [],
      subsectors: []
    };

    for (i = 0; i < 16; i += 1) {
      sector.subsectors[i] = {
        worlds: [],
        index: (i in metadata.Subsectors) ? metadata.Subsectors[i].Index : String.fromCharCode('A'.charCodeAt(0) + i),
        name: (i in metadata.Subsectors) ? metadata.Subsectors[i].Name : ''
      };
    }

    var lines = text.split(/\r?\n/);
    var header = lines.shift().toLowerCase().split(/\t/);
    lines.forEach(function (line) {
      if (!line.length)
        return;

      var world = {};
      line.split(/\t/).forEach(function (field, index) {
        var col = header[index].replace(/[^a-z]/g, '');
        world[col] = field;
      });
      world.population = Math.pow(10, Traveller.fromHex(world.uwp.charAt(4))) * Traveller.fromHex(world.pbg.charAt(0));
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

    window.sector = sector;

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

  var hash = window.location.hash;
  window.location.hash = '';

  window.onload = function () {
    var oParams = getUrlParameters(),
        options = oParams.options !== (void 0) ? oParams.options : MapOptions.BordersMask,
        style = oParams.style || 'print';

    if (!oParams.sector) {
      status("Missing 'sector' parameter in URL query string.");
      return;
    }

    status('Fetching metadata...', true);
    MapService.sectorMetaData(oParams.sector, function (metadata) {

      status('Fetching data...', true);
      MapService.sectorDataTabDelimited(oParams.sector, function (data) {
        status('Processing data...', true);
        setTimeout(function () {

          // Step 1: Parse the sector data
          var sector = parseSector(data, metadata);

          // Step 2: Post-process the data
          sector.title = sector.metadata.Names[0].Text;
          if (!/^The /.test(sector.title)) {
            sector.title = 'The ' + sector.title;
            if (![/ Sector$/, /es$/, / Rim$/].some(function (re) { return re.test(sector.title); })) {
              sector.title += ' Sector';
            }
          }
          document.title = sector.title;

          sector.img_src = SERVICE_BASE + '/api/poster?sector=' + encodeURIComponent(oParams.sector) +
            '&rotation=3&scale=64&options=' + encodeURIComponent(options | MapOptions.SubsectorGrid | MapOptions.NamesMask) +
            '&style=' + encodeURIComponent(style);

          range(16).forEach(function (i) {
            var subsector = sector.subsectors[i],
                neighbor, l;

            if (subsector.name.length === 0) {
              subsector.article = 'subsector ' + String.fromCharCode('A'.charCodeAt(0) + i);
            } else if (/^District /.test(subsector.name)) {
              subsector.article = subsector.name;
            } else if (/^The /i.test(subsector.name)) {
              subsector.article = subsector.name + ' subsector';
            } else {
              subsector.article = 'the ' + subsector.name + ' subsector';
            }
            subsector.title = titleCaps(subsector.article);

            neighbor = function (n, dx, dy) {
              var x = (n % 4) + dx,
                  y = Math.floor(n / 4) + dy;

              if (x < 0 || x >= 4 || y < 0 || y >= 4) {
                return '\xA0'; // Make sure the space doesn't collapse
              } else {
                n = x + (4 * y);
                return sector.subsectors[n].name;
              }
            };

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
              subsector.blurb.push(capitalize(subsector.article) + ' contains ' + subsector.worlds.length + ' worlds with a population of ' + friendlyNumber(subsector.population) + '.');

              if (subsector.maxpop && subsector.maxpop.population > 0) {
                subsector.blurb.push(' The highest population is ' + friendlyNumber(subsector.maxpop.population) + ', at ' + subsector.maxpop.name + '.');
              }
            } else if (subsector.worlds.length === 1 && subsector.maxpop) {
              subsector.blurb.push(capitalize(subsector.article) + ' contains one world, ' + subsector.maxpop.name + ', with a population of ' + friendlyNumber(subsector.maxpop.population) + '.');
            } else if (subsector.worlds.length === 0) {
              subsector.blurb.push(capitalize(subsector.article) + ' contains no charted worlds.');
            }

            if (subsector.maxtl && subsector.maxtl.length > 0) {
              l = subsector.maxtl.map(function (world) { return world.name; });
              subsector.blurb.push(' The highest tech level is ' + subsector.maxtl[0].uwp.charAt(8) + ' at ' + friendlyJoin(l) + '.');
            }

            if (subsector.capital) {
              subsector.blurb.push(' The subsector capital is at ' + subsector.capital.name + '.');
            }

            subsector.blurb = subsector.blurb.join(' ');
            subsector.img_src = SERVICE_BASE + '/api/poster?sector=' + encodeURIComponent(oParams.sector) +
              '&subsector=' + encodeURIComponent(subsector.index) +
              '&scale=64&options=' + encodeURIComponent(options) +
              '&style=' + encodeURIComponent(style);

            subsector.density = (subsector.worlds.length < 42) ? 'sparse' : 'dense';
          });

          // Step 3: Output the page
          status('Composing pages...', true);
          // The following logic is done asynchronously so that the status display can update
          window.setTimeout(function () {

            var template = Handlebars.compile(document.getElementById('template').innerHTML);
            document.getElementById('output').innerHTML = template(sector);

            status();

            window.location.hash = hash;
          }, 0);

        }, 0);
      }, function () {
        status('The requested sector "' + oParams.sector + '" was not found.');
      });
    }, function () {
      status('The requested sector "' + oParams.sector + '" was not found.');
    });
  };

} ());
