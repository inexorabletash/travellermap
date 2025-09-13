import {WireRequest} from "./webServer.js";
import {Universe} from "./universe.js";
import {TileRender} from "./render/tileRender.js";
import {Sector} from "./universe/sector.js";
import {World} from "./universe/world.js";

export function combinePartials<T>(base: T, ...newValue: (Partial<T>|undefined|null)[]): T {
    return Object.fromEntries([...Object.entries((base ?? {}) as any),
        ...newValue.flatMap(v => Object.entries(v ?? {}))]
        .filter(v => v[1] !== undefined)) as T;
}

export function requestUniverse(req: WireRequest): Promise<Universe> {
    const milieu = req.query.milieu;
    return Universe.getUniverse(milieu?.toString());
}

export function absCoordinate(universe: Universe, query: Record<string,string>) {
    let x: number;
    let y: number;
    let sx: number;
    let sy: number;
    let hx: number;
    let hy: number;
    let sector: string;
    let subsector: string;
    if (query.x !== undefined || query.y !== undefined || query.world !== undefined) {
        if(query.world !== undefined) {
            const world = universe.lookupWorldByName(query.world);
            if(world) {
                [x,y] = world.globalCoords;
            } else {
                x = 0;
                y = 0;
            }
        } else {
            x = TileRender.parseNumeric(query.x, 0);
            y = TileRender.parseNumeric(query.y, 0);
        }

        const translated = universe.translateCoords(x, y);
        return {
            x,
            y,
            sx: translated.sx,
            sy: translated.sy,
            hx: translated.hx,
            hy: translated.hy,
        };
    }
    let sectorDef: Sector|undefined;
    if (query.sector !== undefined) {
        sectorDef = universe.getSectorByName(query.sector);
    } else if(query.subsector !== undefined) {
        sectorDef = universe.getSectorBySubsectorName(query.subsector);
    }
    if(sectorDef !== undefined) {
        sx = sectorDef.x;
        sy = sectorDef.y;
    } else {
        sx = TileRender.parseNumeric(query.sx,0);
        sy = TileRender.parseNumeric(query.sx,0);
    }
    if(query.hex === undefined) {
        hx = TileRender.parseNumeric(query.hx,0);
        hy = TileRender.parseNumeric(query.hy,0);
    } else {
        [hx, hy] = World.hexToCoords(query.hex);
    }

    const mapped = Universe.universeCoords({sx,sy,hx,hy})
    x = mapped[0];
    y = mapped[1];
    return { x,y, sx, sy, hx, hy };
}

