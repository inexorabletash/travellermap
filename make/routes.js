"use strict";

const $ = s => document.querySelector(s);

const PS = 16; // px/parsec
const INSET = 2; // px
const RADIUS = 4;

let sec = {};
const routes = [];

const canvas = $('#canvas'), ctx = canvas.getContext('2d');

$('#parsesec').addEventListener('click', parse);
$('#undo').addEventListener('click', undo);
$('#clear').addEventListener('click', clear);

async function parse() {
  try {
    const data = $('#data').value;
    if (!data.length) return;
    const text = await getTextViaPOST(
        Traveller.MapService.makeURL('/api/sec', {type: 'TabDelimited'}),
      data);
    const sector = await parseSector(text);
    sec = sector;
    const dataURL = await getTextViaPOST(
      Traveller.MapService.makeURL('/api/poster'), {
        data: $('#data').value,
        metadata: $('#metadata').value,
        style: 'print',
        options: 41975,
        scale: 64,
        datauri: 1,
        im: $('#highlight-im').checked ? 1 : 0,
        po: $('#highlight-po').checked ? 1 : 0
      });
    $('#canvas').style.backgroundSize = '100% 100%';
    $('#canvas').style.backgroundImage = 'url("' + dataURL + '")';
    refresh();
  } catch (reason) {
    alert('Server error: ' + reason);
  }
}

function parseSector(tabDelimitedData) {
  const sector = {
    worlds: {}
  };
  const lines = tabDelimitedData.split(/\r?\n/);
  const header = lines
          .shift()
          .toLowerCase()
          .split('\t')
          .map(h => h.replace(/[^a-z]/g, ''));
  lines.forEach(line => {
    if (!line.length) return;
    const world = {};
    line
      .split('\t')
      .forEach((field, index) => {
        world[header[index]] = field;
      });
    sector.worlds[world.hex] = world;
  });
  return sector;
}


function refresh() {
  function hexToCoords(hex) {
    const x = parseFloat(hex.substring(0, 2)) - 1;
    const y = parseFloat(hex.substring(2, 4)) - 1;
    return hxhyToCoords(x, y);
  }
  function hxhyToCoords(hx, hy) {
    let x = hx, y = hy;
    const dy = (x % 2) ? 0.5 : 0;
    x *= Math.cos(Math.PI/6); // cos(30deg)
    return {x:x*PS+INSET+PS/2, y:(y+dy)*PS+INSET+PS/2};
  }

  ctx.clearRect(0, 0, PS * canvas.width, PS * canvas.height);

  ctx.lineWidth = 2;
  ctx.strokeStyle = 'red';
  stack.forEach(hex => {
    const coords = hexToCoords(hex), x = coords.x, y = coords.y;
    ctx.beginPath();
    ctx.arc(x,
            y,
            RADIUS + 2, 0, 2 * Math.PI, false);
    ctx.stroke();
  });

  ctx.lineWidth = 4;
  ctx.strokeStyle = "green";
  routes.forEach(route => {
    ctx.beginPath();
    const start = hexToCoords(route.start),
          sx = start.x,
          sy = start.y;
    ctx.moveTo(sx, sy);
    const end = hexToCoords(route.end),
          ex = end.x,
          ey = end.y;
    ctx.lineTo(ex, ey);
    ctx.stroke();
  });

  const template = ($('#form').elements.metatype.value === 'xml')
          ? xml_template : msec_template;
  $('#metadata_generated').value = template({routes:routes});

  ctx.fillStyle = 'black';
}

const xml_template = Handlebars.compile($('#xml-template').innerHTML.trim());
const msec_template = Handlebars.compile($('#msec-template').innerHTML.trim());

[$('#xml'), $('#msec')].forEach(e => {
  e.addEventListener('click', refresh);
});

const stack = [];
$('#canvas').addEventListener('mousedown', e => {
  e.preventDefault();
  e.stopPropagation();

  const offsetX = 'offsetX' in e ? e.offsetX :
    'layerX' in e ? e.layerX :
    e.pageX - e.target.offsetLeft;
  const offsetY = 'offsetY' in e ? e.offsetY :
    'layerY' in e ? e.layerY :
    e.pageY - e.target.offsetTop;
  let x = offsetX, y = offsetY;

  x = (x - INSET) / PS / Math.cos(Math.PI/6);
  y = (y - INSET) / PS;
  x = Math.floor(x);
  if (x % 2) y -= 0.5;
  y = Math.floor(y);
  const hex = ('00' + (x+1)).slice(-2) + ('00' + (y+1)).slice(-2);

  if (stack.length) {
    const start = stack.pop();
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
