/*global Traveller */ // for lint and IDEs

// Common routines for various Makers:
// * Populate sector selector, and load data on demand
// * Add drag-and-drop handlers for TEXTAREA elements

'use strict';

window.addEventListener('DOMContentLoaded', async event => {
  const $ = s => document.querySelector(s);
  const $$ = s => Array.from(document.querySelectorAll(s));

  const cmp = (a, b) => a < b ? -1 : b < a ? 1 : 0;

  const list = $('#sector');

  const seen = new Set();
  const universe = await Traveller.MapService.universe({requireData: 1});
  universe.Sectors
    .filter(sector => Math.abs(sector.X) < 10 && Math.abs(sector.Y) < 7)
    .map(sector => {
      let name = sector.Names[0].Text;
      if (seen.has(name))
        name += ` (${sector.Milieu})`;
      seen.add(name);
      return {
        display: name,
        name: sector.Names[0].Text,
        milieu: sector.Milieu || ''
      };
    })
    .sort((a, b) => cmp(a.display, b.display))
    .forEach(record => {
      const option = document.createElement('option');
      option.appendChild(document.createTextNode(record.display));
      option.value = [record.name, record.milieu].join('|');
      list.appendChild(option);
    });

  list.addEventListener('change', event => {
    const s = list.value.split('|'),
          name = s[0],
          milieu = s[1] || undefined;
    Traveller.MapService.sectorData(name, {
      type: 'SecondSurvey', metadata: 0, milieu})
      .then(data => {
        const target = $('#data');
        if (target) target.value = data;
      });

    Traveller.MapService.sectorMetaData(name, {
      accept: 'text/xml', milieu})
      .then(data => {
        const target = $('#metadata');
        if (target) target.value = data;
      });
  });

  $$('textarea.drag-n-drop').forEach(elem => {
    elem.addEventListener('dragover', event => {
      event.stopPropagation();
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
    });
    elem.addEventListener('drop', async event => {
      event.stopPropagation();
      event.preventDefault();
      const s = await blobToString(event.dataTransfer.files[0]);
      elem.value = s;
    });
    elem.placeholder = 'Copy and paste data or drag and drop a file here.';
  });

  async function blobToString(blob) {
    // Try UTF-8 first
    const text = await blob.text();
    if (text.indexOf('\uFFFD') === -1)
      return text;

    // Fall back to Windows-1252
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.readAsText(blob, 'windows-1252');
      reader.onload = event => { resolve(reader.result); };
      reader.onerror = event => { reject(reader.error); };
    });
  }
});

// |data| can be string (payload) or object (key/value form data)
// Returns Promise<string>
async function getTextViaPOST(url, data) {
  let request;
  if (typeof data === 'string') {
    request = fetch(url, {
      method: 'POST',
      headers: {'Content-Type': 'text/plain'},  // Safari doesn't infer this.
      body: data
    });
  } else {
    data = Object(data);
    const fd = new FormData();
    Object.keys(data).forEach(key => {
      const value = data[key];
      if (value !== undefined && value !== null)
        fd.append(key, data[key]);
    });
    request = fetch(url, {
      method: 'POST',
      body: fd
    });
  }

  const response = await request;
  const text = await response.text();
  if (!response.ok)
    throw text;
  return text;
}

async function getJSONViaPOST(url, data) {
  const text = await getTextViaPOST(url, data);
  return JSON.parse(text);
}
