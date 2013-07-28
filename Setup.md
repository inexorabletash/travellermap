The Traveller Map - Setup Guide
================================

This is the setup guide to source code behind http://travellermap.com - an online resource for fans
of the Traveller role playing game.

The Traveller game in all forms is owned by Far Future Enterprises. 
Copyright (C) 1977-2013 Far Future Enterprises.

See LICENSE.md for software licensing details.

Useful Links
------------

* The site itself: http://travellermap.com
* How the site works: http://travellermap.com/info.htm
* API documentation: http://travellermap.com/api.htm
* Credits for the data: http://travellermap.com/credits.htm
* Blog: http://travellermap.blogspot.com
* GitHub repo: https://github.com/inexorabletash/travellermap
* TODO list/bug tracking: https://trello.com/b/y61wmEKJ/travellermap-com-wish-list

Travellermap comes with a web.config and no database MDF file-- but you can make one if you want. The database is only necessary
for search, so you should be able to explore other parts of the system without it.

Initial Setup
------------------------
Obtain a copy of the source somehow. I used the git tools for Visual Studio due to their integration with the Team Explorer.
Obtain a copy of the PDFSharp 1.32 source. 
Alter the PDFSharp source by applying the patch contained in pdfsharp.patch:
	*N.B. GNU Patch won't patch the stock 1.32 sources with this patch because this patch applies to the tip of the PDFSharp repo.
	**N.B. As of this writing, the Codeplex svn interface is not functioning so you cannot get the tip of the PDFSharp repo.
	***N.B. The author used TortiseUDiff to view the patch and manually apply the hunks to the 3 patched files. Took 5 min.
Build PDFSharp
Using Visual Studio, obtain a copy of the source and load the solution file Maps.sln
Delete and readd the references to PDFSharp in the Maps project and UnitTests project to point to the PDFSharp DLL you just built
The web.config is not a part of the solution, so find it on the hard drive and edit it. Change your AdminKey.
Further dpdate the web.config line 
	<sessionState mode="Off" stateConnectionString="tcpip=127.0.0.1:50103" sqlConnectionString="data source=127.0.0.1;Trusted_Connection=yes" cookieless="true" timeout="20"/>
	so that it uses your local IISExpress port number. Find this by opening the Maps project's properties and looking for the
	Web tab, "Servers" subsection, Project URL box (mine says http://localhost:50103/), but your port will be different.
	Alter the stateConnectionString property to reflect the change in port number.

Trying it out
-------------
Build your solution
You can run it however you like; I use F5 to start debugging.
IIS will start and IE will connect to the site.
	You can select other browsers for testing at this stage
The map will display!


To Add a Database
-----------------
Make a database:
	Right-click the Maps project
	Add New Item...
	Select SQL Server Database
	Give it a good name
	Click add
	The system will prompt you to add the database to the App_Data folder; pick yes.


Now that the database has been added, you must change your web.config:
	Find the App_Data folder
	Within it, double-click your newly-created .mdf file
	The Database Explorer will open
	In the properties panel, navigate to the Connection String property; copy the value of that property
	Open your web.config and find the connectionStrings section
	Paste your copied connection string information from the properties panel into the connectionString attribute of both the SqlDev and SqlProd names
	Save your web.config


Now that your application can find your empty database, the reindex action on the admin page will fill an empty database:
	Start the site; I use F5 to start debugging
	In your browser window, alter your url:
	(mine says http://localhost:<random port>/?x=-82.274&y=65.151&scale=64&options=887&style=poster) to read
		http://localhost:<same port>/Admin.aspx/action=?reindex
	You will see output from the reindexing operation
	Then you will see a little Omega symbol at the bottom of the page. 
	Hit your back button and try out Search. I picked Terra.
