
// http://rosettacode.org/wiki/Longest_Common_Substring
function lcs(a, b) {
  let len = 0, oa = 0, ob = 0;;

  const lengths = [];
  lengths.length = a.length * b.length;
  lengths.fill(0);

  for (let i = 0; i < a.length; ++i) {
    for (let j = 0; j < b.length; j++) {
      if (a[i] === b[j]) {
        lengths[i * a.length + j] =
          (i === 0 || j === 0) ? 1 : lengths[(i - 1) * a.length + (j - 1)] + 1;
        if (lengths[i * a.length + j] > len) {
          len = lengths[i * a.length + j];
          oa = i - len + 1;
          ob = j - len + 1;
        }
      } else {
        lengths[i * a.length + j] = 0;
      }
    }
  }
  return { len: len, oa: oa, ob: ob };
}

function idiff(as, bs) {
  const r = lcs(as, bs);
  // Nothing common
  if (!r.len) {
    const result = [];
    if (as.length) result.push(['-', as]);
    if (bs.length) result.push(['+', bs]);
    return result;
  }
  // Recurse
  return [].concat(
    idiff(as.slice(0, r.oa), bs.slice(0, r.ob)),
    // @ts-ignore
    [['=', bs.slice(r.ob, r.ob + r.len)]],
    idiff(as.slice(r.oa + r.len), bs.slice(r.ob + r.len))
  );
}

function diff(text1, text2) {
  function removeNamespaces(s) {
    return s
      .replace(/\s+xmlns:xsd="http:\/\/www\.w3\.org\/2001\/XMLSchema"/g, '')
      .replace(/\s+xmlns:xsi="http:\/\/www\.w3\.org\/2001\/XMLSchema-instance"/g, '');
  }

  const lines1 = removeNamespaces(text1).split(/\r?\n/);
  const lines2 = removeNamespaces(text2).split(/\r?\n/);
  const out = [];
  idiff(lines1, lines2)
    .filter(function (pair) { return pair[0] !== '='; })
    .forEach(function (pair) {
      const op = pair[0];
      pair[1].forEach(function (line) { out.push(op + ' ' + line); });
    });
  return out.join('\n');;
};


async function fetchPair(contentType, url1, url2) {

  async function getHeaderAndBody(response) {
    const text = await response.text();
    return {
      headers: response.headers,
      text: text
    };
  }

  const requestContentType = contentType.replace(/;.*/, '');

  const responses = await Promise.all([
    fetch(url1).then(getHeaderAndBody),
    fetch(url2, { headers: { Accept: requestContentType } }).then(getHeaderAndBody)]);

  const a = 'Content-Type: ' + contentType + '\n'
    + responses[0].text;
  const b = 'Content-Type: ' + responses[1].headers.get('Content-Type') + '\n'
    + responses[1].text;
  return [a, b];
}

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => document.querySelectorAll(selector);
function elem(tag, attribs) {
  const e = document.createElement(tag);
  if (attribs) Object.keys(attribs).forEach(function (key) {
    e.setAttribute(key, attribs[key]);
  });
  return e;
}
function text(text) { return document.createTextNode(text); }
function link(uri) {
  const a = elem('a', { href: uri });
  a.appendChild(text(uri));
  return a;
}

const status = {
  tests: 0,
  completed: 0,
  passed: 0
};
function update() {
  $('#status_tests').innerHTML = String(status.tests);
  $('#status_passed').innerHTML = String(status.passed);
  $('#status_failed').innerHTML = String(status.completed - status.passed);
}

const runTest = async (leftTitle, rightTitle, func) => {
  ++status.tests;

  const tr1 = $('#results').appendChild(elem('tr'));
  tr1.classList.add('source');
  tr1.appendChild(elem('td')).appendChild(link(leftTitle));
  tr1.appendChild(elem('td')).appendChild(link(rightTitle));
  tr1.appendChild(elem('td')).appendChild(text('diff'));

  const tr2 = $('#results').appendChild(elem('tr'));
  tr2.classList.add('content');

  const results = await func();

  tr1.classList.add(results.pass ? 'pass' : 'fail');
  tr2.classList.add(results.pass ? 'pass' : 'fail');

  tr2.appendChild(elem('td')).appendChild(elem('textarea', { wrap: 'off' })).value = results.left;
  tr2.appendChild(elem('td')).appendChild(elem('textarea', { wrap: 'off' })).value = results.right;
  tr2.appendChild(elem('td')).appendChild(elem('textarea', { wrap: 'off' })).value = results.diff;

  ++status.completed;
  if (results.pass)
    ++status.passed;
  update();
};

//** Usage: check(contentType, expectedURI, actualURI, filter_opt); **/
function check(contentType, url1, url2, filter) {
  runTest(url1, url2, async function () {
    try {
      const pair = await fetchPair(contentType, url1, url2);
      let a = pair[0], b = pair[1];
      if (filter) {
        a = filter(a);
        b = filter(b);
      }
      const d = diff(a, b);
      return { left: a, right: b, diff: d, pass: !d };
    } catch (error) {
      return { left: '', right: '', diff: 'Fetch failed: ' + error, pass: false };
    }
  });
};

