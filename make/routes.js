"use strict";

var $ = function(s) { return document.querySelector(s); };


var PS = 16; // px/parsec
var INSET = 2; // px
var RADIUS = 4;

var sec = {};
var routes = [];


var canvas = $('#canvas'), ctx = canvas.getContext('2d');

$('#parsesec').addEventListener('click', parse);
$('#undo').addEventListener('click', undo);
$('#clear').addEventListener('click', clear);

var sec;
function parse() {
  var data = $('#data').value;
  if (!data.length) return;
  getTextViaPOST(
    Traveller.MapService.makeURL('/api/sec', {type: 'TabDelimited'}),
    data)
    .then(function(data) {
      return parseSector(data);
    })
    .then(function(sector) {
      sec = sector;
      var params = {
        data: $('#data').value,
        metadata: $('#metadata').value,
        style: 'print',
        options: 41975,
        scale: 64,
        datauri: 1,
        im: $('#highlight-im').checked ? 1 : 0,
        po: $('#highlight-po').checked ? 1 : 0
      };
      return getTextViaPOST(Traveller.MapService.makeURL('/api/poster'), params);
    })
    .then(function(dataURL) {
      $('#canvas').style.backgroundSize = '100% 100%';
      $('#canvas').style.backgroundImage = 'url("' + dataURL + '")';
      refresh();
    })
    .catch(function(reason) {
      alert('Server error: ' + reason);
    });
}

function parseSector(tabDelimitedData) {
  var sector = {
    worlds: {}
  };
  var lines = tabDelimitedData.split(/\r?\n/);
  var header = lines.shift().toLowerCase().split('\t')
    .map(function(h) { return h.replace(/[^a-z]/g, ''); });
  lines.forEach(function(line) {
    if (!line.length) return;
    var world = {};
    line.split('\t').forEach(function(field, index) {
      world[header[index]] = field;
    });
    sector.worlds[world.hex] = world;
  });
  return sector;
}


function refresh() {
  function hexToCoords(hex) {
    var x = parseFloat(hex.substring(0, 2)) - 1;
    var y = parseFloat(hex.substring(2, 4)) - 1;
    return hxhyToCoords(x, y);
  }
  function hxhyToCoords(hx, hy) {
    var x = hx, y = hy;
    var dy = (x % 2) ? 0.5 : 0;
    x *= Math.cos(Math.PI/6); // cos(30deg)
    return {x:x*PS+INSET+PS/2, y:(y+dy)*PS+INSET+PS/2};
  }

  ctx.clearRect(0, 0, PS * canvas.width, PS * canvas.height);

  ctx.lineWidth = 2;
  ctx.strokeStyle = 'red';
  stack.forEach(function(hex) {
    var coords = hexToCoords(hex), x = coords.x, y = coords.y;
    ctx.beginPath();
    ctx.arc(x,
            y,
            RADIUS + 2, 0, 2 * Math.PI, false);
    ctx.stroke();
  });


  ctx.lineWidth = 4;
  ctx.strokeStyle = "green";
  routes.forEach(function(route) {
    ctx.beginPath();
    var start = hexToCoords(route.start), sx = start.x, sy = start.y;
    ctx.moveTo(sx, sy);
    var end = hexToCoords(route.end), ex = end.x, ey = end.y;
    ctx.lineTo(ex, ey);
    ctx.stroke();
  });

  var template = ($('#form').elements.metatype.value === 'xml') ? xml_template : msec_template;
  $('#metadata_generated').value = template({routes:routes});

  ctx.fillStyle = 'black';
}

var xml_template = Handlebars.compile($('#xml-template').innerHTML.trim());
var msec_template = Handlebars.compile($('#msec-template').innerHTML.trim());

[$('#xml'), $('#msec')].forEach(function(e) {
  e.addEventListener('click', refresh);
});

var stack = [];
$('#canvas').addEventListener('mousedown', function(e) {
  e.preventDefault();
  e.stopPropagation();

  var offsetX = 'offsetX' in e ? e.offsetX :
    'layerX' in e ? e.layerX :
    e.pageX - e.target.offsetLeft;
  var offsetY = 'offsetY' in e ? e.offsetY :
    'layerY' in e ? e.layerY :
    e.pageY - e.target.offsetTop;
  var x = offsetX, y = offsetY;

  x = (x - INSET) / PS / Math.cos(Math.PI/6);
  y = (y - INSET) / PS;
  x = Math.floor(x);
  if (x % 2) y -= 0.5;
  y = Math.floor(y);
  var hex = ('00' + (x+1)).slice(-2) + ('00' + (y+1)).slice(-2);

  if (stack.length) {
    var start = stack.pop();
    if (start !== hex)
      routes.push({start: start, end: hex});
  } else {
    stack.push(hex);
  }
  refresh();
});

function undo() {
  if (stack.length)
    stack.pop();
  else if (routes.length)
    stack.push(routes.pop().start);
  refresh();

}

function clear() {
  stack.length = 0;
  routes.length = 0;
  refresh();
}

parse();
refresh();
