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


  function compareContent(contentType, url1, url2, callback) {

    function getHeaderAndBody(response) {
      return response.text().then(function(text) {
        return {
          headers: response.headers,
          text: text
        };
      });
    }

    Promise.all([fetch(url1).then(getHeaderAndBody),
                 fetch(url2, {headers: {Accept: contentType }}).then(getHeaderAndBody)])
      .then(function(responses) {
        var a = 'Content-Type: ' + contentType + '\n'
              + responses[0].text;
        var b = 'Content-Type: ' + responses[1].headers.get('Content-Type') + '\n'
              + responses[1].text;
        var d = diff(a, b);
        callback(a, b, d, !d);
      }).catch(function(error) {
        callback('', '', 'Fetch failed: ' + error);
        // TODO: Handle error
      });
  }

  var $ = function(selector) { return document.querySelector(selector); };
  function elem(tag) { return document.createElement(tag); }
  function text(text) { return document.createTextNode(text); }

  function check(contentType, url1, url2) {
    var tr = $('#results').appendChild(elem('tr'));
    tr.appendChild(elem('td')).appendChild(text(url1));
    tr.appendChild(elem('td')).appendChild(text(url2));
    tr.appendChild(elem('td')).appendChild(text('diff'));

    tr = $('#results').appendChild(elem('tr'));

    compareContent(contentType, url1, url2, function(a, b, c, pass) {
      tr.appendChild(elem('td')).appendChild(elem('textarea')).value = a;
      tr.appendChild(elem('td')).appendChild(elem('textarea')).value = b;
      tr.appendChild(elem('td')).appendChild(elem('textarea')).value = c;
      tr.classList.add(pass ? 'pass' : 'fail');
    });
  }

  global.check = check;

}(self));
