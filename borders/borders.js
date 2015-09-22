//
// Border Generation for Traveller
//
// By Joshua Bell (inexorabletash@gmail.com, http://travellermap.com)
//
// Based on allygen by J. Greely http://dotclue.org/t20
//
// The Traveller game in all forms is owned by Far Future Enterprises.
// Copyright (C) 1977-2008 Far Future Enterprises.

var UNALIGNED = "--";
var NON_ALIGNED = "Na";

var AllegianceMap = (function() {
  'use strict';

  function AllegianceMap(width, height, origin_x, origin_y) {
    this.width = width;
    this.height = height;
    this.origin_x = arguments.length >= 3 ? origin_x : 1;
    this.origin_y = arguments.length >= 4 ? origin_y : 1;

    this.map = [];
    for (var x = 0; x < width; ++x) {
      this.map[x] = [];
      for (var y = 0; y < height; ++y) {
        this.map[x][y] = { 'occupied': false, 'alleg': UNALIGNED, 'mark': false };
      }
    }
  }

  AllegianceMap.prototype = {
    getBounds: function() {
      return {
        'top':    this.origin_y,
        'left':   this.origin_x,
        'right':  this.origin_x + this.width  - 1,
        'bottom': this.origin_y + this.height - 1
      };
    },

    inBounds: function( x, y ) {
      if (x - this.origin_x < 0 || x - this.origin_x >= this.width ||
          y - this.origin_y < 0 || y - this.origin_y >= this.height) {
        return false;
      } else {
        return true;
      }
    },

    foreach: function(func) {
      var bounds = this.getBounds();
      for (var c = bounds.left; c <= bounds.right; ++c) {
        for (var r = bounds.top; r <= bounds.bottom; ++r) {
          func(c, r);
        }
      }
    },

    isOccupied: function(x, y) {
      if (!this.inBounds(x, y))
        throw "Coordinates out of bounds";

      return this.map[x - this.origin_x][y - this.origin_y].occupied;
    },

    setOccupied: function(x, y, occupied) {
      if (!this.inBounds(x, y))
        throw "Coordinates out of bounds";

      this.map[x - this.origin_x][y - this.origin_y].occupied = occupied;
    },

    getAllegiance: function(x, y) {
      if (!this.inBounds(x, y))
        throw "Coordinates out of bounds";

      return this.map[x - this.origin_x][y - this.origin_y].alleg;
    },

    getTrueAllegiance: function(x, y) {
      if (!this.inBounds(x, y))
        throw "Coordinates out of bounds";

      var hex = this.map[x - this.origin_x][y - this.origin_y];
      return hex.trueAllegiance || hex.alleg;
    },

    setAllegiance: function(x, y, effectiveAllegiance, trueAllegiance) {
      if (!this.inBounds(x, y))
        throw "Coordinates out of bounds";

      this.map[x - this.origin_x][y - this.origin_y].alleg = effectiveAllegiance;
      this.map[x - this.origin_x][y - this.origin_y].trueAllegiance = trueAllegiance;
    },

    // TODO: Would be simpler if we returned a Hex object
    isMarked: function(x, y) {
      if (!this.inBounds(x, y))
        throw "Coordinates out of bounds";

      return this.map[x - this.origin_x][y - this.origin_y].mark;
    },

    setMarked: function(x, y, mark) {
      if (!this.inBounds(x, y))
        throw "Coordinates out of bounds";

      this.map[x - this.origin_x][y - this.origin_y].mark = mark;
    }
  };

  return AllegianceMap;
}());

// Find neighbor of a hex in a given direction.
//       2
//     1   3
//       *
//     0   4
//       5
// NOTE: This assumes that hex 0101 is "above" hex 0201; results
// will be incorrect for zero-based coordinate systems.
function neighbor(c, r, direction) {
  'use strict';
  switch (direction) {
    case 0: r += 1 - (c-- % 2); break;
    case 1: r -= (c-- % 2); break;
    case 2: r--; break;
    case 3: r -= (c++ % 2); break;
    case 4: r += 1 - (c++ % 2); break;
    case 5: r++; break;
  }

  return [c, r];
}

