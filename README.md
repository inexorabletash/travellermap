NodeJs Traveller Map - Source Code
==================================

This is an implementation of the API behind https://travellermap.com - an online resource for fans
of the Traveller role playing game.

The code was forked in order to provide an implementation which uses the commonly provided data,
but runs without the legacy .NET depenencies which make this difficult to port to non-windows
architechtures allowing for containerizations.

It allows for dynamic overrides of the predefined sector data to allow users to allow it
to support campaign-specific MTU variations.

Note that the implementation is not complete.  For example only a single render format is currently
supported.


The Traveller game in all forms is owned by Mongoose Publishing. Copyright 1977 - 2024 Mongoose Publishing. [Fair Use Policy](https://cdn.shopify.com/s/files/1/0609/6139/0839/files/Traveller_Fair_Use_Policy_2024.pdf?v=1725357857)

See LICENSE.md for software licensing details.

Rationale
---------

Joshua Bell's Travellermap is a great utility for traveller campaigns.  However, it remains somewhat frustrating 
in that it cannot account for the specific variations which can occur as a result of a traveller campaign.  What
happens if the plot dictates a world suddenly becomes a red-zone, or if the players actions result in change in the
government type of a world, or if you have simply decided to define some of the worlds in the Foreven sector?

Travellermap itself runs on fairly old software, and has database dependencies which make it hard to run a 
"private" copy for "MTU" (My Traveller Universe).  As such I decided to produce something more portable which
anyone can run and configure.  This lead to travellermap-node.

Travellermap-node is a fork of travellermap to maintain the same data is the original, but is completely implemented 
in typescript.  In theory we could replace the server component entirely since all rendering is done through a
canvas, although that has not been done here since we would still require a server for the datafiles.

Useful Links
------------

* The site itself: https://travellermap.com
* How the site works: https://travellermap.com/doc/about
* API documentation: https://travellermap.com/doc/api
* Credits for the data: https://travellermap.com/doc/credits
* Blog: https://travellermap.blogspot.com
* GitHub repo: https://github.com/rodchamberlin/travellermap-node
* Issue tracker: https://github.com/rodchamberlin/travellermap-node/issues


Dependencies
------------

* This is a pure nodejs/typescript implementation.  However it uses
  the "canvas" package which requires specific binaries and libraries
  if not precompiled for your platform.


Running
-------

Travellermode-node does not require any additional resources other than an installed version of nodejs, or docker.  
It does require or use a database.  

You can run travellermap-node locally by:

* Checking out, download and compiling this repository
* npx @rodchamberlin/travellermap-node
* Using docker (image not yet published)


Compatibility
-------------

In general travellermap-node will read and process all existing sector files, applying the same rules as in the
C# application.  The following exceptions exist:

* Border and Route colors configured through CSS  in sector files is ignored
  * Instead use the res/Sectors/allegiance_global.tab which allows global settings for allegiances
* Searching is currently only supported for worlds and does both prefix and substring searches for worlds
* Rendering
  * Only one render mode is currently supported
  * Rendering is not an exact match for the travellermap version, fonts/layout are slightly different
  * There is no "universe-level" map for high zoom levels, instead we just render all known sectors.
* At present only one Milieu directory is loaded at a time.
  * Travellermap overloads some unofficial data with official data when loading the data for a Millieu
  * at present travellermap-node simply loads a single directory

Additions
---------

## Global Allegiance configuration

There is now a single top-level file called allegiance_global.tab which contains details of all allegiances
and the appropriate colors for rendering them.  It is a tab-separated file with the following fields:

* Code - the allegiance code
* Legacy - the legacy code
* BaseCode - the base code
* Name - the name of the allegiance
* Location - the location (not really used)
* BorderColor - the HTML color code to use for borders
* RouteColor - the HTML color code to use for jump-routes

The legacy and base-code are relevant because, there is a legacy code or base code and colors are not set for
the specific allegiance code, then we will instead attempt to lookup color information from an allegiace with 
code equal to legacy code and then base code.  This allows for a single color definition for "Im" (for example) 
which applies to ImDd as well as ImDa, ImSy, etc., making larger empires easier to configure.


## Dynamic Overrides

YML Files under "static/res/overrides/<millieu>" are override files.  

Override files are read at startup and override the basic map data as defined under static/res/Sectors.  
The contents of the override directory are also monitored for changes.  If any file changes then the sector
data is reloaded.  Where multiple workers exist, all workers must be restarted (since each worker has a complete
copy of the sector data).

### Override Format

Each file contains override data in YML format.  By convention each file covers a single sector, however
this is not enforced or required.  

The file contents is a YML object with the following fields 

### Field: sector

Although the "sector" field can be an object or array of objects (for multiple sectors).  It can also be set 
to a string value.  If set to a string value then it determines the default sector for all other entries in this
file.


| Field | Usage
|-------| ----
| sector | The sector abbreviation for the sector this applies to
| name   | The sector name
| x      | The sector x coordinate
| y      | The sector y coordinate
| subsector | A record of subsector-id-lettter (A-P) to subsector name

export type Override = {
sector: OverrideSector[] | OverrideSector | string;
allegiance: OverrideAllegiance[] | OverrideAllegiance;
border: OverrideBorder[] | OverrideBorder;
route: OverrideRoute[] | OverrideRoute;
world: OverrideWorld[] | OverrideWorld;
}

export type OverrideCommon = {
sector: string;
}


### Field: allegiance

Allegiance overrides for this sector.  

| Field | Usage
|-------| ----
| sector | The sector abbreviation for the sector this applies to (if not present will default from the global sector definition or first defined sector)
| code   | The allegiance code
| legacy | The legacy allegiance code
| baseCode | The base allegiance code
| name   | The name for this allegiance
| location | A brief description of where in the universe this allegiance lies
| borderColor | HTML color for allegiance borders
| routeColor | HTML color for allegiance routes


### Field: Border

Borders represent faction borders.  They are the list of hexes which are on the factions border.  These hexes
**must** by listed in a clockwise direction.  

When a border spans sectors, then each sector must continue the border to the notional hexes which would be 
"just outside" the sector.  The following table outlines these border hexes.

```
Generic outer borders for a sector:

Top:    0000 0100 0200 0300 0400 0500 0600 0700 0800 0900 1000 1100 1200 1300 1400 1500 1600 1700 1800 1900 2000 2100 2200 2300 2400 2500 2600 2700 2800 2900 3000 3100 3200 3300
Right:  3300 3301 3302 3303 3304 3305 3306 3307 3308 3309 3310 3311 3312 3313 3314 3315 3316 3317 3318 3319 3320 3321 3322 3323 3324 3325 3326 3327 3328 3329 3330 3331 3332 3333 3334 3335 3336 3337 3338 3339 3340 3341
Bottom: 3341 3241 3141 3041 2941 2841 2741 2641 2541 2441 2341 2241 2141 2041 1941 1841 1741 1641 1541 1441 1341 1241 1141 1041 0941 0841 0741 0641 0541 0441 0341 0241 0141 0041
Left:   0041 0040 0039 0038 0037 0036 0035 0034 0033 0032 0031 0030 0029 0028 0027 0026 0025 0024 0023 0022 0021 0020 0019 0018 0017 0016 0015 0014 0013 0012 0011 0010 0009 0008 0007 0006 0005 0004 0003 0002 0001 0000
```

| Field         | Usage
|---------------| ----
| sector        | The sector abbreviation for the sector this applies to (if not present will default from the global sector definition or first defined sector)
| hexes         | An array of the hexes through which this border should run
| replace       | If present this should be a hex through which an existing border runs.  This border will be removed
| allegiance    | The allegiance code for the border    
| color: string | The border color (note this will usually be derived from allegiance data)
| label: string | The label for the enclosed region
| labelPosition | The center hex for the label
| wrapLabel     | Boolean - if true then the label will be split into multiple lines one per word 


### Field: Route

| Field        | Usage
|--------------| ----
| sector       | The sector abbreviation for the sector this applies to (if not present will default from the global sector definition or first defined sector)
| start        | The starting hex for the route
| end          | The end hex for the route
| type         | The route type
| allegiance   | The allegiance for the route
| color        | The color for the route (note this will usually be derived from color)
| style        | The route style (not used)
| startOffsetX | The X sector offset for the start coordinate (if -1 then the start is in the previous sector)
| startOffsetY | The Y sector offset for the start coordinate
| endOffsetX   | The X sector offset for the end coordinate
| endOffsetY   | The Y sector offset for the end coordinate
| replace      | If present any route whose start or end matches this coordinate will be removed.  May be a list of two coordinates (at which point only routes whose start and end match will be removed)


### Field: World

| Field      | Usage
|------------| ----
| sector     | The sector abbreviation for the sector this applies to (if not present will default from the global sector definition or first defined sector)
| hex        | The hex
| name       |
| uwp        |
| bases      | The "bases" character
| notes      | The "remarks".  This can either be an array of difference codes, or a space-separated list of codes
| zone       | The zone type R == Red, A == Amber
| pbg        |
| allegiance |
| stars      |
| ix         |
| ex         |
| cx         |
| nobility   |
| w          |
| ru         |
| delete     | A boolean.  If true then this world will be removed (is someone playing with Ancient weapons?)



Running
-------

### Environment variables

* WORKERS - the number of parallel worker threads to run (default 8).  Set to 0 to not use parallelism
  * Note that parallel workers each have to load a complete copy of the active universe; it isn't practical to share 
the loaded maps between worker instances in javascript. 
* PINO_LOG_LEVEL - log level (default INFO)
* JSON_LOGGING - if present use JSON logging instead of text logging


### Docker container

The docker build creates the application under "/app" directory.

Volume overrides can be used on /app/static/res/overrides to modify the overrides when the application is running.








