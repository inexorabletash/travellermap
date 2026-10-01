# CLAUDE.md

Guidance for Claude Code (and other contributors) working in this repository.

## What this is

Source for https://travellermap.com — a zoomable 2D map of the *Traveller* RPG universe.
Hierarchy: **world** (one hex) → **subsector** (8 wide × 10 tall hexes, lettered A–P) →
**sector** (4×4 subsectors = 32×40 hexes) → **milieu** (a whole map at one point in the
timeline, e.g. `M1105`, the default). Sectors carry metadata: allegiances/polities, borders,
routes, labels, credits and per-world remarks.

This repo is a fork of `inexorabletash/travellermap` (`origin` = `DavidLDawes/travellermap`).
Most upstream commits are **data** changes under `res/Sectors/`, not code.

## Architecture

**Server — ASP.NET (System.Web), .NET Framework 4.8, C# 8, Windows/IIS only.**
- `Global.asax.cs` — registers every URL route (regex-based, see `server/http/Routing.cs`).
  Route order matters: more specific patterns (e.g. `/data/{sector}/sec`) must be registered
  before catch-alls (e.g. `/data/{sector}/{subsector}`).
- `server/api/*Handler.cs` — one handler per API. Data handlers derive from `DataHandlerBase`
  (content negotiation: `accept=` query param → `Accept` header → route default → handler
  default; JSON/XML/text; JSONP via `jsonp=`). Image handlers derive from `ImageHandlerBase`
  (PNG via System.Drawing, SVG, or PDF via PDFsharp-GDI 6.x, serialized behind a lock).
- `server/admin/*` — admin pages (`/admin/flush`, `/admin/reindex`, `/admin/errors`, …).
  Allowed from localhost, or over HTTPS with `?key=` matching `AdminKey` in `web.config`.
- `server/SectorMap.cs` — loads `res/Sectors/milieu.tab` → per-milieu XML sector lists →
  per-sector metadata XML; resolves sectors by name/abbreviation/location.
- `server/serialization/` — parsers/writers for sector data: T5 Second Survey column format
  (`.tab`/`.sec`), legacy SEC, MSEC metadata, XML metadata.
- `server/RenderContext.cs`, `Stylesheet.cs`, `RenderUtil.cs`, `server/graphics/` — map rendering.
  `AbstractGraphics` has Bitmap/SVG/PdfSharp backends; keep all three working.
- `server/search/SearchEngine.cs` — SQL Server search index (built by `/admin/reindex`).
- Caches are **thread-affine**: one copy per worker thread, so they need no locking. Don't
  convert them to plain statics without adding locking. Anything loaded from a data file
  (`SectorMap`, `ResourceManager`, the T5SS allegiance/sophont tables, the default sector
  stylesheet) uses `ThreadLocalCache<T>` (`server/utilities/ThreadLocalCache.cs`), so
  `/admin/flush` → `CacheGeneration.InvalidateAll()` reloads it on every thread. Use it for
  any new file-backed cache; plain `ThreadLocal<T>` is fine for constant tables.
  `SectorMap.Flush()` resets only the current thread (admin reports use it to release memory).

**Client — plain ES modules, no build step.**
- `index.html` + `index.js` — main page UI (search, routes, world/sector info cards, settings).
- `map.js` — the `TravellerMap` tiled map widget, `MapService` API client, `Util`, LRU cache.
- `world_util.js` — decodes UWP/extensions/remarks into human-readable world details.
- Templates are Handlebars (loaded from cdnjs), inlined in `<script type="text/x-handlebars-template">`.
- `sw.js` — service worker providing an offline fallback page.
- `make/` (posters, booklets, atlases, border/route makers), `print/` (world sheets),
  `borders/` (border generation), `doc/` (API/file-format docs), `tools/` (ad-hoc tools).

**Data — `res/`.**
- `res/Sectors/milieu.tab` lists the per-milieu index XML files (e.g. `M1105/M1105.xml`).
- Each sector = data file (`*.tab`, T5 column format) + metadata XML (borders, routes,
  allegiances, subsector names, credits). Schema: `res/sectors.xsd`.
