// version 3

const CACHE_NAME = 'offline-resources';
const urlsToCache = [
  '/', // start_url in manifest, even though this isn't served; see:
  // https://developers.google.com/web/tools/lighthouse/audits/cache-contains-start_url

  'offline.html',
  'favicon.svg',
  'https://fonts.googleapis.com/css?family=Marcellus',
];

self.addEventListener('install', event => {
  event.waitUntil(
    (async () => {
      const cache = await self.caches.open(CACHE_NAME);
      await cache.addAll(
        urlsToCache.map(url => new Request(url, {cache:'reload'}))
      );
    })()
  );
  self.skipWaiting();
});

self.addEventListener('activate', event => {
 event.waitUntil(
    (async () => {
      if ("navigationPreload" in self.registration) {
        await self.registration.navigationPreload.enable();
      }
    })()
  );
  self.clients.claim();
});

self.addEventListener('fetch', event => {
  if (event.request.mode !== 'navigate')
    return;

  event.respondWith(
    (async () => {
      try {
          const preloadResponse = await event.preloadResponse;
          if (preloadResponse)
            return preloadResponse;
          const networkResponse = await fetch(event.request);
          return networkResponse;
        } catch (error) {
          const cache = await self.caches.open(CACHE_NAME);
          const cachedResponse = await cache.match('offline.html');
          return cachedResponse;
        }
    })()
  );
});
