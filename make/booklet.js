import * as Traveller from '../map.js';

import {} from './post.js';

const Util = Traveller.Util;
const $ = Util.$;
const $$ = Util.$$;

window.addEventListener('DOMContentLoaded', async () => {
  const searchParams = new URLSearchParams(document.location.search);
  if (searchParams.has('sector')) {
    // Redirect for old URLs
    const url = new URL(document.location);
    url.pathname += '/../../print/booklet';
    document.location = url;
    return;
  }

  $('#compose').addEventListener('click', async event => {
    event.preventDefault();
    const form = $('#form');
    if (!form['data'].value.length) {
      alert('Sector data must be specified.');
      return;
    }

    localStorage.setItem('booklet_data', form['data'].value);
    localStorage.setItem('booklet_metadata', form['metadata'].value);

    const url = new URL(document.location);
    url.pathname += '/../../print/booklet';
    url.searchParams.append('style', form['map-style'].value);
    url.searchParams.append('datastyle', form['data-style'].value);
    window.open(url, '_blank', 'noopener');
    return;
  });
});

