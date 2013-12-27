document.addEventListener('DOMContentLoaded', function() {
  'use strict';
  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return document.querySelectorAll(s); };

  var list = $('#sector');

  MapService.universe(populateSectorList, undefined, {requireData: 1});

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

  list.addEventListener("change", function (e) {
    var name = list.value;
    MapService.sectorData(name, function(data) {
      $('#data').value = data;
    }, undefined, {type: 'legacy', metadata: 0, header: 0});
    MapService.sectorMetaData(name, function(data) {
      $('#metadata').value = data;
    }, undefined, {accept: 'text/xml'});
  });

  function fetchInto(url, selector) {
    var xhr = new XMLHttpRequest();
    xhr.open("GET", url, true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState === 4 && xhr.status === 200) {
        $(selector).value = xhr.responseText;
      }
    };
    xhr.send(null);
  }

  [].forEach.call($$('textarea'), function(elem) {
    elem.addEventListener('dragover', function (e) {
      e.stopPropagation();
      e.preventDefault();
      e.dataTransfer.dropEffect = 'copy';
    });
    elem.addEventListener('drop', function (e) {
      e.stopPropagation();
      e.preventDefault();
      var reader = new FileReader();
      reader.readAsText(e.dataTransfer.files[0], 'windows-1252');
      reader.onload = function(e) {
        elem.value = e.target.result;
      };
    });
    elem.placeholder = 'Copy and paste data or drag and drop a file here.';
  });

});
