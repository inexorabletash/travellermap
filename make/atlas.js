/*global Traveller, Handlebars */
(global => {
  'use strict';

  const $ = s => document.querySelector(s);
  const $$ = s => [...document.querySelectorAll(s)];

  function capitalize(s) {
    return s.substring(0, 1).toUpperCase() + s.substring(1);
  }

  function firstOrNull(a) {
    if (a && a.length > 0)
      return a[0];
    return null;
  }

  const cmp = (a, b) => a < b ? -1 : a > b ? 1 : 0;

  function smartquote(s) {
    return s ? s
      .replace("'", "\u2019")
      .replace(' "', " \u201C")
      .replace('" ', "\u201D ") : s;
  }

  function parseSector(tabDelimitedData, metadata) {
    let i;
    const sector = {
      metadata,
      worlds: []
    };

    const lines = tabDelimitedData.split(/\r?\n/);
    const header = lines.shift().toLowerCase().split('\t');
    lines.forEach(line => {
      if (!line.length)
        return;

      const world = {};
      line.split('\t').forEach((field, index) => {
        const col = header[index].replace(/[^a-z]/g, '');
        world[col] = field;
      });
      const exp = Traveller.fromHex(world.uwp[4]),
            mult = Traveller.fromHex(world.pbg[0]);
      world.population = exp >= 0 && mult >= 0 ? Math.pow(10, exp) * mult : 0;
      if (world.population >= 1e9)
        world.hipop = true;

      // U+2212 MINUS SIGN
      world.ix = world.ix.replace('-', '\u2212');
      world.ex = world.ex.replace('-', '\u2212');

      // Special formatting
      world.ix = world.ix.replace(/[{} ]/g, '');
      world.ex = world.ex.replace(/[() ]/g, '');
      world.cx = world.cx.replace(/[[\] ]/g, '');

      world.name = smartquote(world.name);

      sector.worlds.push(world);
    });

    sector.worlds.sort((a, b) => cmp(a.hex, b.hex));

    const LINES = 114, COLUMNS = 2;
    sector.pages = partition(sector.worlds, LINES*COLUMNS)
      .map(a => ({
        columns: partition(a, LINES).map(w => ({worlds: w})) }));

    sector.pages.forEach((page, index) => {
      // TODO: Replace with a counter?
      page.index = index + 1;
    });
    sector.page_count = sector.pages.length;

    sector.name = smartquote(metadata.Names[0].Text);

    sector.credits = smartquote(metadata.Credits);

    // TM's Y coordinates are inverted relative to FFE publications.
    metadata.Y = -metadata.Y;

    return sector;
  }

  function partition(list, ...counts) {
    const result = [];
    const copy = list.slice();
    while (copy.length) {
      result.push(copy.splice(0, counts[0]));
      if (counts.length > 1)
        counts.shift();
    }
    return result;
  }

  window.addEventListener('DOMContentLoaded', async () => {
    let sectors;
    sectors = [
      /*                                                         */ 'gash','tren',
      /*        */ 'tien','ziaf','gvur','tugl','prov','wind','mesh','mend','amdu','arzu',
      /*        */ 'farf','fore','spin','dene','corr','vlan','lish','anta','empt','star',
      /*        */ 'vang','beyo','troj','reft','gush','dagu','core','forn','ley', 'gate',
      'thet', /*       */ 'touc','rift','verg','ilel','zaru','mass','delp','glim','cruc',
      /*               */ 'afaw','hlak','eali','reav','daib','dias','olde','hint',
      /*                      */ 'stai','iwah','dark','magy','solo','alph','spic',
      /*                      */ 'akti','uist','ustr','cano','alde','newo','lang',
    ];

    // Uncomment for for testing:
    //sectors = ['spin', 'dene', 'troj', 'reft', 'solo'];

    render(await Promise.all(sectors.map(name => Promise.all([
        name,
        Traveller.MapService.sectorDataTabDelimited(name),
        Traveller.MapService.sectorMetaData(name)
      ]))));
  });

  function render(sectors) {
    const data = {};

    data.charted_space_src = Traveller.MapService.makeURL(
      '/api/poster', {
        x1: -256, x2: 255, y1: -159, y2: 160,
        options: 41975, scale: 8, style: 'print',
        accept: 'image/svg+xml',
        dimunofficial: 1, rotation: 3 });

    const index = [];
    const credits = [];
    let page_count = 3;
    data.sectors = sectors.map(tuple => {
      const name = tuple[0], data = tuple[1], metadata = tuple[2];
      const sector = parseSector(data, metadata);

      sector.img_src = Traveller.MapService.makeURL(
        '/api/poster', {
          sector: name,
          style: 'print',
          accept: 'image/svg+xml'
        });

      const short_name = sector.name.replace(/^The /, '');

      index.push({name: short_name, page: ++page_count});
      page_count += sector.page_count;

      if (sector.credits)
        credits.push({name: short_name, credits: sector.credits});
      else
        console.warn(`${sector.name} credits missing`);

      return sector;
    });
    index.sort((a, b) => cmp(a.name, b.name));
    data.index = index;
    data.credits = partition(
      credits
        .sort((a, b) => cmp(a.name, b.name))
        .map(o => o.credits),
      26, 30);

    data.date = (new Date).toLocaleDateString(
      'en-US', {year: 'numeric', month:'long', day: 'numeric'});

    const template = Handlebars.compile($('#template').innerHTML);
    const html = template(data);
    document.body.innerHTML = html;

    window.credits = credits;
    window.sectors = sectors;
    window.data = data;

    // Show image loading progress, and retry if server was overloaded.
    const images = $$('img');
    const progress = document.createElement('progress');
    progress.style = 'position: fixed; left: 0; top: 0; width: 100%;';
    progress.max = images.length;
    progress.value = 0;
    document.body.appendChild(progress);
    images.forEach(img => {
      img.addEventListener('load', event => {
        ++progress.value;
        if (progress.value === progress.max)
          progress.parentElement.removeChild(progress);
      });
      img.addEventListener('error', event => {
        setTimeout(() => {
          console.warn(`retrying ${img.src}`);
          img.src = img.src + '&retry';
        }, 1000 + 5000 * Math.random());
      });
    });
  }

})(self);