- `res/t5ss/` — T5SS allegiance/sophont code tables (served at `/t5ss/*`).

## Building & running

Visual Studio 2022 (or its MSBuild) on Windows is required (see `SETUP.md` for full steps).
1. Copy `Web.config.sample` → `web.config` (git-ignored). Set `AdminKey`; connection strings
   are only needed for search. The `<runtime>` binding redirects are required for PDF output.
2. Build: `msbuild Maps.sln -t:Restore` then `msbuild Maps.sln -p:Configuration=Debug`
   (MSBuild lives at `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`).
   NuGet supplies PDFsharp-GDI 6.x and the net48 reference assemblies.
3. Run: `"C:\Program Files\IIS Express\iisexpress.exe" /path:<repo> /port:50103`, or Ctrl+F5 in VS.
   Smoke test: `/api/poster?sector=Spinward%20Marches&subsector=C&accept=application/pdf`.
4. Optional: SQL Server + `/admin/reindex` to populate search. Debug builds index only
   "selected" sectors.

If PDFsharp's dependency versions change, the build prints MSB3247 with the binding
redirects to copy into `Web.config.sample`.

`TreatWarningsAsErrors` is on for both configurations — new warnings break the build.

## Tests & linting

- **Unit tests** (C#, MSTest): `unittests/UnitTests` — run from VS Test Explorer or
  `vstest.console.exe unittests\UnitTests\bin\Debug\UnitTests.dll`
  (under `Common7\IDE\Extensions\TestPlatform\`). They cover utilities, JSON, and column/MSEC
  serialization only. Known pre-existing failures: `ColumnParserTest` (stale test input) and
  `MSECWriterTest` (needs `HostingEnvironment`, which doesn't exist outside IIS).
- **JS unit tests**: `npm test` (Node's built-in `node --test`, no extra dependencies).
  Tests live in `test/unit/*.test.js`. Import `./setup.js` first; it stubs `window`,
  `location`, `localStorage`, and the `fetch` calls that `world_util.js` makes at import time,
  so `map.js` and `world_util.js` load unchanged in Node.
- **Integration tests** (browser): with the site running, open `test/APITest.html`,
  `test/ContentTest.html`, `test/ImageTest.html`. Reference data/images live in `test/refs/`.
- **Data validation**: `/admin/errors` (parse errors across all sectors), `/admin/codes`
  (unknown allegiance/sophont codes), `tools/lintsec.html`.
- **JS lint**: `npm install` then `npm run lint` or `npx eslint <file>.js` (flat config in `eslint.config.js`).
  Type checking via `jsconfig.json` (`checkJs`) in editors that support it.

There is no CI; run the relevant checks manually before committing.

## Conventions

- C#: `#nullable enable` at the top of files, Allman braces, 4-space indent, `Maps.*` namespaces.
- JS: 2-space indent, `const`/`let`, ES modules, clang-format style (`.clang-format`).
- Cache-busting: pages load scripts as `x.js?update=<timestamp>`, and pages that import
  `map.js`/`world_util.js` pin them in an `<script type="importmap">`. When changing a shared
  module's API, bump its timestamp on **every** page that maps it, or a cached old `map.js`
  can be paired with a new `world_util.js`.
- Browser storage: use `Util.storageGet`/`storageSet`/`storageGetJSON` (they tolerate
  disabled storage and corrupt values), not `localStorage` directly.
- Coordinates: hex `XXYY` is 1-based within a sector (`0101`–`3240`). World-space ("x,y")
  coordinates are relative to Reference (Core 0140); see `server/Astrometrics.cs` and
  `Astrometrics` in `map.js` — keep the two in sync.
- Data edits: keep sector `.tab` columns aligned, use T5SS allegiance codes, and check
  `/admin/errors` afterwards. Border/route changes go in the sector metadata XML.
- Don't commit `web.config`, `bin/`, `obj/`, or `node_modules/`.
