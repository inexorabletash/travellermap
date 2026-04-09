import * as Traveller from '../map.js';
import { prepareWorld, renderWorldImage, splitUWP } from '../world_util.js';

const Util = Traveller.Util;
const $ = Util.$;
const $$ = Util.$$;

window.addEventListener('load', async () => {
  const searchParams = new URLSearchParams(document.location.search);

  if (searchParams.has('nopage'))
    document.body.classList.add('nopage');

  let query;
  if (searchParams.has('sector') && searchParams.has('hex'))
    query = { sector: searchParams.get('sector'), hex: searchParams.get('hex') };
  else if (searchParams.has('x') && searchParams.has('y'))
    query = { x: searchParams.get('x'), y: searchParams.get('y') };
  else
    query = { sector: 'spin', hex: '1910' };
  // @ts-ignore
  query.milieu = searchParams.get('milieu');

  // Look up canonical location.
  try {
    const response = await fetch(Traveller.MapService.makeURL('/api/coordinates?', query));
    if (!response.ok)
      throw Error(response.statusText);
    const coords = await response.json();

    await Promise.all([
      // --------------------------------------------------
      // Fetch world data and fill in sheet.
      (async () => {
        const response = await
          fetch(Traveller.MapService.makeURL('/api/jumpworlds?', {
            x: coords.x, y: coords.y,
            milieu: searchParams.get('milieu'),
            jump: 0
          }));
        if (!response.ok) throw Error(response.statusText);
        const data = await response.json();
        const world = await prepareWorld(data.Worlds[0]);
        if (world) {
          await renderWorldImage(world, $('#wds-world-image'));
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
              const value = searchParams.get(param);
              if (value !== null) {
                url += '&' + param + '=' + encodeURIComponent(value);
              }
            });
            window.history.replaceState(null, document.title, url);
          }
        }
      })(),

      // --------------------------------------------------
      // Fill in neighborhood/jumpmap.
      (async () => {

        if (!searchParams.has('nohood') && $('#wds-neighborhood-data')) {
          const JUMP = 2;
          const SCALE = 48;

          const response = await fetch(Traveller.MapService.makeURL('/api/jumpworlds?', {
            x: coords.x, y: coords.y,
            milieu: searchParams.get('milieu'),
            jump: JUMP
          }));
          if (!response.ok) throw Error(response.statusText);
          const data = await response.json();

          // Make hi-pop worlds uppercase
          for (const world of data.Worlds) {
            const pop = Util.fromHex(splitUWP(world.UWP).Pop);
            if (pop >= 9)
              world.Name = world.Name.toUpperCase();
          }

          const template = Handlebars.compile($('#wds-neighborhood-template').innerHTML);
          $('#wds-neighborhood-data').innerHTML = template(data);

          const mapParams = {
            x: coords.x,
            y: coords.y,
            milieu: searchParams.get('milieu'),
            style: searchParams.get('style'),
            jump: JUMP,
            scale: SCALE,
            border: 0
          };
          if (window.devicePixelRatio > 1)
            mapParams.dpr = window.devicePixelRatio;
          const url = Traveller.MapService.makeURL('/api/jumpmap?', mapParams);
          const image = await Util.fetchImage(url, { imageElement: $('#wds-jumpmap') });

          image.addEventListener('click', event => {
            const result = jmapToCoords(event, JUMP, SCALE, coords.x, coords.y);
            if (result) {
              let search = '?x=' + result.x + '&y=' + result.y;
              const milieu = searchParams.get('milieu');
              if (milieu !== null) {
                search += '&milieu=' + encodeURIComponent(milieu);
              }
              window.location.search = search;
            }
          });
        }
      })(),
    ]);

    window.print();

  } catch (reason) {
    console.error(reason);
  }
});

function jmapToCoords(event, jump, scale, x, y) {
  // TODO: Reject hexes greater than J distance?

  const rect = event.target.getBoundingClientRect();
  const w = rect.right - rect.left;
  const h = rect.bottom - rect.top;

  const scaleX = Math.cos(Math.PI / 6) * scale, scaleY = scale;
  let dx = ((event.clientX - rect.left - w / 2) / scaleX);
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
