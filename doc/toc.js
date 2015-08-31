window.addEventListener('DOMContentLoaded', function() {
  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return document.querySelectorAll(s); };

  var toc = document.createElement('nav');
  toc.className = 'toc';
  [].slice.call($$('h2,h3')).forEach(function(h) {
    var a = document.createElement('a');
    var text = h.textContent || h.innerText;
    if (!h.id)
      h.id = text.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z-]/g, '');;
    a.href = '#' + h.id;
    a.className = h.tagName;
    a.appendChild(document.createTextNode(text));
    toc.appendChild(a);
  });
  var title = document.createElement('h2');
  title.appendChild(document.createTextNode('Contents'));
  toc.insertBefore(title, toc.firstChild);
  document.body.appendChild(toc);
});
