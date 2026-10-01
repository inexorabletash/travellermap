The Traveller Map - Setup Guide
================================

This is the setup guide to source code behind https://travellermap.com - an online resource for fans
of the Traveller role playing game.

This guide assumes basic familiarity with using Visual Studio projects and the Git source control system.

Prerequisites
-------------
* Windows 10 or later
* [Visual Studio Community 2022](https://www.visualstudio.com/downloads/) or the equivalent
* [Git for Windows](https://git-scm.com/download/win) - or the equivalent

Setup and Build
---------------
1. Clone this repository:
    * example: `git clone https://github.com/inexorabletash/travellermap.git`
2. In your clone, copy the included `Web.config.sample` file to `web.config`
    * If you already had a `web.config` from before the PDFsharp 6 upgrade, copy the `<runtime>` section (assembly binding redirects) from `Web.config.sample` into it, or PDF output will fail at runtime.
3. Using Visual Studio, open `Maps.sln`
4. Optionally, modify the `web.config` file in the solution:
    * Add an _admin key_ - this can be used to trigger flushing of the memory cache and rebuilding the search index.
    * Find the `<sessionState>` element and the `stateConnectionString` attribute; change `50103` to your local IISExpress port number. Find this by opening the Maps project's properties and looking for the Web tab, "Servers" subsection, Project URL box (mine says `http://localhost:50103/` for example).
5. Select the Debug or Release target and build the Maps target. NuGet packages (PDFsharp 6 and the .NET Framework 4.8 reference assemblies) are restored automatically; no separate PDFsharp build or .NET 4.8 Developer Pack install is needed.

Command-line build (no IDE), using the MSBuild that ships with Visual Studio:

    msbuild Maps.sln -t:Restore
    msbuild Maps.sln -p:Configuration=Debug

Run it without Visual Studio: `"C:\Program Files\IIS Express\iisexpress.exe" /path:<clone path> /port:50103`

Trying it out
-------------
* You can run it however you like; I use **Ctrl+F5** to start without debugging.
* IIS will start and your default browser will connect to the site.
* The map will display!

To Add a Database
-----------------

Some features such as search require a database.

1. Ensure you have some version of SQL Server installed (Express, Developer, etc). [SQL Server Downloads](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
1. Track down the **connection string** for the database. When installing SQL Server Express Edition, this is given at the end of the install, and looks like: `Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;`
1. Open the Solution > Maps > `Web.config` file and find the `<connectionStrings>` element
1. Paste your copied connection string information from the properties panel into the `connectionString` attribute of both the `SqlDev` and `SqlProd` names
1. Save your `Web.config`

Now that your application can find your empty database, the reindex action on the admin page will fill an empty database:

1. Start the site; I use **Ctrl+F5** to start without debugging.
1. Your browser will open to the default page, `http://localhost:<YOUR_PORT>/index.htm`
1. Edit the URL to load: `http://localhost:<YOUR_PORT>/admin/reindex`

You will see output from the re-indexing operation; when complete the page will show a summary followed by a little Omega symbol (&Omega;) at the bottom of the page.

To verify that the database is populated, you can run a query:

* `http://localhost:<YOUR_PORT>/api/search?q=regina`

> NOTE: When the Debug build target is running, only the worlds in "selected" sectors will be indexed. A Release build will index all worlds.

Linting the client-side javascript:
-----------------

1. Choose Node and ESLint from the Visual Studio Installer option, or Download and install Node https://docs.npmjs.com/cli/v11/configuring-npm/install
2. Run "npm install" in the project root to download the development packages used for linting.
3. Automatically ESLint from Visual Studio or your chosen IDE's ESLint extension, or manually run "npx eslint fileName.js".