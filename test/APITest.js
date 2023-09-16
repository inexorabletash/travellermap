/*global assertEquals, assertTrue, runTests */

//
// Test Helpers
//

const SERVICE_BASE = (function(l) {
  'use strict';
  if (l.hostname === 'localhost' && l.pathname.indexOf('~') !== -1)
    return 'https://travellermap.com';
  return '';
}(window.location));

async function fetchXML(uri) {
  const r = await fetch(SERVICE_BASE + '/' + uri, {headers: {'Accept': 'text/xml'}});
  assertEquals(r.status, 200, 'HTTP Status');
  assertEquals(r.headers.get('Content-Type'), 'text/xml', 'Content-Type');
  return await r.text();
}

async function fetchJSON(uri) {
  const r = await fetch(SERVICE_BASE + '/' + uri, {headers: {'Accept': 'application/json'}});
  assertEquals(r.status, 200, 'HTTP Status');
  assertEquals(r.headers.get('Content-Type'), 'application/json', 'Content-Type');
  return await r.json();
}

async function testXML(uri, expected) {
  function munge(s) {
    return s
      .replace(/(>\s*\r?\n\s*<)/g, '><')
      .replace(/\s+xmlns:xsd="http:\/\/www\.w3\.org\/2001\/XMLSchema"/g, '')
      .replace(/\s+xmlns:xsi="http:\/\/www\.w3\.org\/2001\/XMLSchema-instance"/g, '');
  }

  const xml = await fetchXML(uri);
  assertEquals(munge(xml), munge(expected), 'XML response');
}

async function testJSON(uri, expected) {
  const json = await fetchJSON(uri);
  assertEquals(json, expected, 'JSON response');
}

async function getBlob(url, type) {
  assertTrue('Blob' in self, 'Blob support');
  const r = await fetch(SERVICE_BASE + '/' + url);
  assertEquals(r.status, 200, 'HTTP Status for ' + url);
  assertEquals(r.headers.get('Content-Type'), type, 'Content-Type for ' + url);
  return await r.blob();
}

//
// Tests
//
var tests = [];
function test(name, func) { tests.push({ name: name, func: func }); }

test('Coordinates JSON - Sector Only', () => {
  return testJSON('api/coordinates?sector=spin',
                  { sx: -4, sy: -1, hx: 0, hy: 0, x: -129, y: -80 });
});

test('Coordinates JSON - Sector + Hex', () => {
  return testJSON('api/coordinates?sector=spin&hex=1910',
                  { sx: -4, sy: -1, hx: 19, hy: 10, x: -110, y: -70 });
});

test('Coordinates XML - Sector Only', () => {
  return testXML('api/coordinates?sector=spin',
      '<?xml version="1.0"?><Coordinates xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><sx>-4<\/sx><sy>-1<\/sy><hx>0<\/hx><hy>0<\/hy><x>-129<\/x><y>-80<\/y><\/Coordinates>');
});

test('Coordinates XML - Sector + Hex', () => {
  return testXML('api/coordinates?sector=spin&hex=1910',
      '<?xml version="1.0"?><Coordinates xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><sx>-4<\/sx><sy>-1<\/sy><hx>19<\/hx><hy>10<\/hy><x>-110<\/x><y>-70<\/y><\/Coordinates>');
});


