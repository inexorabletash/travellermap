# Improvement Plan

Follows from `REVIEW.md` (item IDs like B1/O2/S3 refer to it).

## Decisions (2026-09-30)
- **PDFsharp**: go straight to 6.x. **Done** (Phase 0).
- **Upstream**: decide per change. Small bug fixes and clear, simple enhancements are
  candidates for PRs to `inexorabletash/travellermap`. Work toward **moving off Microsoft
  infrastructure** (IIS, System.Web, SQL Server, System.Drawing) stays in this fork.

Tag legend: **[up]** = upstream PR candidate, **[fork]** = fork only.

### What "moving off Microsoft" changes
- Write fixes as **plain functions with no `System.Web` dependency** where practical (admin key
  check, size limits, option parsing, and so on) so the logic and its tests carry over to a
  future host unchanged.
- **Dropped:** the `System.Data.SqlClient` → `Microsoft.Data.SqlClient` upgrade. Search storage
  will probably change anyway, so time goes into putting search behind an interface instead.
- **Kept:** MSTest 3.x. It runs on modern .NET on Linux, so the tests migrate too.
- PDFsharp uses the **GDI** build for now, because rendering is built on System.Drawing
  (`XFont(Font)`, `XImage.FromGdiPlusImage`). The cross-platform build only fits once
  System.Drawing is replaced (Phase 7).

---

