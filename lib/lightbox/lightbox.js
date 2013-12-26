window.addEventListener('DOMContentLoaded', function() {
  'use strict';

  var lightbox = document.body.appendChild(document.createElement('div'));
  var inner = lightbox.appendChild(document.createElement('div'));
  lightbox.id = 'lightbox';
  lightbox.tabIndex = 0;

  lightbox.addEventListener('click', function(e) {
    e.preventDefault();
    lightbox.classList.remove('visible');
  });
  lightbox.addEventListener('keydown', function(e) {
    e.preventDefault();
    lightbox.classList.remove('visible');
  });

  [].forEach.call(document.querySelectorAll('a.lightbox'), function(a) {
    a.addEventListener('click', function (e) {
      e.preventDefault();
      lightbox.classList.add('visible');
      lightbox.focus();
      inner.style.backgroundImage = 'url("' + a.href + '")';
    });
  });
});
