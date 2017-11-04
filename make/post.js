/*global Traveller */ // for lint and IDEs

// Common routines between Poster Maker and Booklet Maker
// * Populate sector selector, and load data on demand
// * Add drag-and-drop handlers for TEXTAREA elements

window.addEventListener('DOMContentLoaded', function() {
  'use strict';
  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return document.querySelectorAll(s); };

  function cmp(a, b) { return a < b ? -1 : b < a ? 1 : 0; }

  var list = $('#sector');

  var seen = new Set();
  Traveller.MapService.universe({requireData: 1})
    .then(function(universe) {
      universe.Sectors
        .filter(function(sector) {
          return Math.abs(sector.X) < 10 && Math.abs(sector.Y) < 5;
        })
        .map(function(sector) {
          var name = sector.Names[0].Text;
          if (seen.has(name))
            name += ' (' + sector.Milieu + ')';
          seen.add(name);
          return {display: name,
                  name: sector.Names[0].Text,
                  milieu: sector.Milieu || ''};
        })
        .sort(function(a, b) { return cmp(a.display, b.display); })
        .forEach(function(record) {
          var option = document.createElement('option');
          option.appendChild(document.createTextNode(record.display));
          option.value = record.name + '|' + record.milieu;
          list.appendChild(option);
        });
    });

  list.addEventListener('change', function (e) {
    var s = list.value.split('|'), name = s[0], milieu = s[1] || undefined;
    Traveller.MapService.sectorData(name, {
      type: 'SecondSurvey', metadata: 0, milieu: milieu})
      .then(function(data) {
        var target = $('#data');
        if (target) target.value = data;
      });

    Traveller.MapService.sectorMetaData(name, {
      accept: 'text/xml', milieu: milieu})
      .then(function(data) {
        var target = $('#metadata');
        if (target) target.value = data;
      });
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
    Object.keys(data).forEach(function(key) {
      var value = data[key];
      if (value !== undefined && value !== null)
        fd.append(key, data[key]);
    });
    request = fetch(url, {
      method: 'POST',
      body: fd
    });
  }

  return request
    .then(function(response) {
      return Promise.all([response, response.text()]);
    })
    .then(function(values) {
      var response = values[0];
      var text = values[1];
      if (!response.ok)
        throw text;
      return text;
    });
}

function getJSONViaPOST(url, data) {
  return getTextViaPOST(url, data).then(function(text) { return JSON.parse(text); });
}