## Phase 0 — Baseline — DONE
- PDFsharp-GDI 1.5 (hand-built DLL) → **6.2.4 via NuGet** in `Maps.csproj` and `UnitTests.csproj`.
- Added `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3 (build-only), so the .NET 4.8
  Developer Pack no longer has to be installed. This also makes a CI build possible.
- `Web.config.sample`: binding redirects for PDFsharp's dependencies; `targetFramework` 4.6.1 → 4.8.
- Verified under IIS Express: PDF, PNG and SVG posters; a PDF jump map in the candy style
  (embedded images, alpha, hex clipping); 8 PDF requests at once. The PDF output was checked
  visually against the PNG.
- Unit tests: 17/19 pass. Both failures predate this work (see Phase 4, item 1).
- `SETUP.md`, `README.md` and `CLAUDE.md` updated. **[up]** candidate once it has run a while.

## Phase 1 — Security and safety fixes [up] — DONE (`bce47ea1`, branch `phase1-fixes`)
Done: B1, B3, B6, B7, B8, B9, B10, B11 (narrowed: 500 only for exception types that always
mean a server fault, because the code also uses `ApplicationException`/`Exception` for input
errors), B16. Unit tests: `HandlerTest.cs`. Checked end to end under IIS Express, except
B1, which can't be exercised from localhost (local requests are always allowed); the unit
test covers it. Bitmap cap: 2^27 pixels; vector cap: 2^20 per side.
Upstream note: cherry-picking onto upstream `main` will conflict in `UnitTests.csproj`
(the PDFsharp reference lines differ). The fix is to keep upstream's reference and add
`HandlerTest.cs` + `System.Web`.

Original scope:
- B1 admin key bypass. Put the check in a pure function: reject a missing or empty key and the
  placeholder, compare in constant time, and unit-test it.
- B3 duplicate 403 body.
- B6/B7: one shared image-size check (64-bit math) used by tiles and posters.
- B8 missing User-Agent, B9 negative rotation, B10 unencoded redirect values.
- **B16 (new)**: `PosterHandler.cs:192` — `'A' + index` gives a number, so titles and filenames
  read "Subsector 67" instead of "Subsector C". Fix: `(char)('A' + index)`.
- B11: return 400 only for input errors, 500 plus logging otherwise.
- Check: new unit tests; `curl` against the admin page, oversized posters, and subsector poster titles.

## Phase 2 — Browser fixes and JavaScript tests [up] — DONE (branch `phase2-client`)
Done: B4, B5, B15, O2, plus B17 (Zhodani `Bases`, latent), B18 (`redir.html` accepted any URL
scheme), B19 (blocked popup). `npm test` runs 13 tests in `test/unit/`. The script cache-busters
are bumped on every page, because `world_util.js` now depends on new `Util` methods.
Checked in headless Chrome against the running site: corrupt storage, rapid world selection,
closing the card mid-load, and `redir.html`. All 4 bugs reproduce on the old code and are
fixed on the new code. The check script is not committed; it could become a CI browser test
in Phase 4.

Original scope:
- Add `"test": "node --test"` and `test/unit/`; export `LRUCache`, `Util.makeURL`, and the
  `world_util` decoders.
- B4 stale world card (request token), B5 guarded preferences, B15 service-worker fallback.
- O2: rebuild `LRUCache` on `Map`, with tests.
- Bump the `index.js?update=` cache-buster.

## Phase 3 — Server cache correctness — B2 DONE (branch `phase3-flush`) [up]
New `ThreadLocalCache<T>` plus a global `CacheGeneration` counter. `/admin/flush` now reloads
`SectorMap`, `ResourceManager`, the T5SS allegiance/sophont tables, and the default stylesheet
on every thread. The code tables previously needed an app restart. `SectorMap.Flush()` keeps
its thread-local meaning for the admin report pages.
Checked under IIS Express with 64 concurrent clients after editing a sector's metadata. On the
old code, 1 of 300 requests saw the edit after a flush; on the new code, 300 of 300. The
allegiance table edit reached 300 of 300 after a flush. `/admin/errors` and `/admin/codes`
still work. Unit tests: `CacheTest.cs` (4 tests).
O1 (one shared map instead of one per thread) is still deferred, per below.

Original scope:
- B2: a generation counter makes `/admin/flush` reach every thread's `SectorMap` and
  `ResourceManager`. **[up]**
- O1: one shared, immutable `SectorMap`. **[fork]**, and only if memory matters. It's
  more naturally done during the Phase 7 host migration.

## Phase 4 — Tests and CI (≈2–3 days)
1. Fix the two old test failures: update the `ColumnParserTest` input; make
   `Sector`'s default stylesheet path injectable so `MSECWriterTest` runs outside IIS. **[up]**
2. Data validation tool: load every milieu, sector and metadata file, check the XML against
   `sectors.xsd`, and report unknown codes. Build it as a console entry point with no
   `System.Web`. **[up]**
3. Unit tests: SectorParser round-trips, SecondSurvey codes, Astrometrics (plus agreement with
   `map.js`), the route table, PathFinder, search query parsing, JSONP.
4. GitHub Actions: a Linux job (eslint, `npm test`, XML schema check) and a Windows job
   (msbuild restore/build, vstest). The Windows job now works because the reference
   assemblies come from NuGet. **[fork]** first; offer upstream if wanted.

## Phase 5 — Remaining version updates
- MSTest v1 → MSTest 3.x NuGet (drop the VS2010-era `Choose` blocks). **[up]**
- Remove the TLS 1.1 line, add SRI to the Handlebars tag, drop `@types/handlebars`, set
  `LangVersion` to latest. **[up]**
- ~~Microsoft.Data.SqlClient~~: dropped (see Decisions).

## Phase 6 — Simplifications (after the tests exist) [up, case by case]
- S1 one option parser, S2 route-table helper, S3 split `ProduceResponse`, S4 domain table,
  S5 legacy style bits, S6 dead compatibility code; B13 escaping `LIKE` wildcards, B14, O3.
- S2/S3 conflict most with upstream code changes. Skip them if Phase 7 goes ahead, since the
  migration rewrites those files anyway.

## Phase 7 — Moving off Microsoft infrastructure [fork] (to be scoped separately)
Rough order, each step keeping the site working:
1. **Rendering**: System.Drawing → SkiaSharp behind the existing `AbstractGraphics`. The SVG
   backend is already independent. PDF → PDFsharp's cross-platform build (needs a font
   resolver) or Skia's PDF backend.
2. **Search**: an interface over `SearchEngine`; SQLite (simplest, file-based) or PostgreSQL.
3. **Host**: System.Web handlers → ASP.NET Core minimal APIs on .NET 8/10 LTS, running on
   Kestrel in a Linux container. The regex route table and `DataResponder` map over fairly
   directly. Still .NET, but no Windows, IIS or SQL Server.
4. **Config**: `web.config` → `appsettings.json` and environment variables.
Guard rail: compare the `test/refs` reference images and PDFs before and after each step.