function erode(map, allegiance, n) {
  'use strict';
  var erodeList = [];

  map.foreach(function(c, r) {

    // Only process empty hexes of the specified allegiance
    if (map.isOccupied(c, r) || map.getAllegiance(c, r) !== allegiance) {
      return;
    }

    for (var dir = 0; dir < 6; ++dir) {
      var count = 0;

      for (var offset = 0; offset < n; ++offset) {
        var hex = neighbor(c, r, (dir + offset) % 6);
        var x = hex[0];
        var y = hex[1];

        count += (!map.inBounds(x, y) || map.getAllegiance(x, y) !== allegiance);
      }

      if (count >= n) {
        erodeList.push([c, r]);
      }
    }
  });

  // Break the spots we identified
  for (var i = 0; i < erodeList.length; ++i) {
    var coord = erodeList[i];
    map.setAllegiance(coord[0], coord[1], UNALIGNED);
  }

  return erodeList.length > 0;
}

// TODO: Standardize on ( x, y ) vs. [ x, y ] vs. { x:x, y:y }

function walk(map, start_x, start_y, allegiance, func) {
  'use strict';
  var border = [[start_x, start_y]];

  if (func) { func(start_x, start_y, -1); }

  // Directions checked in starting hex: sw=0, nw=1, n=2 (by definition)
  var checked = [true, true, true];
  var checkfirst = 3; // northeast - first direction to test
  var checklast;
  var current = [start_x, start_y]; // First hex
  var next; // Next hex

  var done = false;
  while (!done) {
    checklast = checkfirst + 5; // test all directions

    var dir;
    for (var i = checkfirst; i <= checklast; ++i) {
      dir = i % 6;

      // Start hex?
      if (current[0] === start_x && current[1] === start_y) {
        // Have we checked this direction already?
        if (checked[dir]) {
          done = true;
          break;
        }

        // Nope; mark it and keep going
        checked[dir] = true;
      }

      next = neighbor(current[0], current[1], dir);

      if (!map.inBounds(next[0], next[1]))
        continue;

      if (map.getAllegiance(next[0], next[1]) === allegiance)
        break;
    }

    if (!done && map.inBounds(next[0], next[1]) &&
        map.getAllegiance(next[0], next[1]) === allegiance) {
      // Found a friend!
      if (func)
        func(next[0], next[1], dir);
      border.push(next);

      checkfirst = (dir + 4) % 6;
      current = next;
    } else {
      // Can't find a friend!
      break;
    }
  }
  return border;
}

// Return [x, y] where x, y is the hex coordinate closest to the top
// left bounds for the given allegiance, or undefined if that allegiance
// has no hexes claimed. Used for passing into walk() to find the
// border path for persistence/rendering.
function findTopLeft(map, allegiance) {
  'use strict';
  var bounds = map.getBounds();

  for (var c = bounds.left; c <= bounds.right; ++c) {
    for (var r = bounds.top; r <= bounds.bottom; ++r) {
      if (map.getAllegiance(c, r) === allegiance)
        return [c, r];
    }
  }

  return undefined;
}

function breakSpans(map, allegiance, n) {
  'use strict';
  var breakList = []; // List of hexes at which to "break" once scan is done
  var spanList = []; // Running list of contiguous non-world hexes
  var dirList = []; // Running list of contiguous non-world hexes in same dir
  var lastDir = -1;

  function breakCallback(c, r, dir) {
    // Mark the current hex as visted - only need to walk each region once
    map.setMarked(c, r, true);

    // Sneaky bit #1:
    // Break only when three hexes are in the same direction
    // to avoid breaks at concave turns in a border that might
    // be between worlds.
    if (lastDir !== dir)
      dirList = [];

    if (map.inBounds(c, r) && !map.isOccupied(c, r)) {
      spanList.push([c, r]);
      dirList.push([c, r]);

      // Sneaky bit #2:
      // Break when we've found a span of the right length, and with
      // enough hexes in a line to avoid bad breaks
      if (spanList.length >= n && dirList.length >= 2) {
        breakList.push(dirList[dirList.length - 2]);

        spanList = [];
        dirList = [];
      }
    } else {
      // Nope, not a span - reset, keep looking
      spanList = [];
      dirList = [];
    }

    lastDir = dir;
  }

  // Scan the whole map, looking for regions of the appropriate allegiance.
  // When found, walk the perimeter looking for spans to break
  var bounds = map.getBounds();
  for (var c = bounds.left; c <= bounds.right; ++c) {
    // Start out fresh on each row since it is not adjacent to
    // last hex on previous row. If allegiance matches, it will
    // either be marked (same region) or not (different region)
    var previous = null;

    for (var r = bounds.top; r <= bounds.bottom; ++r) {
      var current = map.getAllegiance(c, r);
      var walked = map.isMarked(c, r);

      if (previous === current) {
        // Inside the same region as previous hex
        // per algorithm, already walked it, so skip
        continue;
      }

      previous = current;

      if (walked || current !== allegiance ||
          current === UNALIGNED ||
          current === NON_ALIGNED) {
        // Don't care or already processed, so skip
        continue;
      }

      // Walk the borders of this region looking for spans to break
      walk(map, c, r, allegiance, breakCallback);
    }
  }

  // Clear marks
  map.foreach(function(c, r) { map.setMarked(c, r, false); });

  // Break the spots we identified
  for (var i = 0; i < breakList.length; ++i) {
    var coord = breakList[i];
    map.setAllegiance(coord[0], coord[1], UNALIGNED);
  }

  return breakList.length > 0;
}


