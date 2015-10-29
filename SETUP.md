The Traveller Map - Setup Guide
================================

This is the setup guide to source code behind https://travellermap.com - an online resource for fans
of the Traveller role playing game.

This guide assumes basic familiarity with using Visual Studio projects and the Git source control system.

Prerequisites
-------------
* Windows 7 or later
* [Visual Studio 2015 Community Edition](https://www.visualstudio.com/en-us/downloads/download-visual-studio-vs.aspx) or the equivalent
* Git for Windows Windows - options include
[msysgit](https://code.google.com/p/msysgit/),
[TortoiseGit](https://code.google.com/p/tortoisegit/) (a GUI wrapper around msysgit)
or the git tools for Visual Studio Pro (integrated with Team Explorer)

Setup and Build
---------------
1. Clone the source for PDFsharp:
 * example: `git clone https://github.com/inexorabletash/PDFsharp.git`
 * This fork includes required patches that have not yet been accepted upstream.
2. Using Visual Studio, open `PDFsharp\code\BuildAll-PdfSharp-VS2010.sln`, select the Release target, and build.
3. Clone this repository:
 * example: `git clone https://github.com/inexorabletash/travellermap.git`
4. In your clone, copy the included `web.config.sample` file to `web.config`
5. Using Visual Studio, open `Maps.sln`
6. In the Solution Explorer pane, in both the Maps project and the UnitTests project, delete and re-add the references to PdfSharp to point to the PDFSharp DLL you just built (`PDFsharp\code\PDFSharp\bin\Release\PdfSharp.dll`)
7. Optionally, modify the `web.config` file in the solution:
 * Add an admin key - this can be used to trigger flushing of the memory cache and rebuilding the search index.
 * Find the `sessionState` element and the `stateConnectionString` attribute; change `50103` to
 your local IISExpress port number. Find this by opening the Maps project's properties and looking for the
 Web tab, "Servers" subsection, Project URL box (mine says `http://localhost:50103/` for example).
8. Select the Debug or Release target and build the Maps target.

Trying it out
-------------
* You can run it however you like; I use F5 to start debugging.
* IIS will start and IE will connect to the site, launching the default page (`index.html`)
* The map will display!

To Add a Database
-----------------
Make a database:

1. Right-click the Maps project
2. Select **Add New Item...**
3. Select **SQL Server Database**
4. Give it a good name
5. Click **Add**
6. The system will prompt you to add the database to the `App_Data` folder; pick **Yes**.

Now that the database has been added, you must change your web.config:

1. Find the `App_Data` folder
2. Within it, double-click your newly-created `.mdf` file
3. The Database Explorer will open
4. In the **Properties** panel, navigate to the `Connection String` property; copy the value of that property
5. Open your `web.config` and find the `connectionStrings` element
6. Paste your copied connection string information from the properties panel into the `connectionString` attribute of both the `SqlDev` and `SqlProd` names
7. Save your `web.config`

Now that your application can find your empty database, the reindex action on the admin page will fill an empty database:

1. Start the site; I use **F5** to start debugging
2. Your browser will open to the default page, `http://localhost:<YOUR_PORT>/index.htm`
2. Edit the URL to load: `http://localhost:<YOUR_PORT>/admin/admin?action=reindex`

You will see output from the re-indexing operation; when complete the page will show a summary followed by a little Omega symbol (&Omega;) at the bottom of the page. Hit your back button and try out Search.

NOTE: When the Debug target is running, only the worlds in "selected" sectors will be indexed. A Release build will index all worlds.