const filter_timestamp = s => {
  return s.replace(/# \d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d.*\r?\n/g, '# <TIMESTAMP>');
};

var SERVICE_BASE = (function (l) {
  'use strict';
  if (l.hostname === 'localhost' && l.pathname.indexOf('~') !== -1)
    return 'https://travellermap.com';
  return '';
}(window.location));

// Sanity Tests

check('application/json', 'refs/data_spin_1910.json', SERVICE_BASE + '/data/spin/1910');
check('text/xml', 'refs/data_spin_1910.xml', SERVICE_BASE + '/data/spin/1910');

// Coordinates API

check('application/json', 'refs/coordinates_spin.json', SERVICE_BASE + '/api/coordinates?sector=spin');
check('text/xml', 'refs/coordinates_spin.xml', SERVICE_BASE + '/api/coordinates?sector=spin');

check('application/json', 'refs/coordinates_spin_1910.json', SERVICE_BASE + '/api/coordinates?sector=spin&hex=1910');
check('text/xml', 'refs/coordinates_spin_1910.xml', SERVICE_BASE + '/api/coordinates?sector=spin&hex=1910');

check('application/json', 'refs/coordinates_spin_c.json', SERVICE_BASE + '/api/coordinates?sector=spin&subsector=c');
check('text/xml', 'refs/coordinates_spin_c.xml', SERVICE_BASE + '/api/coordinates?sector=spin&subsector=c');

check('application/json', 'refs/coordinates_spin_regina.json', SERVICE_BASE + '/api/coordinates?sector=spin&subsector=regina');
check('text/xml', 'refs/coordinates_spin_regina.xml', SERVICE_BASE + '/api/coordinates?sector=spin&subsector=regina');

// Credits API

check('application/json', 'refs/credits_legend.json', SERVICE_BASE + '/api/credits?sector=legend');
check('text/xml', 'refs/credits_legend.xml', SERVICE_BASE + '/api/credits?sector=legend');

check('application/json', 'refs/credits_0_0.json', SERVICE_BASE + '/api/credits?x=0&y=0');
check('text/xml', 'refs/credits_0_0.xml', SERVICE_BASE + '/api/credits?x=0&y=0');

// Sector Data API

check('text/plain; charset=utf-8',
  'refs/sec_legend.t5col',
  SERVICE_BASE + '/api/sec?sector=legend', filter_timestamp);
check('text/plain; charset=utf-8',
  'refs/sec_legend.t5tab',
  SERVICE_BASE + '/api/sec?sector=legend&type=TabDelimited');
check('text/plain; charset=Windows-1252',
  'refs/sec_legend.sec',
  SERVICE_BASE + '/api/sec?sector=legend&type=Legacy', filter_timestamp);
check('text/plain; charset=Windows-1252',
  'refs/sec_legend_nometa.sec',
  SERVICE_BASE + '/api/sec?sector=legend&type=Legacy&metadata=0');
check('text/plain; charset=Windows-1252',
  'refs/sec_legend_noheader.sec',
  SERVICE_BASE + '/api/sec?sector=legend&type=Legacy&header=0', filter_timestamp);
check('text/plain; charset=Windows-1252',
  'refs/sec_legend_sscoords.sec',
  SERVICE_BASE + '/api/sec?sector=legend&type=Legacy&sscoords=1', filter_timestamp);

// Metadata API

check('text/xml',
  'refs/metadata_legend.xml',
  SERVICE_BASE + '/api/metadata?sector=legend');
check('application/json',
  'refs/metadata_legend.json',
  SERVICE_BASE + '/api/metadata?sector=legend');

// MSEC API

check('text/plain; charset=utf-8',
  'refs/msec_legend.txt',
  SERVICE_BASE + '/api/msec?sector=legend', filter_timestamp);

// JumpWorlds API

check('text/xml',
  'refs/jumpworlds_legend_0602.xml',
  SERVICE_BASE + '/api/jumpworlds?sector=legend&hex=0602&jump=1');
check('application/json',
  'refs/jumpworlds_legend_0602.json',
  SERVICE_BASE + '/api/jumpworlds?sector=legend&hex=0602&jump=1');

// Universe API

check('text/xml',
  'refs/universe_meta.xml',
  SERVICE_BASE + '/api/universe?tag=meta');
check('application/json',
  'refs/universe_meta.json',
  SERVICE_BASE + '/api/universe?tag=meta');

// Sector Parsing

function sectorParserTest(input, expected) {
  runTest('SEC Source', 'Parsed Output', async () => {
    const response = await fetch(SERVICE_BASE + '/api/sec', { method: 'POST', body: input });
    if (!response.ok) throw new Error(response.statusText);
    const body = await response.text();
    const d = diff(expected, body);
    return {
      left: input,
      right: body,
      diff: d,
      pass: !d
    };
  });
}

// SEC -> SecondSurvey
sectorParserTest(
  'B-Class Star       0101 ???????-?                    ??? Na B2 II\n' +
  'Belts and Giants   0102 ???????-?                    ?BF Na\n',
  'Hex  Name                 UWP       Remarks              {Ix} (Ex) [Cx] N B Z PBG W A  Stellar\n' +
  '---- -------------------- --------- -------------------- ---- ---- ---- - - - --- - -- -------\n' +
  '0101 B-Class Star         ???????-?                                     - - - ???   Na B2 II  \n' +
  '0102 Belts and Giants     ???????-?                                     - - - ?BF   Na        \n');
