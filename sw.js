// version 5

const CACHE_NAME = 'offline-resources';
const urlsToCache = [
  '/',  // start_url in manifest, even though this isn't served; see:
  // https://developers.google.com/web/tools/lighthouse/audits/cache-contains-start_url

  'offline.html',
  'favicon.svg',
  'https://fonts.googleapis.com/css?family=Marcellus',
];

self.addEventListener('install', /** @type {ExtendableEvent} */ event => {
  event.waitUntil((async () => {
    const cache = await self.caches.open(CACHE_NAME);
    await cache.addAll(
        urlsToCache.map(url => new Request(url, {cache: 'reload'})));
  })());
  self.skipWaiting();
});

self.addEventListener('activate', /** @type {ExtendableEvent} */ event => {
  event.waitUntil((async () => {
    if ('navigationPreload' in self.registration) {
      await self.registration.navigationPreload.enable();
    }
  })());
  self.clients.claim();
});

self.addEventListener('fetch', /** @type {FetchEvent} */ event => {
  if (event.request.mode !== 'navigate')
    return;

  event.respondWith((async () => {
    try {
      const preloadResponse = await event.preloadResponse;
      if (preloadResponse)
        return preloadResponse;
      const networkResponse = await fetch(event.request);
      return networkResponse;
    } catch (error) {
      const cache = await self.caches.open(CACHE_NAME);
      const cachedResponse = await cache.match('offline.html');
      // If the offline page was never cached, respond with something rather
      // than undefined (which surfaces as a generic network error).
      return cachedResponse ??
          new Response(
              '<!DOCTYPE html><title>Offline</title>' +
                  '<p>The Traveller Map is not available offline.',
              {status: 503, headers: {'Content-Type': 'text/html'}});
    }
  })());
});
