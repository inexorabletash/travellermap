import {WebServer, WireRequest} from "./webServer.js";
import path from "node:path";
import {TileRender} from "./render/tileRender.js";
import {Universe} from "./universe.js";
import fs from "node:fs";
import {Sector} from "./universe/sector.js";
import {World} from "./universe/world.js";
import {hexRadius} from "./render/border.js";
import {worldBfs} from "./universe/bfs.js";
import {inspect} from "node:util";
import {FilePoller} from "./filePoller.js";
import logger from './logger.js';

const workers = Number.parseInt(process.env['WORKERS'] ?? '8');
const port = Number.parseInt(process.env['PORT'] ?? '8000');
const server = new WebServer(() => logger.info(`Server started on port ${port} with ${workers} workers`), workers);
const tileRenderer = new TileRender();


process.on('SIGINT', () => {
    console.warn('Received SIGINT - shutting down');
    process.exit(0);
});

process.on('SIGTERM', () => {
    console.warn('Received SIGINT - shutting down');
    process.exit(0);
});


function requestUniverse(req: WireRequest): Promise<Universe> {
    const milieu = req.query.milieu;
    return Universe.getUniverse(milieu?.toString());
}

function absCoordinate(universe: Universe, query: Record<string,string>) {
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


server.registerRaw('/api/tile', 'get', async (req) => {
    const universe = await requestUniverse(req);
    const content = tileRenderer.renderTile(universe, req.query, req.contentType);

    return {
        contentType: req.contentType,
        statusCode: 200,
        body: content,
    };
}, ['image/png', 'image/svg+xml', 'application/pdf']);

server.registerRaw('/api/poster', 'get', async (req) => {
    const universe = await requestUniverse(req);
    const content = tileRenderer.renderSector(universe, req.query, req.contentType??'');

    return {
        contentType: req.contentType,
        statusCode: 200,
        body: content,
    };
}, ['image/png', 'image/svg+xml', 'application/pdf']);

server.registerRaw('/api/jumpmap', 'get', async (req) => {
    const universe = await requestUniverse(req);
    const coords = absCoordinate(universe, req.query);
    req.query.x = coords.x;
    req.query.y = coords.y;
    const content = tileRenderer.renderJump(universe, req.query, req.contentType??'');

    return {
        contentType: req.contentType,
        statusCode: 200,
        body: content,
    };
}, ['image/png', 'image/svg+xml', 'application/pdf']);

server.registerJson('/api/credits', 'get', async (req) => {
    const universe = await requestUniverse(req);
    const coords = absCoordinate(universe, req.query);
    const world = universe.lookupWorld(coords.x, coords.y);

    return world?.credits();
});

server.registerJson('/api/jumpworlds', 'get', async (req) => {
    const jump = TileRender.parseNumeric(req.query.jump, 0);
    const universe = await requestUniverse(req);
    const coords = absCoordinate(universe, req.query);
    const worldList = hexRadius([coords.x, coords.y], jump, true);
    const worlds = worldList.map(w => universe.lookupWorld(w[0], w[1])?.jumpWorld()).filter(jw => !!jw);
    return {
        Worlds: worlds,
    };
});

server.registerJson('/api/route', 'get', async (req) => {
    const universe = await requestUniverse(req);
    const from = universe.lookupWorldByName(req.query.start);
    const to = universe.lookupWorldByName(req.query.end);
    const jump = TileRender.parseNumeric(req.query.jump, 2);
    const wild = TileRender.parseNumeric(req.query.wild, 0);
    const im = TileRender.parseNumeric(req.query.im, 0);
    const nored = TileRender.parseNumeric(req.query.nored, 0);
    const nozone = TileRender.parseNumeric(req.query.nozone, 0);
    const aok = TileRender.parseNumeric(req.query.aok, 0);
    if(!from || !to) {
        return [];
    }
    const worlds = worldBfs(universe, from, to, jump,
        w => {
            const atmos = World.digitValue(w.uwp.substring(2, 3)) ?? 0;
            const hydro = World.digitValue(w.uwp.substring(3, 4)) ?? 0;
            const gg = World.digitValue(w.pbg?.substring(2)) ?? 0;
            const belts = World.digitValue(w.pbg?.substring(1)) ?? 0;
            if(wild && !(
                (gg > 0) ||
                (hydro > 0 && (wild>1 || (atmos > 1 && atmos < 10))) ||
                (wild > 1 && belts > 0)
            )) {
                return false;
            }
            if(im && !(w.allegiance.startsWith('Im') || w.allegiance == 'CsIm')) {
                return false;
            }
            if(nored && w.zone === 'R') {
                return false;
            }
            if(nozone && w.zone !== '') {
                return false;
            }
            if(!aok && w.notes.has('Anomaly')) {
                return false;
            }
            return true;
        })
    return worlds?.map(w => w.jumpWorld()) ?? [];
})

server.registerJson('/api/coordinates', 'get', async (req) => {
    const universe = await requestUniverse(req);
    const coords = absCoordinate(universe, req.query);
    return coords;
});

server.registerJson('/api/search', 'get', async (req) => {
    const universe = await requestUniverse(req);
    const q = req.query.q?.toString() ?? '(default)'
    if(q === '(default)') {
        return JSON.parse(await fs.promises.readFile(path.join(process.cwd(),'static','res','search','Default.json'), 'utf8'));
    }
    return { Results: universe.search(q) };
});



server.staticRoute('/', path.join(process.cwd(), 'static'));

server.restartRoute('/_restart_');

server.start(port, async () => {
    if(server.isMaster()) {
        new FilePoller(Universe.OVERRIDE_DIR,
        //fs.watch(Universe.OVERRIDE_DIR, {encoding: 'utf8', recursive: true, },
            (change, file) => {
                const bn = path.basename(file);
                if(file && path.extname(file) === '.yml' && !bn.startsWith('.') && !bn.startsWith('#')) {
                    server.restartWorkers();
                }
            });
    } else {
        await Universe.getUniverse(undefined);
    }
});
