document.addEventListener('DOMContentLoaded', function() {
  'use strict';
  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return document.querySelectorAll(s); };

  var list = $('#sector');

  Traveller.MapService.universe(populateSectorList, undefined, {requireData: 1});

  function populateSectorList(universe) {
    var names = [];
    universe.Sectors.forEach(function (sector) {
      var x = sector.X, y = sector.Y, name = sector.Names[0].Text;
      if (Math.abs(x) < 10 && Math.abs(y) < 5) {
        names.push(name);
      }
    });
    names.sort();
    names.forEach(function (name) {
      var option = document.createElement('option');
      option.appendChild(document.createTextNode(name));
      option.value = name;
      list.appendChild(option);
    });
  }

  list.addEventListener('change', function (e) {
    var name = list.value;
    Traveller.MapService.sectorData(name, function(data) {
      $('#data').value = data;
    }, undefined, {type: 'SecondSurvey', metadata: 0});
    Traveller.MapService.sectorMetaData(name, function(data) {
      $('#metadata').value = data;
    }, undefined, {accept: 'text/xml'});
  });

  Array.from($$('textarea')).forEach(function(elem) {
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
