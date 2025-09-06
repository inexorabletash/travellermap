NodeJs Traveller Map - Source Code
==================================

This is an implementation of the API behind https://travellermap.com - an online resource for fans
of the Traveller role playing game.

The code was forked in order to provide an implementation which uses the commonly provided data,
but runs without the legacy .NET depenencies which make this difficult to port to non-windows
architechtures allowing for containerizations.

It allows allows for dynamic overrides of the predefined sector data to allow users to allow it
to support campaign-specific MTU variations.

Note that the implementation is not complete.  For example only a single render format is currently
supported.


The Traveller game in all forms is owned by Mongoose Publishing. Copyright 1977 - 2024 Mongoose Publishing. [Fair Use Policy](https://cdn.shopify.com/s/files/1/0609/6139/0839/files/Traveller_Fair_Use_Policy_2024.pdf?v=1725357857)

See LICENSE.md for software licensing details.


Useful Links
------------

* The site itself: https://travellermap.com
* How the site works: https://travellermap.com/doc/about
* API documentation: https://travellermap.com/doc/api
* Credits for the data: https://travellermap.com/doc/credits
* Blog: https://travellermap.blogspot.com
* GitHub repo: https://github.com/rodchamberlin/travellermap
* Issue tracker: https://github.com/rodchamberlin/travellermap/issues


Dependencies
------------

* This is a pure nodejs/typescript implmenetation.  However it uses
  the "canvas" package which requires specific binaries and libraries
  if not precompiled for your platform.


Running
-------

You can run travellermap-node locally by:

* Checking out, download and compiling this repository
* TODO: Running via npm using npx @xxxx/travellermap-node
* Using docker