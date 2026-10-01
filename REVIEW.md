# Code Review — Traveller Map

Reviewed at commit `9e5c90e9` (2026-09-30). Scope: server (C#), client (JS), build/config,
and tests. Data files under `res/Sectors/` were not reviewed for content.

Overall: a mature, well-structured codebase. Route table, content negotiation and the
multi-backend renderer (Bitmap/SVG/PDF) are clean. The issues below are mostly edge cases,
admin/ops paths, and aging dependencies rather than core-logic defects.

Severity: **High** = security or wrong behavior in normal use; **Med** = wrong behavior in
edge cases or ops; **Low** = robustness/cosmetic.

---

## 1. Bugs

| # | Sev | Location | Issue |
|---|-----|----------|-------|
| B1 | High | `server/admin/AdminHandler.cs:23` | **Admin auth bypass if `AdminKey` is unset.** `Request["key"] == AppSettings["AdminKey"]` is `null == null` → `true` when the setting is missing and no `key` is passed (over HTTPS). Also, the sample default `YOUR_KEY_HERE` works as a real key if left in place. Fix: reject when the configured key is null/empty or equals the placeholder; use a constant-time compare. |
| B2 | Med | `server/SectorMap.cs:38,193` + `AdminHandler.cs:128` | **`/admin/flush` only flushes one thread.** `SectorMap` is `[ThreadStatic]` (commented as a "singleton" — it's actually one full copy per worker thread), so `Flush()` clears only the request thread's copy. Other threads keep serving stale sector data until app restart. `/admin/flush` also never clears the `ThreadLocal<ResourceManager>` LRU caches at all. Fix: a global generation counter checked by `GetInstance()` (and by `ResourceManager`) so every thread rebuilds lazily after a flush. |
| B3 | Med | `server/admin/AdminHandler.cs:26` and `:37-45` | Wrong key over HTTPS writes the 403 body **twice** (`SendError` in `AdminAuthorized`, then again in `ProcessRequest`). Remove one. |
| B4 | Med | `index.js` `showWorldData()` (~1170-1267) | **Stale-response race.** Nothing checks that `selectedWorld` is still the same after the `await`s. Clicking world A then B quickly can show A's card last; closing the card while the fetch is in flight re-opens it. Fix: capture a request token/`AbortController` and bail if superseded. |
| B5 | Med | `index.js:860-861` | `JSON.parse` of `localStorage` preferences is unguarded at module top level; a corrupt/foreign value (or `localStorage` throwing in some privacy modes) breaks page startup. Wrap in `try/catch` and fall back to defaults. |
| B6 | Med | `server/api/PosterHandler.cs:39-50,268` | **No size cap on posters.** Arbitrary `x1..x2/y1..y2` with `scale` up to 512 produces an unbounded `tileSize` (can overflow `int` or request multi-GB bitmaps/SVG). `TileHandler` has `MaxDimension`; posters should too (a DoS vector on a public site). |
| B7 | Low | `server/api/TileHandler.cs:40` | `width * height` is `int` math and overflows (e.g. `w=h=65537` passes the check). Cast to `long`. |
| B8 | Low | `server/api/ImageHandlerBase.cs:113` | `Request.UserAgent.Contains("Chrome")` throws `NullReferenceException` on a POST with no User-Agent (surfaces as a 400). Use `?.Contains(...) == true`. |
| B9 | Low | `server/api/PosterHandler.cs:256` | `rotation % 4` is negative for negative input (`-1`), silently meaning "no rotation". Use `((r % 4) + 4) % 4`. |
| B10 | Low | `server/http/Routing.cs:93` | Redirect targets (`/go/...`, `/booklet/...`, `/sheet/...`) substitute route values **unencoded** into the query string; a name containing `&`, `#`, `+` or `%` is mangled. Use `Uri.EscapeDataString`. |
| B11 | Low | `server/api/DataHandlerBase.cs:63` | Release builds map *every* unhandled exception to **400 Bad Request**, hiding server faults (and they're not logged). Keep 400 for `ArgumentException`/`FormatException`/parse errors; return 500 and log otherwise. |
| B12 | Low | `server/api/DataHandlerBase.cs:187,197` | JSONP pre/postamble wrap `Response.OutputStream` in a `using StreamWriter`, whose `Dispose` closes the underlying stream. It evidently works today under `HttpResponseStream`, but it's fragile; write via `Response.Output`/`Response.Write` instead. JSONP also has no test coverage (see §5). |
| B13 | Low | `server/search/SearchEngine.cs` (`LIKE` clauses) | User search text isn't escaped for `LIKE` wildcards (`%`, `_`, `[`). Not SQL injection (parameters are used correctly), but `_` in a query matches any character. |
| B14 | Low | `server/http/Routing.cs:14-18` | `caseInsensitive` parameter is dead — `IgnoreCase` is always set. Either remove the parameter or honor it. |
| B16 | Low | `server/api/PosterHandler.cs:192` | `$"Subsector {'A' + index}"` adds a char and an int, so subsector poster titles and download filenames say "Subsector 67" instead of "Subsector C". Use `(char)('A' + index)`. (Found while verifying the PDFsharp upgrade.) |
| B17 | Low | `world_util.js` `prepareWorld` | For Zhodani worlds with a `KM`/`W` base, `Bases` was a string, so a world that also had an `Re`/`Px`/`Ex`/`Rs*` remark threw on `Bases.push` and its card never opened. Latent: no such world exists in current data (all 679 sectors in 9 milieux checked). |
| B18 | Med | `redir.html` | `href` from the query string was used as a link and for automatic navigation with any URL scheme, including `javascript:`. Exploiting it needs a user click (the link is `target=_blank rel=noopener`) or a previously ticked "skip" for that origin, but only `http:`/`https:` should be accepted. |
| B19 | Low | `index.js` `showSectorData` | `window.open(...).onload` threw when popups were blocked (`window.open` returns null). |
| B20 | Low | `server/SecondSurvey.cs` legacy base tables | First-match GlobMaps had specific entries after matching `*` entries: Solomani/Zhodani `KM` encoded as `F` (should be `K`/`Z`), Droyne `M` as `M` (should be `Q`), and Hiver `Sc.H` decoded as `CK` (should be `H`). The test reference files had captured the wrong output. Fixed in Phase 4. |
| B15 | Low | `sw.js` fetch handler | If `offline.html` isn't in the cache, `respondWith(undefined)` yields a network error. Return `Response.error()` or a minimal inline response. |

## 2. Optimizations

- **O1 — One `SectorMap` per thread (memory).** Every IIS worker thread parses and holds all
  milieux/sectors/metadata (`[ThreadStatic]`). Loading once into an immutable shared instance
  and handling the only mutation (Dotmap fallbacks in `FromLocation`) with a
  `ConcurrentDictionary` would cut memory by roughly the thread count and speed warm-up. Same
  idea for the many `ThreadLocal<IReadOnlyDictionary<...>>` lookup tables in `SecondSurvey.cs`,
  `RenderUtil.cs`, `DataHandlerBase.cs` — they are read-only after construction and can be
  `static readonly`. (`RegexMap`/`ImageCache` need a check that they're thread-safe first.)
- **O2 — Client `LRUCache` (`map.js:550-595`).** `fetch()`/`insert()` use `Array.indexOf` +
  `splice`, O(n) per tile access every frame; capacity grows to `tileCount * 2`. A `Map`
  (delete + re-set for recency, `map.keys().next()` for eviction) makes it O(1) and simpler.
- **O3 — `FindNearestWorldMatch` (`SearchEngine.cs`)** sorts all matches but reads one row; add
  `TOP 1`.
- **O4 — Admin `Reindex`/`Flush`** stream a whole copy of `site.css` into each admin page; link
  it instead.
- **O5 — Handlebars templates** are compiled at runtime from inline `<script>` blocks. Fine at
  this size; precompiling is only worth it if you add a build step for other reasons.

## 3. Simplifications

- **S1 — Duplicate option parsing.** `AdminHandlerBase.GetStringOption/GetBoolOption` duplicate
  `DataResponder`'s (there's a `TODO: Dedupe` noting it) and parse bools differently (`== "1"`
  vs integer `!= 0`). Move to one static helper on `HandlerBase`.
- **S2 — Route table.** `Global.asax.cs` repeats near-identical `/data/{sector}/{X}/{sec|tab|image}`
  groups for quadrant, subsector-index, and subsector-name. A small helper
  (`AddDataRoutes(prefix, defaults)`) would halve the table and remove the inconsistent
  `metadata` default (`0` int in `/api/sec/...` vs `"0"` string in `/data/...`).
- **S3 — `ImageHandlerBase.ProduceResponse`** is ~150 lines mixing option parsing with three
  output backends. Split into `ParseStyleOptions(ctx)` + `RenderSvg/RenderPdf/RenderBitmap`.
  The long list of `GetBoolOption(..., queryDefaults: queryDefaults, ...)` calls could be a
  table of `(param, setter)` pairs.
- **S4 — `PosterHandler` domain table.** The `switch` of named domains is data; move it to a
  static dictionary (or a resource file, per the existing TODO).
- **S5 — Legacy style bits.** `#define LEGACY_STYLES` in `DataHandlerBase.cs` — if the old
  `options` style bits are no longer sent by any client, remove the path.
- **S6 — Stale compat code** in `Global.asax.cs` (`Tls11`), `index.js` (`isIframe` "!= for IE",
  iOS 12 `webkit*` fullscreen shims), `Maps.csproj` (`DefaultTargetSchema IE50`,
  `DefaultClientScript JScript`, etc.). Safe to trim.

## 4. Version updates

| Item | Current | Suggested | Notes |
|------|---------|-----------|-------|
| PDFsharp | 1.5, built from source, DLL referenced by relative path | **PDFsharp 6.x** (NuGet, MIT) | Biggest setup-pain removal: no separate clone/build, no re-adding references (SETUP steps 1-2, 6). 6.x supports .NET Framework 4.6.2+; API is mostly compatible (`XGraphics`, `PdfDocument`). Keep the global PDF lock unless you confirm 6.x's thread-safety for this use. |
| Unit test framework | MSTest v1 (`Microsoft.VisualStudio.QualityTools.UnitTestFramework`, VS2010-era conditions in csproj) | **MSTest 3.x** (`MSTest.TestFramework` + `MSTest.TestAdapter` NuGet) | Enables command-line `vstest`/`dotnet test` and CI. |
| Project format | Legacy non-SDK `.csproj` (ToolsVersion 12) | SDK-style csproj (still targeting `net48`) | Smaller project files, NuGet `PackageReference`; web projects need care (MSBuild.SDK.SystemWeb). |
| C# language | `LangVersion 8.0` | `latest` | Syntax-only features (file-scoped namespaces, pattern improvements) work on .NET Framework. |
| SQL client | `System.Data.SqlClient` | `Microsoft.Data.SqlClient` | The old one is in maintenance only. |
| .NET Framework | 4.8 | 4.8.1 (optional) | Long-term, moving off `System.Web` to ASP.NET Core would allow Linux hosting, but it's a rewrite of the handler/routing layer and `System.Drawing` rendering (→ SkiaSharp/ImageSharp) — only worth it with a strong reason. |
| TLS | `Tls11 \| Tls12` enabled explicitly | Remove the line (4.8 uses OS defaults incl. TLS 1.3) | |
| `Web.config.sample` | `targetFramework="4.6.1"` | `4.8`, and add `<httpRuntime targetFramework="4.8">` | Mismatch with the csproj. |
| Handlebars | 4.7.8 from cdnjs, no SRI | same version + `integrity`/`crossorigin` attributes | 4.7.8 is the current 4.x. |
| `@types/handlebars` | ^4.0.40 | remove | Handlebars ships its own types; this package is a deprecated stub. |
| ESLint / globals | 10.x / 17.x | current | Up to date. |
| Docs | README said IIS8 / .NET 4.6.1 | — | Fixed in this change. |

## 5. Possibly missing tests

Current unit tests cover only `Util`, JSON, and column/MSEC serialization (19 test methods).
The browser integration tests are valuable but need a running site and manual inspection.

High value:
1. **Data validation in CI.** Most commits are data edits. A test (or console tool) that loads
   `milieu.tab` → every sector file and metadata XML, validates XML against `res/sectors.xsd`,
   and fails on parse errors/unknown allegiance or sophont codes — i.e. `/admin/errors` +
   `/admin/codes` headless — would catch broken borders/allegiances before merge.
2. **`SectorParser` / T5 column format** round-trips (parse → write → parse), including
   malformed rows, wide/narrow columns, and legacy SEC.
3. **`SecondSurvey`** allegiance/sophont decoding, legacy → T5 mapping, remark parsing.
4. **`Astrometrics`** coordinate conversions (sector/hex ↔ world x,y), including negative sectors
   and the Reference hex, plus a parity test against `map.js`'s `Astrometrics`.
5. **Routing table**: each URL in the API docs resolves to the intended handler with the
   intended defaults (guards against ordering regressions like `/data/X/sec` vs subsector name).
6. **Admin auth** (`AdminAuthorized`) — missing key, empty config, wrong key, non-HTTPS. Would
   have caught B1.
7. **`PathFinder`** / `/api/route`: known routes, unreachable destinations, jump limits.

Medium value:
8. `SearchEngine.ParseQuery` — operators (`uwp:`, `alleg:`, `in:` …), quoting, sector+hex form.
9. `HighlightWorldPattern.Parse`, `DataResponder.AcceptTypes` ordering, JSONP wrapping
   (currently zero JSONP tests anywhere).
10. Image handler limits: oversized/negative `w`/`h`, poster bounds, `dpr` clamping.
11. **JS unit tests** (no JS tests exist): `world_util.js` UWP/remark decoding, `Util.makeURL`,
    `LRUCache`, and `map.js` coordinate math. These are pure functions — Node's built-in
    `node --test` runner needs no new dependencies.
12. **CI**: there's no `.github/workflows`. Even a JS-only workflow (`npm ci && npx eslint .`
    + `node --test`) plus an XML-schema check of `res/Sectors/**/*.xml` would run on Linux
    runners; the C# build/tests need a `windows-latest` runner.
