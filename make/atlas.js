(function(global) {
  'use strict';

  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return Array.from(document.querySelectorAll(s)); };

  function capitalize(s) {
    return s.substring(0, 1).toUpperCase() + s.substring(1);
  }

  function firstOrNull(a) {
    if (a && a.length > 0)
      return a[0];
    return null;
  }

  function cmp(a, b) { return a < b ? -1 : a > b ? 1 : 0; }

  function parseSector(tabDelimitedData, metadata) {
    var i, sector = {
      metadata: metadata,
      worlds: []
    };

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

    sector.worlds.sort(function(a, b) { return cmp(a.hex, b.hex); });

    var LINES = 128, COLUMNS = 2;

    sector.pages = partition(sector.worlds, LINES*COLUMNS)
      .map(function(a) { return {columns: partition(a, LINES)
                                 .map(function(w) { return { worlds: w }; })}; });

    sector.pages.forEach(function(page, index) {
      // TODO: Replace with a counter?
      page.index = index + 1;
    });
    sector.page_count = sector.pages.length;

    sector.name = metadata.Names[0].Text;

    sector.credits = metadata.Credits;

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

  window.addEventListener('DOMContentLoaded', function() {
    var sectors;
    sectors = [
      /*   */ 'ziaf', 'gvur', 'tugl', 'prov', 'wind', 'mesh', 'mend', 'amdu',
      'farf', 'fore', 'spin', 'dene', 'corr', 'vlan', 'lish', 'anta', 'empt',
      'vang', 'beyo', 'troj', 'reft', 'gush', 'dagu', 'core', 'forn', 'ley',  'gate',
      'thet', /*   */ 'rift', 'verg', 'ilel', 'zaru', 'mass', 'delp', 'glim', 'cruc',
      /*           */ 'hlak', 'eali', 'reav', 'daib', 'dias', 'olde', 'hint',
      /*           */ 'stai', 'iwah', 'dark', 'magy', 'solo', 'alph', 'spic',
      /*           */ 'akti', 'uist', 'ustr'
    ];
    Promise.all(sectors.map(function(name) {
      return Promise.all([
        name,
        Traveller.MapService.sectorDataTabDelimited(name),
        Traveller.MapService.sectorMetaData(name)
      ]);
    })).then(render);
  });

  function render(sectors) {
    var data = {};

    data.charted_space_src = Traveller.MapService.makeURL(
      '/api/poster', { x1: -256, x2: 255, y1: -159, y2: 160,
                       options: 41975, scale: 8, style: 'print',
                       dimunofficial: 1, rotation: 3 });

    var index = [];
    var credits = [];
    var page_count = 3;
    data.sectors = sectors.map(function(tuple) {
      var name = tuple[0], data = tuple[1], metadata = tuple[2];
      var sector = parseSector(data, metadata);

      sector.img_src = Traveller.MapService.makeURL(
        '/api/poster', {sector: name, style: 'print', dpr: 2});

      var short_name = sector.name.replace(/^The /, '');

      index.push({name: short_name, page: ++page_count});
      if (sector.credits)
        credits.push({name: short_name, credits: sector.credits});
      else
        console.warn(sector.name + ' credits missing');

      return sector;
    });
    index.sort(function(a, b) { return cmp(a.name, b.name); });
    data.index = index;
    data.credits = partition(
      credits
        .sort(function(a, b) { return cmp(a.name, b.name); })
        .map(function(o) { return o.credits; })
      , 30);

    data.date = (new Date).toLocaleDateString(
      'en-US', {year: 'numeric', month:'long', day: 'numeric'});

    var template = Handlebars.compile($('#template').innerHTML);
    document.body.innerHTML = template(data);

    window.credits = credits;
    window.sectors = sectors;
    window.data = data;

    // Show image loading progress, and retry if server was overloaded.
    var images = $$('img');
    var progress = document.createElement('progress');
    progress.style = 'position: fixed; left: 0; top: 0; width: 100%;';
    progress.max = images.length;
    progress.value = 0;
    document.body.appendChild(progress);
    images.forEach(function(img) {
      img.addEventListener('load', function() {
        ++progress.value;
        if (progress.value === progress.max)
          progress.parentElement.removeChild(progress);
      });
      img.addEventListener('error', function() {
        setTimeout(function() {
          console.warn('retrying ' + img.src);
          img.src = img.src + '&retry';
        }, 1000 + 5000 * Math.random());
      });
    });
  };

}(self));
