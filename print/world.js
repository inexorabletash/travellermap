/*global Traveller, Util, Handlebars */ // for lint and IDEs
(global => {
  'use strict';

  const $ = s => document.querySelector(s);
  const $$ = s => Array.from(document.querySelectorAll(s));

  window.addEventListener('load', () => {
    const searchParams = new URL(document.location).searchParams;

    if (searchParams.has('nopage'))
      document.body.classList.add('nopage');

    let coords;
    if (searchParams.has('sector') && searchParams.has('hex'))
      coords = {sector: searchParams.get('sector'), hex: searchParams.get('hex')};
    else if (searchParams.has('x') && searchParams.has('y'))
      coords = {x: searchParams.get('x'), y: searchParams.get('y')};
    else
      coords = {sector: 'spin', hex: '1910'};
    coords.milieu = searchParams.get('milieu');

    // Look up canonical location.
    fetch(Traveller.MapService.makeURL('/api/coordinates?', coords))
      .then(response => {
        if (!response.ok) throw Error(response.statusText);
        return response.json();
      })
      .then(coords => {
        const promises = [];

        // Fetch world data and fill in sheet.
        promises.push(
          fetch(Traveller.MapService.makeURL('/api/jumpworlds?', {
            x: coords.x, y: coords.y,
            milieu: searchParams.get('milieu'),
            jump: 0
          }))
            .then(response => {
              if (!response.ok) throw Error(response.statusText);
              return response.json();
            })
            .then(data => {
              return Traveller.prepareWorld(data.Worlds[0]);
            })
            .then(world => {
              if (!world) return undefined;
              return Traveller.renderWorldImage(world, $('#wds-world-image'));
            })
            .then(world => {
              if (!world) return;

              $('#wds-world-image').classList.add('wds-ready');
              $('#wds-world-data').innerHTML =
                Handlebars.compile($('#wds-world-template').innerHTML)(world);

              // Document title
              document.title = Handlebars.compile(
                '{{{Name}}} ({{{Sector}}} {{{Hex}}}) - World Data Sheet')(world);

              // Prettify URL
              if ('history' in window && 'replaceState' in window.history) {
                let url = window.location.href.replace(/\?.*$/, '') + '?sector=' + world.Sector + '&hex=' + world.Hex;
                ['milieu', 'style'].forEach(param => {
                  if (searchParams.has(param))
                    url += '&' + param + '=' + encodeURIComponent(searchParams.get(param));
                });
                window.history.replaceState(null, document.title, url);
              }
            })
          );

        // Fill in neighborhood/jumpmap.
        if (!searchParams.has('nohood') && $('#wds-neighborhood-data')) {
          const JUMP = 2;
          const SCALE = 48;

          promises.push(
            fetch(Traveller.MapService.makeURL('/api/jumpworlds?', {
              x: coords.x, y: coords.y,
              milieu: searchParams.get('milieu'),
              jump: JUMP
            }))
              .then(response => { return response.json(); })
              .then(data => {
                // Make hi-pop worlds uppercase
                data.Worlds.forEach(world => {
                  const pop = Traveller.fromHex(Traveller.splitUWP(world.UWP).Pop);
                  if (pop >= 9)
                    world.Name = world.Name.toUpperCase();
                });

                const template = Handlebars.compile($('#wds-neighborhood-template').innerHTML);
                $('#wds-neighborhood-data').innerHTML = template(data);
              })
            .then(() => {
              const mapParams = {
                x: coords.x,
                y: coords.y,
                milieu: searchParams.get('milieu'),
                style: searchParams.get('style'),
                jump: JUMP,
                scale: SCALE,
                border: 0};
              if (window.devicePixelRatio > 1)
                mapParams.dpr = window.devicePixelRatio;
              const url = Traveller.MapService.makeURL('/api/jumpmap?', mapParams);
              return Util.fetchImage(url, $('#wds-jumpmap'));
            })
            .then(image => {
              image.addEventListener('click', event => {
                const result = jmapToCoords(event, JUMP, SCALE, coords.x, coords.y);
                if (result) {
                  let search = '?x=' + result.x + '&y=' + result.y;
                  if (searchParams.has('milieu'))
                    search += '&milieu=' + encodeURIComponent(searchParams.get('milieu'));
                  window.location.search = search;
                }
              });
            }));
        }
        return Promise.all(promises);
      })
      .then(() => {
        if (searchParams.has('print'))
          window.print();
      })
      .catch(reason => {
        console.error(reason);
      });
  });

  function jmapToCoords(event, jump, scale, x, y) {
    // TODO: Reject hexes greater than J distance?

    const rect = event.target.getBoundingClientRect();
    const w = rect.right - rect.left;
    const h = rect.bottom - rect.top;

    const scaleX = Math.cos(Math.PI / 6) * scale, scaleY = scale;
    let dx = ((event.clientX  - rect.left - w / 2) / scaleX);
    let dy = ((event.clientY - rect.top - h / 2) / scaleY);

    function p(n) { return Math.abs(Math.round(n) - n); }
    const THRESHOLD = 0.4;

    if (p(dx) > THRESHOLD) return null;
    dx = Math.round(dx);
    if (x % 2)
      dy += (dx % 2) ? 0.5 : 0;
    else
      dy -= (dx % 2) ? 0.5 : 0;
    if (p(dy) > THRESHOLD) return null;
    dy = Math.round(dy);

    return { x: x + dx, y: y + dy };
  }


})(this);
