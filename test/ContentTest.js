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

  // TODO: Use a proper polyfill for fetch/Response from http://fetch.spec.whatwg.org
  function Response(xhr) {
    this._xhr = xhr;
    var headers = {};
    xhr.getAllResponseHeaders().split(/\r?\n/).forEach(function(header) {
      headers[header.substring(0, header.indexOf(':'))] = header.substring(header.indexOf(':') + 2);
    });
    this.headers = headers;
  }
  Response.prototype = {
    asText: function() {
      return Promise.resolve(this._xhr.responseText);
    },
    asXML: function() {
      return Promise.resolve(this._xhr.responseXML);
    }
  };
  function fetch(url, options) {
    return new Promise(function(resolve, reject) {
      var xhr = new XMLHttpRequest(), async = true;
      xhr.open(options && options.method || 'GET', url, async);
      xhr.onreadystatechange = function() {
        if (xhr.readyState !== XMLHttpRequest.DONE)
          return;
        if (xhr.status === 200)
          resolve(new Response(xhr));
        else
          reject(xhr.statusText);
      };
      xhr.send();
    });
  }

  function compareContent(contentType, url1, url2, callback) {

    function getHeaderAndBody(response) {
      return response.asText().then(function(text) {
        return {
          headers: response.headers,
          text: text
        };
      });
    }

    Promise.all([fetch(url1).then(getHeaderAndBody), fetch(url2).then(getHeaderAndBody)])
      .then(function(responses) {
        var a = 'Content-Type: ' + contentType + '\n'
              + responses[0].text;
        var b = 'Content-Type: ' + responses[1].headers['Content-Type'] + '\n'
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

    compareContent(contentType, '../' + url1, '../' + url2, function(a, b, c, pass) {
      tr.appendChild(elem('td')).appendChild(text(a));
      tr.appendChild(elem('td')).appendChild(text(b));
      tr.appendChild(elem('td')).appendChild(text(c));
      tr.classList.add(pass ? 'pass' : 'fail');
    });
  }

  global.check = check;

}(self));
