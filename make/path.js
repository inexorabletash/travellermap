/*global Traveller */
(global => {
  // A* Algorithm
  //
  // Based on notes in _AI for Game Developers_, Bourg & Seemann,
  //     O'Reilly Media, Inc., July 2004.

  class Node {
    constructor(id, cost, parent) {
      this.id = id;
      this.cost = cost;
      this.parent = parent;
    }
  }

  class List {
    constructor() {
      // TODO: Should be a Set
      this.list = {};
      this.count = 0;
    }

    isEmpty() {
      return this.count == 0;
    }

    contains(node) {
      return this.list[node.id] !== undefined;
    }

    add(node) {
      if (!this.contains(node)) {
        this.list[node.id] = node;
        this.count++;
      }
    }

    remove(node) {
      if (this.contains(node)) {
        delete this.list[node.id];
        this.count--;
      }
    }

    getLowestCostNode() {
      let cost  = undefined;
      let node  = undefined;

      for (const key in this.list) {
        const currentNode = this.list[key];

        if (cost === undefined || currentNode.cost < cost) {
          node = currentNode;
          cost = currentNode.cost;
        }
      }

      return node;
    }
  }

  function computeRoute(map, startHex, endHex, jump) {
    const open   = new List();
    const closed = new List();

    // add the starting node to the open list
    open.add(new Node(startHex, 0, 0, undefined));

    // while the open list is not empty
    while (!open.isEmpty()) {
      // current node = node from open list with the lowest cost
      let currentNode = open.getLowestCostNode();

      // if current node = goal node then path complete
      if (currentNode.id == endHex) {
        const path = [];

        let node = currentNode;
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
        const adjacentHexes = reachable(currentNode.id, jump);

        // for each adjacent node
        adjacentHexes.forEach(adjacentHex => {
          const adjacentNode = new Node(adjacentHex, -1, currentNode);

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

          // NOTE: Can tweak cost, e.g.
          //   if RedZone   then cost += 2
          //   if AmberZone then cost += 1
          //   if NoWater   then cost += 1
          //   if !Imperial then cost += 1

          adjacentNode.cost = currentNode.cost + dist(adjacentHex, endHex);
        });
      }
    }

    return undefined;
  }

  // Distance in hexes
  // dist('0101', '0404') -> 5
  function dist(a, b) {
    a = Number(a);
    b = Number(b);
    const a_x = div(a,100);
    const a_y = mod(a,100);
    const b_x = div(b,100);
    const b_y = mod(b,100);

    const dx = b_x - a_x;
    const dy = b_y - a_y;

    let adx = Math.abs(dx);
    let ody = dy + div(adx, 2);

    if (odd(a_x) && even(b_x))
      ody += 1;

    return max(adx - ody, ody, adx);
  }

  function even(x) { return (x % 2) == 0; }
  function odd (x) { return (x % 2) != 0; }

  function div(a, b) { return Math.floor(a / b); }
  function mod(a, b) { return Math.floor(a % b); }

  function max(a, b, c) { return (a >= b && a >= c) ? a : (b >= a && b >= c) ? b : c; }


  // Returns list of hexes within range:
  // reachable('0101', 2) -> ['0102', '0201', '0202', '0301', '0302']
  //
  // This just walks over a square and calls dist(); it could be smarter.
  function reachable(hex, jump) {
    const results = [];

    const h = Number(hex);
    const x = div(h, 100);
    const y = mod(h, 100);

    for (let rx = x - jump; rx <= x + jump; ++rx) {
      for (let ry = y - jump; ry <= y + jump; ++ry) {
        if (rx >= 1 && rx <= Traveller.Astrometrics.SectorWidth &&
            ry >= 1 && ry <= Traveller.Astrometrics.SectorHeight) {
          const candidate = hexString(rx, ry);
          const distance = dist(hex, candidate);
          if (distance > 0 && distance <= jump) {
            results.push(candidate);
          }
        }
      }
    }

    return results;
  }

  function hexString(x, y) {
    let str = '';
    if (x < 10) str += '0';
    str += x.toString();

    if (y < 10) str += '0';
    str += y.toString();
    return str;
  }

  // Exports
  global.computeRoute = computeRoute;

})(this);
