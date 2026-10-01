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

## Phase 4 — Tests and CI — DONE (branch `phase4-tests`)
- Fixed both long-failing tests. They needed `Util.MapPath` and `Util.ContentRoot`, so data
  files load outside IIS; `MSECWriterTest`'s expectation had been stale since a 2017 color
  change.
- `DataValidator` and `DataValidationTest`, with a baseline ratchet of 725 known errors.
  `sectors.xsd` now allows metadata attributes on `<Sector>`, which removed about 1,400
  false schema errors.
- New unit tests: SecondSurvey (found and fixed B20), Astrometrics (fixture shared with JS),
  PathFinder, and the route table. 45 C# tests and 14 JS tests.
- `npm run test:browser` runs the three browser suites headlessly. Added JSONP tests and
  refreshed stale references (legend legacy codes after B20; Regina's world count; the
  overview image).
- GitHub Actions CI (Linux JS job, Windows build/test/data job). It has not run on GitHub yet.
- Search query parsing tests were not done: `ParseQuery` is private and tied to SQL. That
  fits better with Phase 7's search interface.

Original scope:
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

## Phase 4b — Data quality backlog (revisit errors and warnings)
The Phase 4 ratchet stops *new* errors; this item works down the existing ones. Analysis from
2026-10-01 (full report: run `DataValidationTest` with `TM_VALIDATION_REPORT=<path>`).

**Where the 187,263 warnings and 725 errors come from**
- 112k warnings (60%) are in the **Zhodani Core Route** fan project (tag `ZCR`, 2005, legacy
  `.sec`). `/admin/errors` deliberately skips non-curated tags; the Phase 4 validator didn't.
- The **T5SS-generated official sectors** (files headed "Generated file - DO NOT MODIFY") have
  only **1,110** warnings. Their source is `res/t5ss/data`, not the generated files.
- 64k warnings are in **hand-maintained official sectors** (e.g. Koog, Rfigh, Hadji, Harbinger;
  HIWG-era data). These use **pre-T5 trade-code conventions**: population-0 outposts coded
  `Ba Lo Ni`, and Ni on population 1–3. Classic Traveller defined Lo as ≤3 and Ni as ≤6; T5
  (which the checker implements) defines them as 1–3 and 4–6. `Extraneous code: Ni/Lo` alone
  is 73k warnings.
- 74% of all warnings (137,776) are trade codes, which are fully determined by the UWP.

**Proposals, with measured effect** (cumulative; warnings / errors)

| # | Change | Owner | After |
|---|---|---|---|
| — | Today | | 187,263 / 725 |
| T1 | Validator warnings use the same scope as `/admin/errors` (OTU/Apocryphal/Faraway); errors still checked everywhere | us [up] | 74,931 / 725 |
| T2 | Demote generation-rule checks (TL = mods+1D, Gov/Law = Flux) to Hint: they test whether a world *could be randomly generated*, and canon worlds deviate on purpose. Still visible on `/admin/errors` as hints | us [up] | 60,057 / 725 |
| T3 | `World.Validate` crashes on placeholder `{Ix}`/`(Ex)` (`----`), reported as 40 "Parse Error"s in Nadir. Data is fine (production shows the worlds); treat dashes as absent | us [up] | 60,057 / 685 |
| T4 | `sectors.xsd`: `Label/@Color` is optional (the server defaults it to amber) | us [up] | 60,057 / 664 |
| D1 | Tool that recomputes T5 trade codes from the UWP for a sector file, producing a reviewable diff; apply per sector with maintainer agreement (changes published data conventions) | tool: us; data: maintainers | 18,906 / 664 |
| D2 | Same tool: population-0 Ex efficiency −5 / infrastructure rules (mechanical) | as D1 | 9,875 / 664 |
| D3 | Rim Worlds (Faraway): 262 worlds use lowercase `na`; almost certainly `Na` (Non-aligned). Codes are case-sensitive, and real codes differ by case (`Cs`/`CS`), so fix the data, not the lookup | sector author (active upstream contributor) | 9,875 / 402 |

