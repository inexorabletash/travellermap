(function(global) {

  //////////////////////////////////////////////////////////////////////
  //
  // A* Algorithm
  //
  // Based on notes in _AI for Game Developers_, Bourg & Seemann,
  //     O'Reilly Media, Inc., July 2004.
  //
  //////////////////////////////////////////////////////////////////////

  function Node(id, cost, steps, parent) {
    this.id = id;
    this.cost = cost;
    this.steps = steps;
    this.parent = parent;
  }

  function List() {
    // TODO: Should be a Set
    this.list = {};
    this.count = 0;
  }

  List.prototype = {
    isEmpty: function() {
      return (this.count == 0);
    },

    contains: function(node) {
      return (this.list[node.id] !== undefined);
    },

    add: function(node) {
      if (!this.contains(node)) {
        this.list[node.id] = node;
        this.count++;
      }

      var str = '';
      for (var key in this.list) {
        str += ' ' + key + ': ' + this.list[key] + '   ';
      }
    },

    remove: function(node) {
      if (this.contains(node)) {
        delete this.list[node.id];
        this.count--;
      }

      var str = '';
      for (var key in this.list) {
        str += ' ' + key + ': ' + this.list[key] + '   ';
      }
    },

    getLowestCostNode: function() {
      var cost  = undefined;
      var node  = undefined;

      for (var key in this.list) {
        var currentNode = this.list[key];

        if (cost === undefined || currentNode.cost < cost) {
          node = currentNode;
          cost = currentNode.cost;
        }
      }

      return node;
    }
  };


  function CalculateRoute(startHex, endHex, jump) {
    var open   = new List();
    var closed = new List();

    // add the starting node to the open list
    open.add(new Node(startHex, 0, 0, undefined));

    // while the open list is not empty
    while (!open.isEmpty()) {
      // current node = node from open list with the lowest cost
      var currentNode = open.getLowestCostNode();

      // if current node = goal node then path complete
      if (currentNode.id == endHex) {
        var path = [];

        var node = currentNode;
        path.unshift(node.id);

        while (node.parent) {
          node = node.parent;
          path.unshift(node.id);
        }

        return path;
      } else {
        // move current node to the closed list
        open.remove(currentNode);
        closed.add(currentNode);

        // examine each node adjacent to the current node
        var adjacentHexes = reachable(currentNode.id, jump);

        // for each adjacent node
        adjacentHexes.forEach(function(adjacentHex) {
          var adjacentNode = new Node(adjacentHex, -1, currentNode.steps+1, currentNode);

          // if it isn't on the open list
          if (open.contains(adjacentNode))
            return;

          // and it isn't on the closed list
          if (closed.contains(adjacentNode))
            return;

          // and it isn't an obstacle then
          if (map[adjacentHex] === undefined)
            return;

          // move it to open list and calculate cost
          open.add(adjacentNode);

          var cost = adjacentNode.steps + dist(adjacentHex, endHex);

          // NOTE: Can tweak cost, e.g.
          //   if RedZone   then cost += 2
          //   if AmberZone then cost += 1
          //   if NoWater   then cost += 1
          //   if !Imperial then cost += 1

          adjacentNode.cost = cost;
        });
      }
    }

    return undefined;
  }

  //////////////////////////////////////////////////////////////////////
  //
  // Function:
  //     dist(hexA, hexB) - returns distance in hexes
  //
  // Example:
  //     dist('0101', '0404') returns 5
  //
  //////////////////////////////////////////////////////////////////////

  function dist(a, b) {
    a = Number(a);
    b = Number(b);
    var a_x = div(a,100);
    var a_y = mod(a,100);
    var b_x = div(b,100);
    var b_y = mod(b,100);

    var dx = b_x - a_x;
    var dy = b_y - a_y;

    var adx = Math.abs(dx);
    var ody = dy + div(adx, 2);

    if (odd(a_x) && even(b_x)) {
      ody += 1;
    }

    return max(adx - ody, ody, adx);
  }

  function even(x) { return (x % 2) == 0; }
  function odd (x) { return (x % 2) != 0; }

  function div(a, b) { return Math.floor(a / b); }
  function mod(a, b) { return Math.floor(a % b); }

  function max(a, b, c) { return (a >= b && a >= c) ? a : (b >= a && b >= c) ? b : c; }


  //////////////////////////////////////////////////////////////////////
  //
  // Function:
  //    reachable(hex, jump) - returns list of hexes within range
  //
  // Example:
  //    reachable('0101', 2) returns (0102, 0201, 0202, 0301, 0302)
  //
  // Note:
  //    This just walks over a square and calls dist(); it could be
  //     more intelligent
  //
  //////////////////////////////////////////////////////////////////////

  function reachable(hex, jump) {
    var results = [];

    var h = Number(hex);
    var x = div(h, 100);
    var y = mod(h, 100);

    for (var rx = x - jump; rx <= x + jump; ++rx) {
      for (var ry = y - jump; ry <= y + jump; ++ry) {
        if (rx >= 1 && rx <= SECTOR_WIDTH && ry >= 1 && ry <= SECTOR_HEIGHT) {
          var candidate = HexString(rx, ry);
          var distance = dist(hex, candidate);
          if (distance > 0 && distance <= jump) {
            results.push(candidate);
          }
        }
      }
    }

    return results;
  }

  var SECTOR_WIDTH = 32;
  var SECTOR_HEIGHT = 40;
  function HexString(x, y) {
    var str = '';
    if (x < 10) str += '0';
    str += x.toString();

    if (y < 10) str += '0';
    str += y.toString();
    return str;
  }

  // TODO: Refactor this
  var map;
  global.computeRoute = function(sector, start, end, jump) {
    map = sector;
    return CalculateRoute(start, end, jump || 1);
  };

}(this));
