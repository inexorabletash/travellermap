(global => {

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
    return {len: len, oa: oa, ob: ob};
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
      [['=', bs.slice(r.ob, r.ob + r.len)]],
      idiff(as.slice(r.oa + r.len), bs.slice(r.ob + r.len))
    );
  }

  global.diff = function(text1, text2) {
    function removeNamespaces(s) {
      return s
        .replace(/\s+xmlns:xsd="http:\/\/www\.w3\.org\/2001\/XMLSchema"/g, '')
        .replace(/\s+xmlns:xsi="http:\/\/www\.w3\.org\/2001\/XMLSchema-instance"/g, '');
    }

    const lines1 = removeNamespaces(text1).split(/\r?\n/);
    const lines2 = removeNamespaces(text2).split(/\r?\n/);
    const out = [];
    idiff(lines1, lines2)
      .filter(function(pair) { return pair[0] !== '='; })
      .forEach(function(pair) {
        const op = pair[0];
        pair[1].forEach(function(line) { out.push(op + ' ' + line); });
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
      fetch(url2, {headers: {Accept: requestContentType }}).then(getHeaderAndBody)]);

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
    if (attribs) Object.keys(attribs).forEach(function(key) {
      e.setAttribute(key, attribs[key]);
    });
    return e;
  }
  function text(text) { return document.createTextNode(text); }
  function link(uri) {
    const a = elem('a', {href: uri});
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

  global.runTest = async (leftTitle, rightTitle, func) => {
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

    tr2.appendChild(elem('td')).appendChild(elem('textarea', {wrap: 'off'})).value = results.left;
    tr2.appendChild(elem('td')).appendChild(elem('textarea', {wrap: 'off'})).value = results.right;
    tr2.appendChild(elem('td')).appendChild(elem('textarea', {wrap: 'off'})).value = results.diff;

    ++status.completed;
    if (results.pass)
      ++status.passed;
    update();
  };

  global.check = (contentType, url1, url2, filter) => {
    global.runTest(url1, url2, async function() {
      try {
        const pair = await fetchPair(contentType, url1, url2);
        let a = pair[0], b = pair[1];
        if (filter) {
          a = filter(a);
          b = filter(b);
        }
        const d = global.diff(a, b);
        return {left: a, right: b, diff: d, pass: !d};
      } catch(error) {
        return {left: '', right: '', diff: 'Fetch failed: ' + error, pass: false};
      }
    });
  };

  global.filter_timestamp = s => {
    return s.replace(/# \d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d.*\r?\n/g, '# <TIMESTAMP>');
  };

})(self);
