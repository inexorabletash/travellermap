/*global Traveller, Util, Handlebars */ // for lint and IDEs
(function(global) {
  'use strict';

  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return document.querySelectorAll(s); };

  window.addEventListener('DOMContentLoaded', function() {
    var searchParams = new URL(document.location).searchParams;

    if (searchParams.has('nopage'))
      document.body.classList.add('nopage');

    var coords;
    if (searchParams.has('sector') && searchParams.has('hex'))
      coords = {sector: searchParams.get('sector'), hex: searchParams.get('hex')};
    else if (searchParams.has('x') && searchParams.has('y'))
      coords = {x: searchParams.get('x'), y: searchParams.get('y')};
    else
      coords = {sector: 'spin', hex: '1910'};
    coords.milieu = searchParams.get('milieu');

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
          fetch(Traveller.MapService.makeURL('/api/jumpworlds?', {
            x: coords.x, y: coords.y,
            milieu: searchParams.get('milieu'),
            jump: 0
          }))
            .then(function(response) {
              if (!response.ok) throw Error(response.statusText);
              return response.json();
            })
            .then(function(data) {
              return Traveller.prepareWorld(data.Worlds[0]);
            })
            .then(function(world) {
              if (!world) return undefined;
              return Traveller.renderWorldImage(world, $('#wds-world-image'));
            })
            .then(function(world) {
              if (!world) return;

              $('#wds-world-image').classList.add('wds-ready');
              $('#wds-world-data').innerHTML =
                Handlebars.compile($('#wds-world-template').innerHTML)(world);

              // Document title
              document.title = Handlebars.compile(
                '{{{Name}}} ({{{Sector}}} {{{Hex}}}) - World Data Sheet')(world);

              // Prettify URL
              if ('history' in window && 'replaceState' in window.history) {
                var url = window.location.href.replace(/\?.*$/, '') + '?sector=' + world.Sector + '&hex=' + world.Hex;
                if (searchParams.has('milieu'))
                  url += '&milieu=' + encodeURIComponent(searchParams.get('milieu'));
                window.history.replaceState(null, document.title, url);
              }
            })
          );

        // Fill in neighborhood/jumpmap.
        if (!searchParams.has('nohood') && $('#wds-neighborhood-data')) {
          var JUMP = 2;
          var SCALE = 48;

          promises.push(
            fetch(Traveller.MapService.makeURL('/api/jumpworlds?', {
              x: coords.x, y: coords.y,
              milieu: searchParams.get('milieu'),
              jump: JUMP
            }))
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
                milieu: searchParams.get('milieu'),
                style: searchParams.get('style'),
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
                if (result) {
                  var search = '?x=' + result.x + '&y=' + result.y;
                  if (searchParams.has('milieu'))
                    search += '&milieu=' + encodeURIComponent(searchParams.get('milieu'));
                  window.location.search = search;
                }
              });
            }));
        }
        return Promise.all(promises);
      })
      .then(function() {
        if (searchParams.has('print'))
          window.print();
      })
      .catch(function(reason) {
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
