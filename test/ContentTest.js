(function(global) {

  function idiff(as, bs) {
    // Map line -> index
    var map = new Map();
    as.forEach(function(a, i) {
      if (!map.has(a)) map.set(a, []);
      map.get(a).push(i);
    });

    var common = [];
    var ssa = 0;
    var ssb = 0;
    var len = 0;

    // Find largest common substring
    bs.forEach(function(b, idx) {
      if (!map.has(b)) return;
      var tmp = [];
      map.get(b).forEach(function(i) {
        tmp[i] = ((i && common[i - 1]) || 0) + 1;
        if (tmp[i] > len) {
          len = tmp[i];
          ssa = i - len + 1;
          ssb = idx - len + 1;
        }
      });
      common = tmp;
    });

    // Nothing common
    if (!len) {
      var result = [];
      if (as.length) result.push(['-', as]);
      if (bs.length) result.push(['+', bs]);
      return result;
    }

    // Recurse
    return [].concat(
      idiff(as.slice(0, ssa), bs.slice(0, ssb)),
      [['=', bs.slice(ssb, ssb + len)]],
      idiff(as.slice(ssa + len), bs.slice(ssb + len))
    );
  }

  function diff(text1, text2) {
    var lines1 = text1.split(/\r?\n/);
    var lines2 = text2.split(/\r?\n/);
    var out = [];
    idiff(lines1, lines2)
      .filter(function(pair) { return pair[0] !== '='; })
      .forEach(function(pair) {
        var op = pair[0];
        pair[1].forEach(function(line) { out.push(op + ' ' + line); });
      });
    return out.join('\n');;
  }


  function compareContent(contentType, url1, url2, filter, callback) {

    function getHeaderAndBody(response) {
      return response.text().then(function(text) {
        return {
          headers: response.headers,
          text: text
        };
      });
    }

    var requestContentType = contentType.replace(/;.*/, '');

    return Promise.all([fetch(url1).then(getHeaderAndBody),
                 fetch(url2, {headers: {Accept: requestContentType }}).then(getHeaderAndBody)])
      .then(function(responses) {
        var a = 'Content-Type: ' + contentType + '\n'
              + responses[0].text;
        var b = 'Content-Type: ' + responses[1].headers.get('Content-Type') + '\n'
              + responses[1].text;
        if (filter) {
          a = filter(a);
          window.b_1 = b;
          b = filter(b);
          window.b_2 = b;
          window.f = filter;
        }
        var d = diff(a, b);
        callback(a, b, d, !d);
        return !d;
      }, function(error) {
        callback('', '', 'Fetch failed: ' + error);
        return false;
      });
  }

  var $ = function(selector) { return document.querySelector(selector); };
  var $$ = function(selector) { return document.querySelectorAll(selector); };
  function elem(tag, attribs) {
    var e = document.createElement(tag);
    if (attribs) Object.keys(attribs).forEach(function(key) {
      e.setAttribute(key, attribs[key]);
    });
    return e;
  }
  function text(text) { return document.createTextNode(text); }
  function link(uri) {
    var a = elem('a', {href: uri});
    a.appendChild(text(uri));
    return a;
  }

  var status = {
    tests: 0,
    completed: 0,
    passed: 0
  };
  function update() {
    $('#status_tests').innerHTML = String(status.tests);
    $('#status_passed').innerHTML = String(status.passed);
    $('#status_failed').innerHTML = String(status.completed - status.passed);
  }

  global.check = function(contentType, url1, url2, filter) {
    ++status.tests;

    var tr = $('#results').appendChild(elem('tr'));
    tr.appendChild(elem('td')).appendChild(link(url1));
    tr.appendChild(elem('td')).appendChild(link(url2));
    tr.appendChild(elem('td')).appendChild(text('diff'));

    tr = $('#results').appendChild(elem('tr'));

    compareContent(contentType, url1, url2, filter, function(a, b, c, pass) {
      tr.appendChild(elem('td')).appendChild(elem('textarea')).value = a;
      tr.appendChild(elem('td')).appendChild(elem('textarea')).value = b;
      tr.appendChild(elem('td')).appendChild(elem('textarea')).value = c;
      tr.classList.add(pass ? 'pass' : 'fail');
    }).then(function(result) {
      ++status.completed;
      if (result) {
        ++status.passed;
      }
      update();
    });
  };

  global.filter_timestamp = function(s) {
    return s.replace(/# \d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d.*\r?\n/g, '# <TIMESTAMP>');
  };

}(self));
