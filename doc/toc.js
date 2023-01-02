window.addEventListener('DOMContentLoaded', () => {
  const $ = s => document.querySelector(s);
  const $$ = s => Array.from(document.querySelectorAll(s));

  const config = $('script[src="toc.js"]');
  const selector = (config && config.getAttribute('data-toc-selector')) || 'h2,h3';

  const toc = document.createElement('nav');
  toc.className = 'toc';
  $$(selector).forEach(h => {
    const a = document.createElement('a');
    const text = h.textContent || h.innerText;
    if (!h.id) {
      h.id = text
        .toLowerCase()
        .replace(/[^a-z0-9 -]/g, '')
        .trim()
        .replace(/\s+/g, '-');
    }
    a.href = '#' + h.id;
    a.className = h.tagName;
    a.appendChild(document.createTextNode(text));
    toc.appendChild(a);
  });
  const title = document.createElement('h2');
  title.appendChild(document.createTextNode('Contents'));
  toc.insertBefore(title, toc.firstChild);
  document.body.appendChild(toc);
});
