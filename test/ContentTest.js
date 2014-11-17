(function(global) {

  function diff(text1, text2) {
    var lines1 = text1.split(/\r?\n/);
    var lines2 = text2.split(/\r?\n/);

    // TODO: Real diff - for now this just ignores leading/trailing matches
    while (lines1.length && lines2.length) {
      if (lines1[0] === lines2[0]) {
        lines1.shift();
        lines2.shift();
        continue;
      }
      if (lines1[lines1.length - 1] === lines2[lines2.length - 1]) {
        lines1.pop();
        lines2.pop();
        continue;
      }
      break;
    }

    var out = [];
    while (lines1.length && lines2.length) {
      out.push('- ' + lines1.shift());
      out.push('+ ' + lines2.shift());
    }
    while (lines1.length)
      out.push('- ' + lines1.shift());
    while (lines2.length)
      out.push('+ ' + lines2.shift());

    return out.join('\n');
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

    return Promise.all([fetch(url1).then(getHeaderAndBody),
                 fetch(url2, {headers: {Accept: contentType }}).then(getHeaderAndBody)])
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
