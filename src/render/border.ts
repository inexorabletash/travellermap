import {CanvasRenderingContext2D} from "canvas";
import {Sector} from "../universe/sector.js";

import {HEX_SIDE_EXTRA, HEX_SIDE_LENGTH, HEX_X_SCALE} from "./constants.js";


// We are going to assume that the hexes are ordered in a clockwise fashion
// Hex layout
//    0100    0300    0500
//        0200    0400
//    0101    0301    0501
//        0201    0401
//    0102    0302    0502
//
//   So 0101 borders 0200 and 0201 .... 0201 borders 0301 and 0302
//   this means that odd hexes border [x+1,y-1] and [x+1,y] wherase even hexes border [x+1,y] and [x+1,y+1]
//
function clamp(val: number) {
    return Math.min(Math.max(val, -0.5), 0.5);
}

export function renderBorder(ctx: CanvasRenderingContext2D, sector: Sector, hexList: string[],
                             lineColor: typeof CanvasRenderingContext2D.prototype.strokeStyle,
                             fillColor?: typeof CanvasRenderingContext2D.prototype.fillStyle) {
    const hexes = hexList.map(hl => toCoords(parseHex(hl)));
    const paths = borderPath(hexes, ctx.lineWidth, defaultSkipCoord);

    if(fillColor) {
        let first = true;
        ctx.globalAlpha = 0.1;
        ctx.fillStyle = fillColor;
        ctx.globalCompositeOperation = 'destination-over';
        ctx.beginPath();
        for(const path of paths) {
            const offsetPath: [number,number][] =
                path.map(p => [p[0]-0.5,p[1]-0.5])
                    .map(p => [clamp(p[0]), clamp(p[1])]);
            if(first) {
                ctx.moveTo(...offsetPath[0]);
                first = false;
            }
            offsetPath.forEach(p => ctx.lineTo(...p));
        }
        ctx.fill();
    }

    ctx.globalAlpha = 1;
    ctx.beginPath();
    ctx.strokeStyle = lineColor;
    ctx.fillStyle = lineColor;
    ctx.globalCompositeOperation = 'copy';

    for(const path of paths) {
        const offsetPath: [number,number][] = path.map(p => [p[0]-0.5,p[1]-0.5]);
        ctx.moveTo(...offsetPath[0]);
        for(const comp of offsetPath.slice(1)) {
            ctx.lineTo(...comp);
        }
    }
    ctx.stroke();

}


export function borderPath(hexes: [number,number][], lineWidth: number, shouldSkip: (coord: [number,number]) => boolean): [number,number][][] {
    let lastInit = hexes.shift();
    let currentInit = hexes.shift();

    if(lastInit === undefined) {
        return [];
    }
    if(currentInit === undefined) {
        if(!shouldSkip(lastInit)) {
            const path: [number,number][] = [];
            path.push(edgeCoords(lastInit, 5, lineWidth));
            for (let idx = 0; idx < 6; ++idx) {
                path.push(edgeCoords(lastInit, idx, lineWidth));
            }
            return [path];
        }

        // Special case one hex
        return [];
    }

    //hexes.push(lastInit);
    hexes.push(currentInit);

    let last = lastInit;
    let current = currentInit;
    let lastStep = delta(last, current);

    const paths: [number,number][][] = [];
    let curPath: [number,number][] = [];
    let lastEdge!: [number,number];
    /**
     * The logic walks between the hexes in order looking at the angle between them.  Since we are drawing the outer
     * border, then there are 4 scenarios
     *  - the route bends backwards 60 degrees (draw 1 line)
     *  - the route goes straight ahead (draw 2 lines)
     *  - the route bends clockwise 60 degrees (draw 3 lines)
     *  - the route bends clockwise 120 degrees (4 lines)
     *  - the route goes back on itself (5 lines)
     *  Note that the case where we go backwards 120 degress cannot happen because we would then have gone straight
     *  from the previous hex to the next hex and skipped this one.
     *
     *  TO do this we work out a direction based around the coordinate difference (0=>6) and the derive the
     *  angle (based around the difference in directions.  We use the current direction to determine which hex
     *  point to start drawing lines from, and then go around the hex clockwise using the HEX_OFFSET array to determine
     *  for a given direction what is the offset for the point we should draw.
     */
    for(const next of hexes) {
        const nextStep = delta(current, next);
        const edge = deltaToEdgeNumber(lastStep);
        const nextEdge = deltaToEdgeNumber(nextStep);
        if(edge >= 0 && nextEdge >= 0) {
            const edgeCnt = edgeCount(lastStep, nextStep);
            if (!shouldSkip(current)) {
                const newEdge = edgeCoords(current, edge, lineWidth, -1);
                if(lastEdge === undefined || lastEdge[0] !== newEdge[0] || lastEdge[1] !== newEdge[1]) {
                    curPath = [newEdge];
                    paths.push(curPath);
                }
                for (let idx = 0; idx < edgeCnt; ++idx) {
                    lastEdge = edgeCoords(current, (edge + idx + 1) % 6, lineWidth, idx === edgeCnt-1 ? 1 : 0);
                    curPath.push(lastEdge);
                }
            } else {
                paths.push([edgeCoords(current, undefined, undefined)])
            }
            lastStep = nextStep;
            last = current;
        }
        current = next;
    }
    return paths;
}



function defaultSkipCoord(coord: [number, number]): boolean {
    if(coord[0] < 1 || coord[0]>Sector.SECTOR_WIDTH) {
        return true;
    }
    if(coord[1] < 2 || coord[1] > 2*Sector.SECTOR_HEIGHT+1) {
        return true;
    }
    return false;
}

