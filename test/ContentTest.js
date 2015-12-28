(function(global) {

  // http://rosettacode.org/wiki/Longest_Common_Substring
  function lcs(a, b) {
    var len = 0, oa = 0, ob = 0;;

    var lengths = [];
    lengths.length = a.length * b.length;
    lengths.fill(0);

    for (var i = 0; i < a.length; ++i) {
      for (var j = 0; j < b.length; j++) {
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
    var r = lcs(as, bs);
    // Nothing common
    if (!r.len) {
      var result = [];
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
          b = filter(b);
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
