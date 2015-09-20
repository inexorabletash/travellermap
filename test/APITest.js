
//
// Test Helpers
//

var SERVICE_BASE = (function(l) {
  'use strict';
  if (l.hostname === 'localhost' && l.pathname.indexOf('~') !== -1)
    return 'http://travellermap.com';
  return '';
}(window.location));

function fetchXML(uri) {
  return fetch(SERVICE_BASE + '/' + uri, {headers: {'Accept': 'text/xml'}})
    .then(function(r) {
      assertEquals(r.status, 200,
                   'Expected HTTP 200 OK status, saw: ' + r.status);
      assertEquals(r.headers.get('Content-Type'), 'text/xml',
                   'Content-Type: ' + r.headers.get('Content-Type'));
      return r.text();
    });
}

function fetchJSON(uri) {
  return fetch(SERVICE_BASE + '/' + uri, {headers: {'Accept': 'application/json'}})
    .then(function(r) {
      assertEquals(r.status, 200, 'Expected HTTP 200 OK status, saw: ' + r.status);
      assertEquals(r.headers.get('Content-Type'), 'application/json',
                   'Content-Type: ' + r.headers.get('Content-Type'));
      return r.json();
    });
}

function testXML(uri, expected) {
  return fetchXML(uri).then(function(xml) {
    function munge(s) {
      return s.replace(/(>\s*\r?\n\s*<)/g, '><');
    }
    assertEquals(munge(xml), munge(expected),
                 'XML response does not match, ' + xml + ' != ' + expected);
  });
}

function testJSON(uri, expected) {
  return fetchJSON(uri).then(function(json) {
    assertEquals(json, expected,
                 'JSON response does not match, ' + JSON.stringify(json) + ' != ' + JSON.stringify(expected));
  });
}

function getBlob(url, type) {
  return fetch(SERVICE_BASE + '/' + url)
    .then(function(r) {
      assertEquals(r.status, 200, 'Expected HTTP 200 OK for ' + url + ', saw: ' + r.status);
      assertEquals(r.headers.get('Content-Type'), type, 'Content-Type for ' + url + ': ' + r.headers.get('Content-Type'));
      return r.blob();
    });
}

//
// Tests
//
var tests = [];
function test(name, func) { tests.push({ name: name, func: func }); }

test('Coordinates JSON - Sector Only', function() {
  return testJSON('api/coordinates?sector=spin',
                  { sx: -14, sy: -1, hx: 0, hy: 0, x: -129, y: -80 });
});

test('Coordinates JSON - Sector + Hex', function() {
  return testJSON('api/coordinates?sector=spin&hex=1910',
                  { sx: -4, sy: -1, hx: 19, hy: 10, x: -110, y: -70 });
});

test('Coordinates XML - Sector Only', function() {
  return testXML('api/coordinates?sector=spin',
      '<?xml version="1.0"?><Coordinates xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><sx>-4<\/sx><sy>-1<\/sy><hx>0<\/hx><hy>0<\/hy><x>-129<\/x><y>-80<\/y><\/Coordinates>');
});

test('Coordinates XML - Sector + Hex', function() {
  return testXML('api/coordinates?sector=spin&hex=1910',
      '<?xml version="1.0"?><Coordinates xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><sx>-4<\/sx><sy>-1<\/sy><hx>19<\/hx><hy>10<\/hy><x>-110<\/x><y>-70<\/y><\/Coordinates>');
});


function substituteParams(call) {
  var values = {
    "$sector": "solo",
    "$subsector": "A",
    "$hex": "1827",
    "$jump": 2,
    "$query": "terra"
  };
  Object.keys(values).forEach(function(key) {
    call = call.replace(key, values[key]);
  });
  return call;
}

function typeTest(api, expected_type) {
  api = substituteParams(api);
  test(api, function() {
    return fetch(SERVICE_BASE + '/' + api)
      .then(function(r) {
        assertEquals(r.status, 200, 'Expected HTTP 200 OK status, saw: ' + r.status);
        var result_type = r.headers.get('Content-Type');
        assertEquals(result_type, expected_type,
                     'Content-Type, expected ' + expected_type + ' saw: ' + result_type);
      });
  });
}

typeTest('Coordinates.aspx?sector=$sector', 'text/xml');
typeTest('JumpWorlds.aspx?sector=$sector&hex=$hex&j=$jump', 'text/xxml');
typeTest('JumpWorlds.aspx?sector=$sector&hex=$hex&j=$jump', 'text/xml');
typeTest('SEC.aspx?sector=$sector', 'text/plain; charset=Windows-1252');
typeTest('SEC.aspx?sector=$sector&type=SecondSurvey', 'text/plain; charset=utf-8');
typeTest('SEC.aspx?sector=$sector&type=TabDelimited', 'text/plain; charset=utf-8');
typeTest('MSEC.aspx?sector=$sector', 'text/plain; charset=utf-8');
typeTest('SectorMetaData.aspx?sector=$sector', 'text/xml');
typeTest('Universe.aspx', 'text/xml');
typeTest('Search.aspx?q=$query', 'text/xml');
typeTest('JumpMap.aspx?sector=$sector&hex=$hex&j=$jump', 'image/png');
typeTest('JumpMap.aspx?sector=$sector&hex=$hex&j=$jump&style=candy', 'image/png');
typeTest('JumpMap.aspx?sector=$sector&hex=$hex&j=$jump&style=candy&clip=0', 'image/jpeg');
typeTest('Poster.aspx?sector=$sector&subsector=$subsector', 'image/png');
typeTest('Poster.aspx?sector=$sector&subsector=$subsector&style=candy', 'image/jpeg');
typeTest('Tile.aspx?x=0&y=0&scale=64', 'image/png');
typeTest('Tile.aspx?x=0&y=0&scale=64&style=candy', 'image/jpeg');


