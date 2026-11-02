window.addEventListener('DOMContentLoaded', () => {
  const $ = s => document.querySelector(s);
  const $$ = s => [...document.querySelectorAll(s)];

  const config = $('script[src="toc.js"]');
  const selector = config?.dataset.tocSelector || 'h2,h3';

  const toc = document.createElement('nav');
  toc.className = 'toc';
  $$(selector).forEach(h => {
    const a = document.createElement('a');
    const text = h.textContent;
    if (!h.id) {
      h.id = text.toLowerCase()
                 .replace(/[^a-z0-9 -]/g, '')
                 .trim()
                 .replace(/\s+/g, '-');
    }
    Object.assign(a, {
      href: '#' + h.id,
      className: h.tagName,
      innerText: text,
    });
    toc.append(a);
  });
  const title = document.createElement('h2');
  title.append('Contents');
  toc.insertBefore(title, toc.firstChild);
  document.body.appendChild(toc);
});