function buildBridges(map, allegiance) {
  'use strict';

  // Scan the whole map, looking for 1 parsec gaps within a polity
  // that could be bridged. Insert the first possible bridge detected
  // in each case.

  function neighborAllegiance(c, r, dir) {
    var hex = neighbor(c, r, dir);
    var x = hex[0];
    var y = hex[1];
    return map.inBounds(x, y) ? map.getAllegiance(x, y) : UNALIGNED;
  }

  var bounds = map.getBounds();
  for (var c = bounds.left; c <= bounds.right; ++c) {
    for (var r = bounds.top; r <= bounds.bottom; ++r) {
      if (map.getAllegiance(c, r) === UNALIGNED) {
        var na = [];
        for (var i = 0; i < 6; i += 1)
          na[i] = neighborAllegiance(c, r, i);

        for (i = 0; i < 6; i += 1) {
          if (na[i] === allegiance &&
              na[(i + 1) % 6] !== allegiance &&
              na[(i + 2) % 6] === allegiance) {
            map.setAllegiance(c, r, allegiance);
            break;
          }
        }
      }
    }
  }
}


// Claim all unclaimed hexes to be of the specified allegiance
function claimAllUnclaimed(map, allegiance) {
  'use strict';
  map.foreach(function(c, r) {
    if (map.getAllegiance(c, r) === UNALIGNED)
      map.setAllegiance(c, r, allegiance);
  });
}

// Apply the erode and breakSpans algorithms until a steady state
// is achieved.
function processAllegiance(map, allegiance) {
  'use strict';
  claimAllUnclaimed(map, allegiance);

  // Reduce to the "alpha shape" of the polity
  //
  var dirty;
  do {
    dirty = false;

    // don't be too greedy; snip off empty hexes on the edges of empires
    // with 3 unallied, adjacent neighbors
    //
    dirty = dirty || erode(map, allegiance, 3);

    // don't be too greedy, part deux; don't claim spans of 4 or more
    // empty hexes along borders. And break the longest ones first
    //
    dirty = dirty || breakSpans(map, allegiance, 4);

    // repeat until a steady state is obtained
  }
  while (dirty);

  buildBridges(map, allegiance);
}

// Process all allegiances in the map, starting with the polity with
// the smallest number of claimed worlds.
function processMap(map, success_callback, progress_callback) {
  'use strict';
  var counts = {};

  // Compute allegiance counts

  setTimeout(function() {
    if (progress_callback) progress_callback('Computing allegiance counts...');

    map.foreach(function(c, r) {
      if (map.isOccupied(c, r)) {
        var alleg = map.getAllegiance(c, r);
        if (counts[alleg]) {
          counts[alleg] += 1;
        } else {
          counts[alleg] = 1;
        }
      }
    });

    var list = [];
    Object.keys(counts).forEach(function(key) {
      list.push({ allegiance: key, count: counts[key] });
    });
    list.sort(function(a, b) { return a.count - b.count; });


    function doNext() {
      if (!list.length) {
        success_callback();
        return;
      }
      var polity = list.shift();
      if (polity.allegiance !== NON_ALIGNED && polity.count > 1) {
        if (progress_callback)
          progress_callback('Processing allegiance ' + polity.allegiance +
                            ' (' + polity.count + ' worlds)');
        processAllegiance(map, polity.allegiance);
      }
      setTimeout(doNext, 0);
    }
    doNext();

  }, 0);
}
