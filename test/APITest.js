
//
// Test Helpers
//

function fetchXML(uri) {
  var xhr = new XMLHttpRequest();
  xhr.open('GET', '../' + uri, false);
  xhr.setRequestHeader('Accept', 'text/xml');
  xhr.send();

  assertEquals(xhr.status, 200, 'Expected HTTP 200 OK status, saw: ' + xhr.status);
  assertEquals(xhr.getResponseHeader('Content-Type'), 'text/xml', 'Incorrect Content-Type: ' + xhr.getResponseHeader('Content-Type'));
  return xhr.responseText;
}

function fetchJSON(uri) {
  var xhr = new XMLHttpRequest();
  xhr.open('GET', '../' + uri, false);
  xhr.setRequestHeader('Accept', 'application/json');
  xhr.send();
  assertEquals(xhr.status, 200, 'Expected HTTP 200 OK status, saw: ' + xhr.status);
  assertEquals(xhr.getResponseHeader('Content-Type'), 'application/json', 'Incorrect Content-Type: ' + xhr.getResponseHeader('Content-Type'));
  return xhr.responseText;
}


function testXML(uri, expected) {
  var xml = fetchXML(uri);

  function munge(s) {
    return s.replace(/(>\s*\r?\n\s*<)/g, '><');
  }

  assertEquals(munge(xml), munge(expected),
            'XML response does not match, ' + xml + ' != ' + expected);
}

function testJSON(uri, expected) {
  var json = fetchJSON(uri);

  assertEquals(JSON.parse(json), expected,
            'JSON response does not match, ' + json + ' != ' + JSON.stringify(expected));
}

function checkType(uri, expected_type) {
  var xhr = new XMLHttpRequest(), result_type;
  xhr.open('GET', '../' + uri, false);
  xhr.send();

  assertEquals(xhr.status, 200, 'Expected HTTP 200 OK status, saw: ' + xhr.status);
  result_type = xhr.getResponseHeader('Content-Type');
  assertEquals(result_type, expected_type, 'incorrect Content-Type, expected ' + expected_type + ' saw: ' + result_type);
}

function getBlob(url, type) {
  // possibly do this using XHR2 with xhr.contentType = 'blob'
  var xhr = new XMLHttpRequest();
  xhr.open('GET', '../' + url, false);
  xhr.send();
  assertEquals(xhr.status, 200, 'Expected HTTP 200 OK for ' + url + ', saw: ' + xhr.status);
  assertEquals(xhr.getResponseHeader('Content-Type'), type, 'Incorrect Content-Type for ' + url + ': ' + xhr.getResponseHeader('Content-Type'));

  assertTrue(Blob, 'Browser does not support Blob');
  return new Blob([xhr.responseText]);
}

//
// Tests
//
var tests = [];
function test(name, func) { tests.push({ name: name, func: func }); }

test('Coordinates JSON - Sector Only', function() {
  testJSON('Coordinates.aspx?sector=spin',
      { sx: -4, sy: -1, hx: 0, hy: 0, x: -129, y: -80 });
});

test('Coordinates JSON - Sector + Hex', function() {
  testJSON('Coordinates.aspx?sector=spin&hex=1910',
      { sx: -4, sy: -1, hx: 19, hy: 10, x: -110, y: -70 });
});

test('Coordinates XML - Sector Only', function() {
  testXML('Coordinates.aspx?sector=spin',
      '<?xml version="1.0"?><Coordinates xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><sx>-4<\/sx><sy>-1<\/sy><hx>0<\/hx><hy>0<\/hy><x>-129<\/x><y>-80<\/y><\/Coordinates>');
});

test('Coordinates XML - Sector + Hex', function() {
  testXML('Coordinates.aspx?sector=spin&hex=1910',
      '<?xml version="1.0"?><Coordinates xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><sx>-4<\/sx><sy>-1<\/sy><hx>19<\/hx><hy>10<\/hy><x>-110<\/x><y>-70<\/y><\/Coordinates>');
});


function typeTest(api, type) {

  var call = api;
  var values = {
    "$sector": "solo",
    "$subsector": "A",
    "$hex": "1827",
    "$jump": 2,
    "$query": "terra"
  };
  Object.keys(values).forEach(function (key) {
    call = call.replace(key, values[key]);
  });

  test(api, function () {
    checkType(call, type);
  });
}



typeTest('Coordinates.aspx?sector=$sector', 'text/xml');
typeTest('JumpWorlds.aspx?sector=$sector&hex=$hex&j=$jump', 'text/xml');
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

test('SEC/SectorMetaData/Poster', function () {
  assertTrue(window.FormData, 'Browser does not support FormData');

  var fd = new FormData();
  fd.append('file', getBlob('SEC.aspx?sector=spin', 'text/plain; charset=Windows-1252'));
  fd.append('metadata', getBlob('SectorMetaData.aspx?sector=spin', 'text/xml'));

  var xhr = new XMLHttpRequest();
  xhr.open('POST', '../Poster.aspx', false);
  xhr.send(fd);

  assertEquals(xhr.status, 200, 'Expected HTTP 200 OK status, saw: ' + xhr.status);
  assertEquals(xhr.getResponseHeader('Content-Type'), 'image/png', 'incorrect Content-Type: ' + xhr.getResponseHeader('Content-Type'));
});

test('SEC/SectorMetaData/JumpMap', function () {
  assertTrue(window.FormData, 'Browser does not support FormData');

  var fd = new FormData();
  fd.append('file', getBlob('SEC.aspx?sector=spin', 'text/plain; charset=Windows-1252'));
  fd.append('metadata', getBlob('SectorMetaData.aspx?sector=spin', 'text/xml'));

  var xhr = new XMLHttpRequest();
  xhr.open('POST', '../JumpMap.aspx?hex=1910', false);
  xhr.send(fd);

  assertEquals(xhr.status, 200, 'Expected HTTP 200 OK status, saw: ' + xhr.status);
  assertEquals(xhr.getResponseHeader('Content-Type'), 'image/png', 'incorrect Content-Type: ' + xhr.getResponseHeader('Content-Type'));
});

typeTest('api/coordinates?sector=$sector', 'application/json');
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

// Initiate Test Harness

window.onload = function() { runTests(tests); };
