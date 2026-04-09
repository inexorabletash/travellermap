import {
  AllegianceMap,
  breakSpans,
  claimAllUnclaimed,
  erode,
  NON_ALIGNED,
  processMap,
  UNALIGNED,
  walk
} from '../borders/borders.js';

const $ = s => document.querySelector(s);

const allegianceMap = {
  'Im' : 'red',
  'As' : 'yellow',
  'Kk' : 'green',
  'Va' : 'olive',
  'Zh' : 'cyan',
  'So' : 'orange',
  'Hv' : 'purple',
  'Na' : 'gray'
};

const g_map = new AllegianceMap(16, 20, 1, 1);

//----------------------------------------------------------------------
//
// View
//
//----------------------------------------------------------------------

function makeMapDisplay(containerElement, map) {
  const fragments = [];
  let fragment;

  const sz = 20;
  const pad = 5;
  let top, left = -sz;

  for (let x = g_map.origin_x; x < g_map.origin_x + g_map.width; ++x) {
    left += (sz + pad);
    top = (x % 2 ? 0 : (sz + pad) / 2) - sz;

    for (let y = g_map.origin_y; y < g_map.origin_y + g_map.height; ++y) {
      top += (sz + pad);

      fragment = `<div class='hex' id='hex_${x}_${y}' style='width: ${
          sz}px; height: ${sz}px; left: ${left}px; top: ${top}px;'>`;
      fragment += hexContents(x, y, map);
      fragment += `</div>`;
      fragments.push(fragment);
    }
  }

  containerElement.innerHTML = fragments.join("");
  containerElement.style.width =
      (g_map.width * sz + (g_map.width + 1) * pad) + "px";
  containerElement.style.height =
      ((g_map.height + 0.5) * sz + (g_map.height + 1.5) * pad) + "px";
}

function updateHex(x, y) {
  // Update view
  $(`#hex_${x}_${y}`).innerHTML = hexContents(x, y, g_map);
}

function updateView() {
  g_map.foreach((x, y) => { updateHex(x, y); });
}

function updateWalks() {
  const borders = [];
  let visited = {}

  for (let x = g_map.origin_x; x < g_map.origin_x + g_map.width; x += 1) {
    let last_alleg = UNALIGNED;
    for (let y = g_map.origin_y; y < g_map.origin_y + g_map.height; y += 1) {
      const label = hexLabel(x, y);
      const alleg = g_map.getAllegiance(x, y);
      if (alleg !== UNALIGNED && alleg !== NON_ALIGNED &&
          alleg !== last_alleg && !(label in visited)) {

        let path = walk(g_map, x, y, alleg);
        let pathLabels = path.map(hex => hexLabel(hex[0], hex[1]));
        pathLabels.forEach(label => { visited[label] = true; });

        borders.push({allegiance : alleg, path : pathLabels});
      }
      last_alleg = alleg;
    }
  }

  borders.sort((a, b) => a.allegiance < b.allegiance   ? -1
                         : a.allegiance > b.allegiance ? 1
                                                       : 0);

  const html = [];
  for (const border of borders) {
    html.push(`<li>${border.allegiance} : ${border.path.join(" ")}</li>`);
  };
  $('#walks').innerHTML = html.join("");
}

function hexLabel(x, y) {
  return (x < 10 ? "0" : "") + x + (y < 10 ? "0" : "") + y;
}

function hexContents(x, y, map) {
  const hexNumber = hexLabel(x, y);
  const occupied = map.isOccupied(x, y);
  const alleg = map.getAllegiance(x, y);
  const color = (alleg == UNALIGNED) ? "transparent" : allegianceMap[alleg];

  return `<div class='hexContents' style='background-color: ${color};'>
    <span class='hexNumber'>${hexNumber}</span><br/>
    ${occupied ? "<span class='world'>&bull;</span>" : ""}
  </div>`;
}

//----------------------------------------------------------------------
//
// Interaction
//
//----------------------------------------------------------------------

function selectedAllegiance() {
  const select = $('#allegiance');
  return select.options[select.selectedIndex].value;
}

function clickHex(x, y) {
  const occupied = !g_map.isOccupied(x, y);
  g_map.setOccupied(x, y, occupied);
  if (occupied) {
    g_map.setAllegiance(x, y, selectedAllegiance());
  } else {
    g_map.setAllegiance(x, y, UNALIGNED);
  }

  updateHex(x, y);
}

//----------------------------------------------------------------------
//
// Initialization
//
//----------------------------------------------------------------------

window.addEventListener('DOMContentLoaded', () => {
  $('#btnClaimAll').addEventListener('click', () => {
    claimAllUnclaimed(g_map, selectedAllegiance());
    updateView();
  });

  $('#btnErode').addEventListener('click', () => {
    erode(g_map, selectedAllegiance(), 3);
    updateView();
  });

  $('#btnBreakSpans').addEventListener('click', () => {
    breakSpans(g_map, selectedAllegiance(), 4);
    updateView();
  });

  $('#btnRun').addEventListener('click', () => { run(); });

  $('#btnClearMap').addEventListener('click', () => { clearMap(); });

  const view = $('#map');
  makeMapDisplay(view, g_map);

  view.addEventListener('click', event => {
    let target = (event.target)       ? event.target
                 : (event.srcElement) ? event.srcElement
                                      : null;
    while (target) {
      if (target && target.id) {
        const coords = target.id.split("_");
        if (coords[0] == "hex") {
          clickHex(coords[1], coords[2]);
          break;
        }
      }
      target = target.parentNode;
    }
  });
});

function clearMap() {
  g_map.foreach((x, y) => {
    g_map.setAllegiance(x, y, UNALIGNED);
    g_map.setOccupied(x, y, false);

    updateHex(x, y);
  });
}

function run() {
  processMap(g_map, () => {
    updateView();
    updateWalks();
  });
}