const EDGES = [
    [-1, -1],
    [0, -2],
    [1, -1],
    [1, 1],
    [0, 2],
    [-1, 1],
]

const END_OFFSETS = [
    [ -0.25, 0.5 ],
    [ -Math.sqrt(2)/2, 0 ],
    [ -0.25, -0.5 ],
    [ 0.25, -0.5 ],
    [ Math.sqrt(2)/2, 0 ],
    [ 0.25, 0.5 ],
];

const HEX_OFFSETS = [
    [+HEX_SIDE_LENGTH / 2 / HEX_X_SCALE, 0.5],
    [-HEX_SIDE_LENGTH / 2 / HEX_X_SCALE, 0.5],
    [-(HEX_SIDE_EXTRA + HEX_SIDE_LENGTH / 2) / HEX_X_SCALE, 0],
    [-HEX_SIDE_LENGTH / 2 / HEX_X_SCALE, -0.5],
    [HEX_SIDE_LENGTH / 2 / HEX_X_SCALE, -0.5],
    [(HEX_SIDE_LENGTH / 2 + HEX_SIDE_EXTRA) / HEX_X_SCALE, 0],
];
function edgeCoords(coord: [number, number], idx?: number, lineWidth?: number, startEnd = 0): [number,number] {
    const lineCenter = (lineWidth ?? 0);
    const linePositionXMultiplier = (1 - lineCenter*Sector.SECTOR_WIDTH/2/0.5);
    const linePositionYMultiplier = (1 - lineCenter*Sector.SECTOR_HEIGHT/2/0.5);

    let hox = 0;
    let hoy = 0;
    if(idx !== undefined) {
        hox = HEX_OFFSETS[idx][0] * linePositionXMultiplier;
        hoy = HEX_OFFSETS[idx][1] * linePositionYMultiplier;

        if (startEnd === 1) {
            hox += Sector.SECTOR_WIDTH * lineCenter * END_OFFSETS[idx][0];
            hoy += Sector.SECTOR_HEIGHT * lineCenter * END_OFFSETS[idx][1];
        } else if(startEnd === -1) {
            hox += -Sector.SECTOR_WIDTH * lineCenter * END_OFFSETS[(idx + 1) % 6][0];
            hoy += -Sector.SECTOR_HEIGHT * lineCenter * END_OFFSETS[(idx + 1) % 6][1];
        }
    }

    const centerx = ((coord[0]-0.5 + hox) / Sector.SECTOR_WIDTH);
    const centery = (((coord[1]/2)-0.5 + hoy) / Sector.SECTOR_HEIGHT);
    return [centerx, centery];
}


function edgeCount(last: [number,number], next: [number,number]) : number {
    const val = angle(next, last);

    return val + 2;
}

// Returns
//   Path [0101, 0200, 0300] = [1,-1], [1,-1] => 0           last[0]-next[0], last[1]-next[1] == [0,0]
//        [0101, 0200, 0301] = [1,-1], [1,1]  => 1           only change in X or Y
//        [0101, 0200, 0201] = [1,1], [0,2]   => 2           change in both X and Y
//        [0101, 0200, 0101] = [1,1], [-1,-1] => 3           invert coords
function angle(last: [number,number], next: [number,number]) : number {
    const val = (deltaToEdgeNumber(last) - deltaToEdgeNumber(next)) % 6;
    if(val <= -3) {
        return val + 6;
    } else if(val > 3) {
        return val - 6;
    } else {
        return val;
    }
}

function deltaToEdgeNumber(from: [number, number]): number {
    for(let idx = 0; idx < EDGES.length; ++idx) {
        if(EDGES[idx][0] === from[0] && EDGES[idx][1] === from[1]) {
            return idx;
        }
    }
    return -1;
}

function delta(from: [number, number], to: [number, number]): [number, number] {
    return [to[0]-from[0], to[1]-from[1]];
}

function toCoords(parsed: [number, number]): [number, number] {
    return [parsed[0], 2 * parsed[1] + ((Math.abs(parsed[0])+1) % 2)]
}

function fromCoords(coord: [number, number]): [number, number] {
    return [coord[0], Math.trunc((coord[1] - ((Math.abs(coord[0])+1) % 2))/2)];
}

export function parseHex(hex: string) : [number, number] {
    hex = hex?.toString() ?? '0';
    while(hex.length < 4) {
        hex = '0'+hex;
    }
    return [Number.parseInt(hex.substring(0,2)), Number.parseInt(hex.substring(2,4))];
}


export function toCoordsWorld(parsed: [number, number]): [number, number] {
    return [parsed[0], 2 * parsed[1] - ((Math.abs(parsed[0])+1) % 2)]
}

export function fromCoordsWorld(coord: [number, number]): [number, number] {
    return [coord[0], Math.trunc((coord[1] + ((Math.abs(coord[0])+1) % 2))/2)];
}


export function hexRadius(center: [number,number], radius: number, fill: boolean = false): [number,number][] {
    const rv: [number,number][] = [];
    if(radius === 0) {
        return [center];
    }

    const coords = toCoordsWorld([center[0], center[1]-radius]);

    for(let dir = 0; dir < 6; ++dir) {
        for(let i = 0; i < radius; ++i) {
            coords[0] += EDGES[(dir + 3) % 6][0];
            coords[1] += EDGES[(dir + 3) % 6][1];
            rv.push(fromCoordsWorld(coords));
        }
    }

    if(fill) {
        rv.push(...hexRadius(center, radius-1, fill));
    }
    return rv;
}
