/*global Handlebars, Traveller, getTextViaPOST */

"use strict";

const $ = s => document.querySelector(s);

const PS = 16; // px/parsec
const INSET = 2; // px
const RADIUS = 4;

let sec = {worlds:{}};
let routes = [];
let candidates = [];

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
    candidates = [];
    routes = [];
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

function refresh() {
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

  ctx.lineWidth = 2;
  ctx.strokeStyle = 'blue';
  candidates.forEach(hex => {
    const coords = hexToCoords(hex), x = coords.x, y = coords.y;
    ctx.beginPath();
    ctx.arc(x,
            y,
            RADIUS + 2, 0, 2 * Math.PI, false);
    ctx.stroke();
  });

  ctx.lineWidth = 4;
  ctx.strokeStyle = "green";
  ctx.fillStyle = "green";
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

    ctx.beginPath();
    ctx.arc(sx, sy, RADIUS, 0, 2 * Math.PI, false);
    ctx.arc(ex, ey, RADIUS, 0, 2 * Math.PI, false);
    ctx.fill();
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

$('#auto-kk').addEventListener('click', () => { auto('kk'); });
$('#auto-zh').addEventListener('click', () => { auto('zh'); });

function auto(t) {
  switch (t) {
  case 'kk':
    autoConnect(Object.values(sec.worlds).filter(world => {
      if (!world.allegiance.startsWith('Kk')) return false;
      if (world.uwp[0] !== 'A') return false;
      return true;
    }), 3);
    break;
  case 'zh':
    autoConnect(Object.values(sec.worlds).filter(world => {
      if (!world.allegiance.startsWith('Zh')) return false;
      return ['K', 'M', 'D', 'W'].some(b => world.bases.includes(b));
    }), 4);
    break;
  }
}

function autoConnect(worlds, range) {
  routes = [];
  candidates = worlds.map(w => w.hex);

  // Examine each pair
  for (let i = 0; i < worlds.length - 1; ++i) {
    for (let j = i + 1; j < worlds.length; ++j) {
      const hex1 = worlds[i].hex,
            hex2 = worlds[j].hex;
      if (dist(hex1, hex2) <= range) {
        routes.push({start: hex1, end: hex2});
      }
    }
  }
  removeIntersections();
  refresh();
}

// Distance in hexes
// dist('0101', '0404') -> 5
function dist(a, b) {
  a = Number(a);
  b = Number(b);
  const a_x = div(a,100);
  const a_y = mod(a,100);
  const b_x = div(b,100);
  const b_y = mod(b,100);

  const dx = b_x - a_x;
  const dy = b_y - a_y;

  let adx = Math.abs(dx);
  let ody = dy + div(adx, 2);

  if (odd(a_x) && even(b_x))
    ody += 1;

  return max(adx - ody, ody, adx);

  function even(x) { return (x % 2) == 0; }
  function odd (x) { return (x % 2) != 0; }

  function div(a, b) { return Math.floor(a / b); }
  function mod(a, b) { return Math.floor(a % b); }

  function max(a, b, c) { return (a >= b && a >= c) ? a : (b >= a && b >= c) ? b : c; }
}


$('#nointersect').addEventListener('click', () => { removeIntersections(); refresh(); });
function removeIntersections() {
  const len = r => dist(r.start, r.end);

  // Remove degenerate routes
  routes = routes.filter(route => len(route) > 0);

  // Sort by length (reversed), so shortest routes are removed
  routes.sort((a,b) => len(b) - len(a));

  // Remove any intersecting routes
  for (let i = 0; i < routes.length - 1; ++i) {
    for (let j = i + 1; j < routes.length;) {
      if (intersects(routes[i], routes[j])) {
        routes.splice(j, 1);
      } else {
        ++j;
      }
    }
  }

  function intersects(route1, route2) {
    // Filter identical routes
    if (route1.start === route2.start && route1.end === route2.end) return true;
    if (route1.start === route2.end && route1.end === route2.start) return true;

    // But allow endpoints to touch
    if (route1.start === route2.start) return false;
    if (route1.end   === route2.end  ) return false;
    if (route1.start === route2.end  ) return false;
    if (route1.end   === route2.start) return false;

    // Look for true intersections
    const coords0 = hexToCoords(route1.start);
    const coords1 = hexToCoords(route1.end);
    const coords2 = hexToCoords(route2.start);
    const coords3 = hexToCoords(route2.end);

    return segmentIntersect(coords0.x, coords0.y,
                            coords1.x, coords1.y,
                            coords2.x, coords2.y,
                            coords3.x, coords3.y);
  }


  // https://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect
  function segmentIntersect(p0_x, p0_y, p1_x, p1_y, p2_x, p2_y, p3_x, p3_y) {
    let s1_x, s1_y, s2_x, s2_y;
    s1_x = p1_x - p0_x;     s1_y = p1_y - p0_y;
    s2_x = p3_x - p2_x;     s2_y = p3_y - p2_y;

    let s, t;
    s = (-s1_y * (p0_x - p2_x) + s1_x * (p0_y - p2_y)) / (-s2_x * s1_y + s1_x * s2_y);
    t = ( s2_x * (p0_y - p2_y) - s2_y * (p0_x - p2_x)) / (-s2_x * s1_y + s1_x * s2_y);

    return (s >= 0 && s <= 1 && t >= 0 && t <= 1);
  }
}

parse();
refresh();
