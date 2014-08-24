(function(global) {

  // This is a very, very coarse approximation of the fetch API
  // http://fetch.spec.whatwg.org/

  function fetch(input, init) {
    return new Promise(function(resolve, reject) {
      var url = String(input);

      init = init || {};
      var method = init.method || 'GET';
      var headers = init.headers || {};
      var body = init.body || null;

      var xhr = new XMLHttpRequest(), async = true;
      xhr.open(method, url, async);
      Object.keys(init.headers || {}).forEach(function(key) {
        xhr.setRequestHeader(key, headers[key]);
      });
      xhr.onreadystatechange = function () {
        if (xhr.readyState !== XMLHttpRequest.DONE) return;
        if (xhr.status === 200)
          resolve(xhr);
        else
          reject(xhr);
      };
      xhr.send(body);
    });
  }

  global.fetch = fetch;

}(self));