**Smaller data fixes worth doing** (each confirmed by reading the file)
- *Visible on the map:* `M1201/Spinward Marches.xml:219` uses `label=` instead of `Label=`, so the
  "Federation of Arden" border label never renders. `M1105/Kidunal.xml:44,48` uses
  `Wraplabel=` instead of `WrapLabel=`. `M1105/Astron.xml:42-44` puts `WrapLabel` on `<Label>`
  (should be `Wrap`; value is false, so there's no visible effect).
- Vanguard Reaches: zero-length route `2340 → 2340`; delete it.
- Undefined border allegiances (8): `Ec` (Kruse), `Tangle` (Dhuerorrg ×2), `Ds` (Ziafrplians),
  `Dw` and `MF` (The Beyond), `Au` and `OC` (Alte Grenzen). Add `<Allegiance>` definitions
  (names needed from the source material).
- Remaining undefined world allegiance codes (~330, after `na`): `Cc` in Gvurrdon M1248 (48;
  defined for Faraway sectors but not here), `Ne`, `Dw`, `Hf`, `Ms`, `Mr`, … Review per sector.
- 38 schema errors are stray text inside `<Routes>`/`<Borders>`/`<Sector>` (22 in `Rzakki.xml`),
  probably notes that should be XML comments.
- `Tabs`, `Era`, `Source-Milieu` attributes aren't read by the server; remove them, or declare
  them as ignored in the schema.

**Suggested order:** T1–T4 first (small code changes, no data judgement, upstream-friendly), then
the visible-on-map fixes, then D1/D2 as an opt-in tool, then D3 and the allegiance definitions
with their authors. Regenerate the baseline after each step to lock in the gains.

## Phase 5 — Remaining version updates — DONE (branch `phase5-updates`) [up]
- MSTest v1 → **MSTest 4.4.1** NuGet (supports net462+ and modern .NET). Its analyzers found 7 swapped
  expected/actual assertions and 1 always-true assertion; fixed.
- SRI hash on all 7 Handlebars script tags (verified a wrong hash blocks the script).
- Removed the TLS line (no outgoing calls). If outgoing calls are added, set
  `<httpRuntime targetFramework="4.8">` so they use OS TLS defaults.
- C# 8.0 → **12.0** pinned in both projects (no new warnings).
- `@types/handlebars`: **kept**. The review was wrong: it provides the real types for the
  CDN-loaded global.
- ~~Microsoft.Data.SqlClient~~: dropped (see Decisions).
- **CI image tests** (follow-up from Phase 4): the CI artifact showed two causes.
  1. About half the "failures" were images that didn't load in time (byte-identical on
     re-fetch). Fixed by limiting concurrency and reporting load errors.
  2. The rest are **ClearType** sub-pixel fringes on text (14–802 px, ≤0.035% of an image).
     `ImageHandlerBase` renders text with `TextRenderingHint.ClearTypeGridFit`, whose fringe
     colors vary by machine. A pixel tolerance would also hide real regressions such as a
     missing label (similar size).

  **Decision for you:**
  - (a) keep ImageTest informational in CI (the status quo);
  - (b) keep a second, CI-specific set of references; or
  - (c) render PNG text with grayscale anti-aliasing (`AntiAliasGridFit`). ClearType is designed
    for one LCD's sub-pixel layout, so it's arguably wrong in a downloadable image. (c) changes
    every rendered image slightly and needs all references regenerated; it's an upstream
    product call.

## Phase 6 — Simplifications — DONE (branch `phase6-simplify`, built on `phase5-updates`) [up, case by case]
Each refactor was checked for unchanged behavior with more than the unit tests:
- **S1** One option parser in `HandlerBase`, shared by the APIs and admin pages
  (`OptionParsingTest`). Edge-case changes: admin `=2` is now true, and API booleans accept bare
  flags (`?nogrid`).
- **S2** Route helper for the quadrant/subsector groups. A full route-table dump (67 routes:
  pattern, handler, defaults, order) is identical before and after.
- **S3** `ProduceResponse` split into style options, DPR, SVG/PDF/bitmap writers, and the data
  URI. 14 output variants are byte-identical before and after (PDFs equal apart from the
  per-request XMP timestamps/UUIDs and font-subset tags, which differ between any two requests).
- **S4** Poster domains as a table; domain posters are byte-identical (`PosterDomainsTest`).
- **B14** dead route parameter; **O3** `TOP 1`.

Not done, with reasons:
- **S5 (legacy style bits): keep.** `doc/api.html` promises old URLs using the deprecated
  `options` style flags keep working.
- **S6 (compatibility code): skip.** The `webkit` fullscreen fallbacks still serve iPads before
  iPadOS 16.4. The legacy `.csproj` IDE properties are ignored by MSBuild and would go away in a
  Phase 7 project conversion.
- **B13 (LIKE wildcards): not a bug.** Wildcards are a documented search feature (`*` → `%` in
  `SearchHandler`), so escaping them would break search.

Notes:
- Since Phase 1's bitmap cap, the large undocumented `domain` posters (e.g. `chartedspace`, 16×8
  sectors) need an explicit smaller `scale`. Before Phase 1 they tried to allocate ~580M-pixel
  bitmaps.
- **ImageTest also drifts locally:** during Phase 6, 11 text-heavy references started failing
  on this dev machine *with code that had passed earlier the same day*. The cause was confirmed
  by building the earlier commit, which renders the same new output. It's the same ClearType
  machine-dependence as CI (Phase 5 note). That makes the ClearType decision more pressing:
  refreshing the references would only fix them for one machine, temporarily.

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
