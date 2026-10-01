The Traveller Map - Source Code
================================

This is the source code behind https://travellermap.com - an online resource for fans
of the Traveller role playing game.

The Traveller game in all forms is owned by Mongoose Publishing. Copyright 1977 - 2024 Mongoose Publishing. [Fair Use Policy](https://cdn.shopify.com/s/files/1/0609/6139/0839/files/Traveller_Fair_Use_Policy_2024.pdf?v=1725357857)

See LICENSE.md for software licensing details.


Useful Links
------------

* The site itself: https://travellermap.com
* How the site works: https://travellermap.com/doc/about
* API documentation: https://travellermap.com/doc/api
* Credits for the data: https://travellermap.com/doc/credits
* Blog: https://travellermap.blogspot.com
* GitHub repo (upstream): https://github.com/inexorabletash/travellermap
* Issue tracker: https://github.com/inexorabletash/travellermap/issues


What's here
-----------

The map is organized as worlds (one hex each) grouped into subsectors (8×10 hexes),
sectors (4×4 subsectors, 32×40 hexes), and milieux (the whole map at a point in the
timeline; `M1105` is the default).

| Path | Contents |
| --- | --- |
| `Global.asax.cs`, `server/` | ASP.NET server: URL routing, HTTP APIs, sector data parsing, map rendering (PNG/SVG/PDF), search |
| `index.html`, `index.js`, `map.js`, `world_util.js` | Main map client (plain ES modules, no build step) |
| `res/` | Sector data and metadata by milieu, T5SS code tables, images, stylesheets |
| `make/`, `print/`, `borders/` | Poster/booklet/atlas makers, printable world sheets, border tools |
| `doc/` | Site and API documentation (served at `/doc/...`) |
| `test/` | Browser-run integration tests (API, content, image) against a running site |
| `unittests/` | C# MSTest unit tests |
| `tools/` | Ad-hoc data and developer tools |

Getting started
---------------

See [SETUP.md](SETUP.md) for building and running locally, and [CLAUDE.md](CLAUDE.md) for an
architecture overview, conventions, and how to run the tests and data checks.


Dependencies
------------

* The site is built using ASP.NET (System.Web) on .NET Framework 4.8 and runs under IIS / IIS Express on Windows.
* PDF rendering is done using PDFsharp 6 http://www.pdfsharp.net/ (MIT License), via NuGet
* HTML templating uses Handlebars.js https://handlebarsjs.com/ (MIT License)
* Search requires SQL Server (Express/Developer is fine); the rest of the site works without it.
* JavaScript linting uses ESLint via Node/npm (development only).
