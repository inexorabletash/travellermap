// version 2

const CACHE_NAME = 'offline-resources';
const urlsToCache = [
  '/', // start_url in manifest, even though this isn't served; see:
  // https://developers.google.com/web/tools/lighthouse/audits/cache-contains-start_url

  'offline.html',
  'favicon.ico',
  'https://fonts.googleapis.com/css?family=Marcellus',
];

self.addEventListener('install', event => {
  event.waitUntil(
    self.caches.open(CACHE_NAME)
      .then(cache => cache.addAll(urlsToCache))
  );
});

self.addEventListener('fetch', event => {
  if (navigator.onLine) return;

  const path = new URL(event.request.url).pathname;
  if (path.endsWith('.html') || path.match(/\/\w*$/)) {
    event.respondWith(
      self.caches.open(CACHE_NAME)
        .then(cache => cache.match('offline.html'))
    );
  } else {
    event.respondWith(
      self.caches.open(CACHE_NAME)
        .then(cache => cache.match(event.request))
    );
  }
});
