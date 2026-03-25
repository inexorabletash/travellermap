import { computeRoute } from './path.js';
import { getTextViaPOST } from './post.js';
import * as Traveller from '../map.js';

const Util = Traveller.Util;
const $ = Util.$;
const $$ = Util.$$;

const PS = 16; // px/parsec
const INSET = 2; // px
const RADIUS = 4;

let sec;

const canvas = $('#canvas'), ctx = canvas.getContext('2d');

$('#parsesec').addEventListener('click', parse);

async function parse() {
  const data = $('#data').value;
  if (!data.length) return;
  const text = await getTextViaPOST(
    Traveller.MapService.makeURL('/api/sec', { type: 'TabDelimited' }),
    data
  );
  sec = Util.parseSector(text);
  const dataURL = await getTextViaPOST(Traveller.MapService.makeURL('/api/poster'), {
    data: $('#data').value,
    metadata: $('#metadata').value,
    style: 'print',
    options: 41975,
    scale: 64,
    datauri: 1
  });
  $('#canvas').style.backgroundSize = '100% 100%';
  $('#canvas').style.backgroundImage = `url("${dataURL}")`;
  refresh();
}

/** @type {string[]|undefined} */
let route;

function refresh() {
  function hexToCoords(hex) {
    const x = parseFloat(hex.substring(0, 2)) - 1;
    const y = parseFloat(hex.substring(2, 4)) - 1;
    return hxhyToCoords(x, y);
  }
  function hxhyToCoords(hx, hy) {
    let x = hx, y = hy;
    const dy = (x % 2) ? 0.5 : 0;
    x *= Math.cos(Math.PI / 6); // cos(30deg)
    return { x: x * PS + INSET + PS / 2, y: (y + dy) * PS + INSET + PS / 2 };
  }

  ctx.clearRect(0, 0, PS * canvas.width, PS * canvas.height);

  ctx.lineWidth = 2;
  ctx.strokeStyle = 'red';
  for (const hex of stack) {
    const coords = hexToCoords(hex);
    ctx.beginPath();
    ctx.arc(coords.x,
      coords.y,
      RADIUS + 2, 0, 2 * Math.PI, false);
    ctx.stroke();
  }

  let out = '';
  if (route) {
    ctx.lineWidth = 2;
    ctx.strokeStyle = 'rgba(0,128,0,0.5)';
    for (const hex of route) {
      const coords = hexToCoords(hex);
      ctx.beginPath();
      ctx.arc(coords.x,
        coords.y,
        RADIUS + 2, 0, 2 * Math.PI, false);
      ctx.stroke();
    }

    ctx.lineWidth = 4;
    ctx.strokeStyle = "green";
    ctx.beginPath();
    for (const [index, hex] of route.entries()) {
      const coords = hexToCoords(hex);
      if (index === 0) {
        ctx.moveTo(coords.x, coords.y);
        out += hex;
      } else {
        ctx.lineTo(coords.x, coords.y);
        out += ' -> ' + hex;
      }
    }
    ctx.stroke();
  } else {
    out = 'No route found.';
  }
  $('#out').value = out;
}


const stack = [];
$('#canvas').addEventListener('mousedown', event => {
  event.preventDefault();
  event.stopPropagation();

  const offsetX = 'offsetX' in event ? event.offsetX :
    'layerX' in event ? event.layerX :
      event.pageX - event.target.offsetLeft;
  const offsetY = 'offsetY' in event ? event.offsetY :
    'layerY' in event ? event.layerY :
      event.pageY - event.target.offsetTop;
  let x = offsetX, y = offsetY;

  x = (x - INSET) / PS / Math.cos(Math.PI / 6);
  y = (y - INSET) / PS;
  x = Math.floor(x);
  if (x % 2) y -= 0.5;
  y = Math.floor(y);
  const hex = ('00' + (x + 1)).slice(-2) + ('00' + (y + 1)).slice(-2);

  if (stack.length) {
    const start = stack.pop();
    const jump = Number($('#jump').value);
    route = computeRoute(sec.worlds, start, hex, jump);
  } else {
    stack.push(hex);
  }
  refresh();
});

parse();
refresh();
