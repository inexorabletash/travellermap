// Common routines between Poster Maker and Booklet Maker
// * Populate sector selector, and load data on demand
// * Add drag-and-drop handlers for TEXTAREA elements

window.addEventListener('DOMContentLoaded', function() {
  'use strict';
  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return document.querySelectorAll(s); };

  var list = $('#sector');

  Traveller.MapService.universe(populateSectorList, undefined, {requireData: 1});

  function populateSectorList(universe) {
    universe.Sectors
      .filter(function(sector) {
        return Math.abs(sector.X) < 10 && Math.abs(sector.Y) < 5;
      })
      .map(function(sector) {
        return sector.Names[0].Text;
      })
      .sort()
      .forEach(function(name) {
        var option = document.createElement('option');
        option.appendChild(document.createTextNode(name));
        option.value = name;
        list.appendChild(option);
      });
  }

  list.addEventListener('change', function (e) {
    var name = list.value;
    MapService.sectorData(name, function(data) {
      var target = $('#data');
      if (target) target.value = data;
    }, undefined, {type: 'SecondSurvey', metadata: 0});
    MapService.sectorMetaData(name, function(data) {
      var target = $('#metadata');
      if (target) target.value = data;
    }, undefined, {accept: 'text/xml'});
  });

  Array.from($$('textarea.drag-n-drop')).forEach(function(elem) {
    elem.addEventListener('dragover', function (e) {
      e.stopPropagation();
      e.preventDefault();
      e.dataTransfer.dropEffect = 'copy';
    });
    elem.addEventListener('drop', function (e) {
      e.stopPropagation();
      e.preventDefault();
      blobToString(e.dataTransfer.files[0]).then(function(s) {
        elem.value = s;
      });
    });
    elem.placeholder = 'Copy and paste data or drag and drop a file here.';
  });

  function blobToString(blob) {
    return new Promise(function(resolve, reject) {
      var encodings = ['utf-8', 'windows-1252'];
      (function tryNextEncoding() {
        var encoding = encodings.shift();
        var reader = new FileReader();
        reader.readAsText(blob, encoding);
        reader.onload = function(e) {
          var result = reader.result;
          if (result.indexOf('\uFFFD') !== -1 && encodings.length)
            tryNextEncoding();
          else
            resolve(result);
        };
      }());
    });
  }
});

// |data| can be string (payload) or object (key/value form data)
// Returns Promise<string>
function getTextViaPOST(url, data) {
  var request;
  if (typeof data === 'string') {
    request = fetch(url, {
      method: 'POST',
      headers: {'Content-Type': 'text/plain'},  // Safari doesn't infer this.
      body: data
    });
  } else {
    data = Object(data);
    var fd = new FormData();
    Object.keys(data).forEach(function(key) { fd.append(key, data[key]); });
    request = fetch(url, {
      method: 'POST',
      body: fd
    });
  }
  return request.then(
    function(response) {
      if (response.status === 200)
        return response.text();
      return Promise.reject(response.text());
    }
  );
}