typeTest('api/jumpmap?sector=$sector&hex=$hex&j=$jump', 'image/png');
typeTest('api/jumpmap?sector=$sector&hex=$hex&j=$jump&style=candy', 'image/png');
typeTest('api/jumpmap?sector=$sector&hex=$hex&j=$jump&style=candy&clip=0', 'image/jpeg');

typeTest('api/poster?sector=$sector&subsector=$subsector', 'image/png');
typeTest('api/poster?sector=$sector&subsector=$subsector&style=candy', 'image/jpeg');

typeTest('api/tile?x=0&y=0&scale=64', 'image/png');
typeTest('api/tile?x=0&y=0&scale=64&style=candy', 'image/jpeg');

test('sec/metadata/poster - blobs', function () {
  assertTrue('FormData' in self, 'Browser does not support FormData');

  return Promise.all([
    getBlob('api/sec?sector=spin', 'text/plain; charset=utf-8'),
    getBlob('api/metadata?sector=spin&accept=text/xml', 'text/xml')
  ]).then(function(blobs) {
    var fd = new FormData();
    fd.append('file', blobs[0]);
    fd.append('metadata', blobs[1]);
    return fetch(SERVICE_BASE + '/api/poster', {method: 'POST', body: fd});
  }).then(function(r) {
    assertEquals(r.status, 200, 'Expected HTTP 200 OK status, saw: ' + r.status);
    assertEquals(r.headers.get('Content-Type'), 'image/pngx', 'Content-Type: ' + r.headers.get('Content-Type'));
  });
});

test('sec/metadata/poster - form data', function() {
  assertTrue('FormData' in self, 'Browser does not support FormData');
  return Promise.all([
    getBlob('api/sec?sector=spin', 'text/plain; charset=utf-8'),
    getBlob('api/metadata?sector=spin&accept=text/xml', 'text/xml')
  ]).then(function(blobs) {
    var fd = new FormData();
    fd.append('file', blobs[0]);
    fd.append('metadata', blobs[1]);
    return fetch(SERVICE_BASE + '/api/jumpmap?hex=1910', {method: 'POST', body: fd});
  }).then(function(r) {
    assertEquals(r.status, 200, 'Expected HTTP 200 OK status, saw: ' + r.status);
    assertEquals(r.headers.get('Content-Type'), 'image/pngx', 'Content-Type: ' + r.headers.get('Content-Type'));
  });
});

typeTest('api/coordinates?sector=$sector', 'application/json');
typeTest('api/sec?sector=$sector', 'text/plain; charset=utf-8');
typeTest('api/sec?type=sec&sector=$sector', 'text/plain; charset=Windows-1252');
typeTest('api/sec?type=TabDelimited&sector=$sector', 'text/plain; charset=utf-8');
typeTest('api/metadata?sector=$sector', 'application/json');
typeTest('api/metadata?accept=text/xml&sector=$sector', 'text/xml');
typeTest('api/msec?sector=$sector', 'text/plain; charset=utf-8');
typeTest('api/jumpworlds?sector=$sector&hex=$hex&jump=$jump', 'application/json');
typeTest('api/search?q=$query', 'application/json');
typeTest('api/universe', 'application/json');

typeTest('data', 'application/json');

typeTest('data/$sector', 'text/plain; charset=utf-8');
typeTest('data/$sector/tab', 'text/plain; charset=utf-8');
typeTest('data/$sector/sec', 'text/plain; charset=Windows-1252');
typeTest('data/$sector/metadata', 'text/xml');
typeTest('data/$sector/msec', 'text/plain; charset=utf-8');
typeTest('data/$sector/image', 'image/png');
typeTest('data/$sector/coordinates', 'application/json');
typeTest('data/$sector/credits', 'application/json');

typeTest('data/$sector/$subsector', 'text/plain; charset=utf-8');
typeTest('data/$sector/$subsector/tab', 'text/plain; charset=utf-8');
typeTest('data/$sector/$subsector/sec', 'text/plain; charset=Windows-1252');
typeTest('data/$sector/$subsector/image', 'image/png');

typeTest('data/$sector/$hex/coordinates', 'application/json');
typeTest('data/$sector/$hex/credits', 'application/json');
typeTest('data/$sector/$hex/jump/$jump', 'application/json');
typeTest('data/$sector/$hex/jump/$jump/image', 'image/png');


[
  'Coordinates.aspx?sector=$sector',
  'JumpWorlds.aspx?sector=$sector&hex=$hex&j=$jump',
  'SEC.aspx?sector=$sector',
  'MSEC.aspx?sector=$sector',
  'SectorMetaData.aspx?sector=$sector',
  'Universe.aspx',
  'Search.aspx?q=$query',
  'JumpMap.aspx?sector=$sector&hex=$hex&j=$jump',
  'Poster.aspx?sector=$sector&subsector=$subsector',
  'Tile.aspx?x=0&y=0&scale=64',
  'api/coordinates?sector=$sector',
  'api/sec?sector=$sector',
  'api/sec?type=sec&sector=$sector',
  'api/sec?type=TabDelimited&sector=$sector',
  'api/metadata?sector=$sector',
  'api/msec?sector=$sector',
  'api/jumpworlds?sector=$sector&hex=$hex&jump=$jump',
  'api/search?q=$query',
  'api/universe'
].forEach(function(api) {
  api = substituteParams(api);

  test("No Accept: " + api, function() {
    return fetch(SERVICE_BASE + '/' + api, {headers: {'Accept': ''}})
      .then(function(response) {
        assertEquals(response.status, 200,
                     'Expected HTTP 200 OK status, saw: ' + response.status);
      });
  });
});

// Initiate Test Harness

window.onload = function() { runTests(tests); };
