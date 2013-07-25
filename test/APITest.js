
//
// Test Helpers
//

function fetchXML(uri) {
  var xhr = new XMLHttpRequest();
  xhr.open('GET', uri, false);
  xhr.setRequestHeader('Accept', 'text/xml');
  xhr.send();

  assertEquals(xhr.status, 200, 'Expected HTTP 200 OK status, saw: ' + xhr.status);
  assertEquals(xhr.getResponseHeader('Content-Type'), 'text/xml', 'Incorrect Content-Type: ' + xhr.getResponseHeader('Content-Type'));
  return xhr.responseText;
}

function fetchJSON(uri) {
  var xhr = new XMLHttpRequest();
  xhr.open('GET', uri, false);
  xhr.setRequestHeader('Accept', 'application/json');
  xhr.send();
  console.log(uri); 
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
  xhr.open('GET', uri, false);
  xhr.send();

  assertEquals(xhr.status, 200, 'Expected HTTP 200 OK status, saw: ' + xhr.status);
  result_type = xhr.getResponseHeader('Content-Type');
  assertEquals(result_type, expected_type, 'incorrect Content-Type, expected ' + expected_type + ' saw: ' + result_type);
}

//
// Tests
//

var tests = [
        {
          name: 'Coordinates JSON - Sector Only',
          func: function() {
            testJSON('../Coordinates.aspx?sector=Spinward%20Marches',
                { sx: -4, sy: -1, hx: 0, hy: 0, x: -129, y: -80 });
          }
        },
        {
          name: 'Coordinates JSON - Sector + Hex',
          func: function() {
            testJSON('../Coordinates.aspx?sector=Spinward%20Marches&hex=1910',
                { sx: -4, sy: -1, hx: 19, hy: 10, x: -110, y: -70 });
          }
        },
        {
          name: 'Coordinates XML - Sector Only',
          func: function() {
            testXML('../Coordinates.aspx?sector=Spinward%20Marches',
                '<?xml version="1.0"?><Coordinates xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><sx>-4<\/sx><sy>-1<\/sy><hx>0<\/hx><hy>0<\/hy><x>-129<\/x><y>-80<\/y><\/Coordinates>');
          }
        },
        {
          name: 'Coordinates XML - Sector + Hex',
          func: function() {
            testXML('../Coordinates.aspx?sector=Spinward%20Marches&hex=1910',
                '<?xml version="1.0"?><Coordinates xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"><sx>-4<\/sx><sy>-1<\/sy><hx>19<\/hx><hy>10<\/hy><x>-110<\/x><y>-70<\/y><\/Coordinates>');
          }
        },
        { name: 'Coordinates defaults to XML', func: function() { checkType('../Coordinates.aspx?sector=Ley', 'text/xml'); } },
        { name: 'Credits defaults to XML', func: function() { checkType('../JumpWorlds.aspx?sector=Spinward%20Marches&hex=1910&j=1', 'text/xml'); } },
        { name: 'JumpWorlds defaults to XML', func: function() { checkType('../JumpWorlds.aspx?sector=Spinward%20Marches&hex=1910&j=1', 'text/xml'); } },
        { name: 'SEC defaults to text', func: function () { checkType('../SEC.aspx?sector=Spinward%20Marches', 'text/plain; charset=Windows-1252'); } },
        { name: 'SEC/Second Survey defaults to text', func: function () { checkType('../SEC.aspx?sector=Spinward%20Marches&type=SecondSurvey', 'text/plain; charset=utf-8'); } },
        { name: 'SEC/Tab Delimited defaults to text', func: function () { checkType('../SEC.aspx?sector=Spinward%20Marches&type=TabDelimited', 'text/plain; charset=utf-8'); } },
        { name: 'MSEC defaults to text', func: function () { checkType('../MSEC.aspx?sector=Spinward%20Marches', 'text/plain; charset=utf-8'); } },
        { name: 'SectorMetaData defaults to XML', func: function() { checkType('../SectorMetaData.aspx?sector=Spinward%20Marches', 'text/xml'); } },
        { name: 'Universe defaults to XML', func: function() { checkType('../Universe.aspx', 'text/xml'); } },

        { name: 'Search defaults to XML', func: function() { checkType('../Search.aspx?q=Regina', 'text/xml'); } },

        { name: 'JumpMap defaults to PNG', func: function() { checkType('../JumpMap.aspx?sector=Spinward%20Marches&hex=1910&j=1', 'image/png'); } },
        { name: 'Poster defaults to PNG', func: function() { checkType('../Poster.aspx?sector=Ley&subsector=A', 'image/png'); } },
        { name: 'Tile defaults to PNG', func: function() { checkType('../Tile.aspx?x=0&y=0&scale=64', 'image/png'); } },

        { name: 'SEC/SectorMetaData/Poster', func: function() {

          function getBlob(url, type) {
            // possibly do this using XHR2 with xhr.contentType = 'blob'
            var xhr;

            xhr = new XMLHttpRequest();
            xhr.open('GET', url, false);
            xhr.send();
            assertEquals(xhr.status, 200, 'Expected HTTP 200 OK for ' + url + ', saw: ' + xhr.status);
            assertEquals(xhr.getResponseHeader('Content-Type'), type, 'Incorrect Content-Type for ' + url + ': ' + xhr.getResponseHeader('Content-Type'));

            assertTrue(Blob, 'Browser does not support Blob');
            return new Blob([xhr.responseText]);
          }

          var xhr, fd;

          assertTrue(window.FormData, 'Browser does not support FormData');
          fd = new FormData();
          fd.append('file', getBlob('../SEC.aspx?sector=Spinward%20Marches', 'text/plain; charset=Windows-1252'));
          fd.append('metadata', getBlob('../SectorMetaData.aspx?sector=Spinward%20Marches', 'text/xml'));
          xhr = new XMLHttpRequest();
          xhr.open('POST', '../Poster.aspx', false);
          xhr.send(fd);

          assertEquals(xhr.status, 200, 'Expected HTTP 200 OK status, saw: ' + xhr.status);
          assertEquals(xhr.getResponseHeader('Content-Type'), 'image/png', 'incorrect Content-Type: ' + xhr.getResponseHeader('Content-Type'));
        }
        }

    ];

// Initiate Test Harness

window.onload = function() { runTests(tests); };
        