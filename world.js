var Traveller, Util, Handlebars;
(function(global) {
  'use strict';

  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return document.querySelectorAll(s); };

  window.addEventListener('DOMContentLoaded', function() {
    var query = Util.parseURLQuery(document.location);

    if ('nopage' in query)
      document.body.classList.add('nopage');

    var coords;
    if ('sector' in query && 'hex' in query)
      coords = {sector: query.sector, hex: query.hex};
    else if ('x' in query && 'y' in query)
      coords = {x: query.x, y: query.y};
    else
      coords = {sector: 'spin', hex: '1910'};

    // Look up canonical location.
    fetch(Traveller.MapService.makeURL('/api/coordinates?', coords))
      .then(function(response) {
        if (!response.ok) throw Error(response.statusText);
        return response.json();
      })
      .then(function(coords) {
        var promises = [];

        // Fetch world data and fill in sheet.
        promises.push(
          fetch(Traveller.MapService.makeURL('/api/jumpworlds?',
                                             {x: coords.x, y: coords.y, jump: 0}))
            .then(function(response) {
              if (!response.ok) throw Error(response.statusText);
              return response.json();
            })
            .then(function(data) {
              return Traveller.renderWorld(data.Worlds[0]);
            })
            .then(function(world) {
              if (!world) return;

              // Document title
              document.title = Handlebars.compile(
                '{{{Name}}} ({{{Sector}}} {{{Hex}}}) - World Data Sheet')(world);

              // Hook up any generated "expandy" fields
              Array.from($$('.wds-expandy')).forEach(function(elem) {
                elem.addEventListener('click', function(event) {
                  var t = elem;
                  do {
                    t = t.nextSibling;
                  } while (t.nodeType !== Node.ELEMENT_NODE);
                  t.classList.toggle('wds-hidden');
                  elem.classList.toggle('wds-expanded');
                });
              });

              // Hook up toggle
              if ($('#wds-mini-toggle')) {
                $('#wds-mini-toggle').addEventListener('click', function(event) {
                  $('#wds-frame').classList.toggle('wds-mini');
                });
              }

              // Prettify URL
              if ('history' in window && 'replaceState' in window.history) {
                var url = window.location.href.replace(/\?.*$/, '') + '?sector=' + world.Sector + '&hex=' + world.Hex;
                window.history.replaceState(null, document.title, url);
              }
            })
          );

        // Fill in neighborhood/jumpmap.
        if (!('nohood' in query) && $('#wds-neighborhood-data')) {
          var JUMP = 2;
          var SCALE = 48;

          promises.push(
            fetch(Traveller.MapService.makeURL('/api/jumpworlds?',
                                               {x: coords.x, y: coords.y, jump: JUMP}))
              .then(function(response) { return response.json(); })
              .then(function(data) {
                // Make hi-pop worlds uppercase
                data.Worlds.forEach(function(world) {
                  var pop = Traveller.fromHex(Traveller.splitUWP(world.UWP).Pop);
                  if (pop >= 9)
                    world.Name = world.Name.toUpperCase();
                });

                var template = Handlebars.compile($('#wds-neighborhood-template').innerHTML);
                $('#wds-neighborhood-data').innerHTML = template(data);
              })
            .then(function() {
              var mapParams = {
                x: coords.x,
                y: coords.y,
                jump: JUMP,
                scale: SCALE,
                border: 0};
              if (window.devicePixelRatio > 1)
                mapParams.dpr = window.devicePixelRatio;
              var url = Traveller.MapService.makeURL('/api/jumpmap?', mapParams);
              return Util.fetchImage(url, $('#wds-jumpmap'));
            })
            .then(function(image) {
              image.addEventListener('click', function(event) {
                var result = jmapToCoords(event, JUMP, SCALE, coords.x, coords.y);
                if (result)
                  window.location.search = '?x=' + result.x + '&y=' + result.y;
              });
            }));
        }
        return Promise.all(promises);
      }, function(reason) {
        console.error(reason);
      });
  });

  function jmapToCoords(event, jump, scale, x, y) {
    // TODO: Reject hexes greater than J distance?

    var rect = event.target.getBoundingClientRect();
    var w = rect.right - rect.left;
    var h = rect.bottom - rect.top;

    var scaleX = Math.cos(Math.PI / 6) * scale, scaleY = scale;
    var dx = ((event.clientX  - rect.left - w / 2) / scaleX);
    var dy = ((event.clientY - rect.top - h / 2) / scaleY);

    function p(n) { return Math.abs(Math.round(n) - n); }
    var THRESHOLD = 0.4;

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


}(this));