function substituteParams(call) {
  var values = {
    "$sector": "solo",
    "$subsector": "A",
    "$ssname": "Sol",
    "$quadrant": "gamma",
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
  test(api, async () => {
    const r = await fetch(SERVICE_BASE + '/' + api);
    assertEquals(r.status, 200, 'HTTP Status');
    const result_type = r.headers.get('Content-Type');
    assertEquals(result_type, expected_type, 'Content-Type');
  });
}

/*
// Legacy ASPX APIs ----------

typeTest('Search.aspx?q=$query', 'text/xml');
typeTest('JumpMap.aspx?sector=$sector&hex=$hex&j=$jump', 'image/png');
typeTest('JumpMap.aspx?sector=$sector&hex=$hex&j=$jump&style=candy', 'image/png');
typeTest('JumpMap.aspx?sector=$sector&hex=$hex&j=$jump&style=candy&clip=0', 'image/jpeg');
typeTest('Poster.aspx?sector=$sector&subsector=$subsector', 'image/png');
typeTest('Poster.aspx?sector=$sector&subsector=$subsector&style=candy', 'image/jpeg');
typeTest('Tile.aspx?x=0&y=0&scale=64', 'image/png');
typeTest('Tile.aspx?x=0&y=0&scale=64&style=candy', 'image/jpeg');
typeTest('Coordinates.aspx?sector=$sector', 'text/xml');
typeTest('Credits.aspx?sector=$sector', 'text/xml');
typeTest('JumpWorlds.aspx?sector=$sector&hex=$hex&j=$jump', 'text/xml');
typeTest('JumpWorlds.aspx?sector=$sector&hex=$hex&j=$jump', 'text/xml');
typeTest('Universe.aspx', 'text/xml');
typeTest('SEC.aspx?sector=$sector', 'text/plain; charset=Windows-1252');
typeTest('SEC.aspx?sector=$sector&type=SecondSurvey', 'text/plain; charset=utf-8');
typeTest('SEC.aspx?sector=$sector&type=TabDelimited', 'text/plain; charset=utf-8');
typeTest('SectorMetaData.aspx?sector=$sector', 'text/xml');
typeTest('MSEC.aspx?sector=$sector', 'text/plain; charset=utf-8');
*/

// APIs ----------

// Search

typeTest('api/search?q=$query', 'application/json');

typeTest('api/route?start=SPIN+1910&end=SPIN+2207', 'application/json');

// Rendering

typeTest('api/jumpmap?sector=$sector&hex=$hex&j=$jump', 'image/png');
typeTest('api/jumpmap?sector=$sector&hex=$hex&j=$jump&style=candy', 'image/png');
typeTest('api/jumpmap?sector=$sector&hex=$hex&j=$jump&style=candy&clip=0', 'image/jpeg');

typeTest('api/poster?sector=$sector', 'image/png');
typeTest('api/poster?sector=$sector&subsector=$subsector', 'image/png');
typeTest('api/poster?sector=$sector&subsector=$subsector&style=candy', 'image/jpeg');
typeTest('api/poster?sector=$sector&quadrant=$quadrant', 'image/png');

typeTest('api/poster/$sector', 'image/png');
typeTest('api/poster/$sector/$quadrant', 'image/png');
typeTest('api/poster/$sector/$subsector', 'image/png');
typeTest('api/poster/$sector/$ssname', 'image/png');

typeTest('api/tile?x=0&y=0&scale=64', 'image/png');
typeTest('api/tile?x=0&y=0&scale=64&style=candy', 'image/jpeg');

// Location Queries

typeTest('api/coordinates?sector=$sector', 'application/json');
typeTest('api/credits?sector=$sector', 'application/json');
typeTest('api/jumpworlds?sector=$sector&hex=$hex&jump=$jump', 'application/json');
typeTest('api/universe', 'application/json');

// Data Retrieval

typeTest('api/sec?sector=$sector', 'text/plain; charset=utf-8');
typeTest('api/sec?type=sec&sector=$sector', 'text/plain; charset=Windows-1252');
typeTest('api/sec?type=TabDelimited&sector=$sector', 'text/plain; charset=utf-8');

typeTest('api/sec/$sector', 'text/plain; charset=utf-8');
typeTest('api/sec/$sector/$quadrant', 'text/plain; charset=utf-8');
typeTest('api/sec/$sector/$subsector', 'text/plain; charset=utf-8');
typeTest('api/sec/$sector/$ssname', 'text/plain; charset=utf-8');

typeTest('api/metadata?sector=$sector', 'application/json');
typeTest('api/metadata/$sector', 'application/json');
typeTest('api/metadata?accept=text/xml&sector=$sector', 'text/xml');

typeTest('api/msec?sector=$sector', 'text/plain; charset=utf-8');
typeTest('api/msec/$sector', 'text/plain; charset=utf-8');

// T5SS Stock Data ----------

typeTest('t5ss/allegiances', 'application/json');
typeTest('t5ss/allegiances?accept=text/xml', 'text/xml');
typeTest('t5ss/sophonts', 'application/json');
typeTest('t5ss/sophonts?accept=text/xml', 'text/xml');

// Semantic URLs ----------

typeTest('data', 'application/json');

typeTest('data/$sector', 'text/plain; charset=utf-8');
typeTest('data/$sector/sec', 'text/plain; charset=Windows-1252');
typeTest('data/$sector/tab', 'text/plain; charset=utf-8');
typeTest('data/$sector/coordinates', 'application/json');
typeTest('data/$sector/credits', 'application/json');
typeTest('data/$sector/metadata', 'text/xml');
typeTest('data/$sector/msec', 'text/plain; charset=utf-8');
typeTest('data/$sector/image', 'image/png');
typeTest('data/$sector/booklet', 'text/html');

typeTest('data/$sector/$quadrant', 'text/plain; charset=utf-8');
typeTest('data/$sector/$quadrant/tab', 'text/plain; charset=utf-8');
typeTest('data/$sector/$quadrant/sec', 'text/plain; charset=Windows-1252');
typeTest('data/$sector/$quadrant/image', 'image/png');

typeTest('data/$sector/$subsector', 'text/plain; charset=utf-8');
typeTest('data/$sector/$subsector/tab', 'text/plain; charset=utf-8');
typeTest('data/$sector/$subsector/sec', 'text/plain; charset=Windows-1252');
typeTest('data/$sector/$subsector/image', 'image/png');

typeTest('data/$sector/$hex', 'application/json');
typeTest('data/$sector/$hex/coordinates', 'application/json');
typeTest('data/$sector/$hex/credits', 'application/json');
typeTest('data/$sector/$hex/jump/$jump', 'application/json');
typeTest('data/$sector/$hex/image', 'image/png');
typeTest('data/$sector/$hex/jump/$jump/image', 'image/png');
typeTest('data/$sector/$hex/sheet', 'text/html');

typeTest('data/$sector/$ssname', 'text/plain; charset=utf-8');
typeTest('data/$sector/$ssname/tab', 'text/plain; charset=utf-8');
typeTest('data/$sector/$ssname/sec', 'text/plain; charset=Windows-1252');
typeTest('data/$sector/$ssname/image', 'image/png');


test('sec/metadata/poster - blobs', async () => {
  assertTrue('FormData' in self, 'FormData support');

  const blobs = await Promise.all([
    getBlob('api/sec?sector=spin', 'text/plain; charset=utf-8'),
    getBlob('api/metadata?sector=spin&accept=text/xml', 'text/xml')
  ]);

  const fd = new FormData();
  fd.append('file', blobs[0]);
  fd.append('metadata', blobs[1]);

  const r = await fetch(SERVICE_BASE + '/api/poster', {method: 'POST', body: fd});
  assertEquals(r.status, 200, 'HTTP Status');
  assertEquals(r.headers.get('Content-Type'), 'image/png', 'Content-Type');
});

test('sec/metadata/poster - form data', async () => {
  assertTrue('FormData' in self, 'FormData support');
  const blobs = await Promise.all([
    getBlob('api/sec?sector=spin', 'text/plain; charset=utf-8'),
    getBlob('api/metadata?sector=spin&accept=text/xml', 'text/xml')
  ]);

  const fd = new FormData();
  fd.append('file', blobs[0]);
  fd.append('metadata', blobs[1]);

  const r = await fetch(SERVICE_BASE + '/api/jumpmap?hex=1910', {method: 'POST', body: fd});
  assertEquals(r.status, 200, 'HTTP Status');
  assertEquals(r.headers.get('Content-Type'), 'image/png', 'Content-Type');
});

[
  /*
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
  */
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

  test("No Accept: " + api, async () => {
    const response = await fetch(SERVICE_BASE + '/' + api, {headers: {'Accept': ''}});
    assertEquals(response.status, 200, 'HTTP Status');
  });
});

// Initiate Test Harness

window.onload = () => { runTests(tests); };
