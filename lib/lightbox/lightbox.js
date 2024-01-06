window.addEventListener('DOMContentLoaded', () => {
  'use strict';

  const lightbox = document.body.appendChild(document.createElement('div'));
  const inner = lightbox.appendChild(document.createElement('div'));
  lightbox.id = 'lightbox';
  lightbox.tabIndex = 0;

  lightbox.addEventListener('click', event => {
    event.preventDefault();
    lightbox.classList.remove('visible');
  });
  lightbox.addEventListener('keydown', event => {
    event.preventDefault();
    lightbox.classList.remove('visible');
  });

  [].forEach.call(document.querySelectorAll('a.lightbox'), a => {
    a.addEventListener('click', event => {
      event.preventDefault();
      lightbox.classList.add('visible');
      lightbox.focus();
      inner.style.backgroundImage = `url("${a.href}")`;
    });
  });
});
