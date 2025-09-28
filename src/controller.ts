import {WebServer} from "./webServer.js";
import {TileRender} from "./render/tileRender.js";
import {hexRadius} from "./render/border.js";
import {worldBfs} from "./universe/bfs.js";
import {World} from "./universe/world.js";
import fs from "node:fs";
import path from "node:path";
import {absCoordinate, combinePartials, requestUniverse} from "./util.js";


export function addListeners(server: WebServer, tileRenderer: TileRender) {

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

    server.registerRaw('/api/poster', 'post', async (req) => {
        const universe = await requestUniverse(req);
        const query = combinePartials(req.query, req.body);
        const content = tileRenderer.renderSector(universe, query, req.contentType??'');

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
        let worldList;
        if(coords.hsuffix) {
            const sector = universe.getSector(coords.sx, coords.sy);
            worldList = [sector?.lookupWorld(World.coordsToHex([coords.hx, coords.hy, coords.hsuffix]))];
        } else {
            worldList = hexRadius([coords.x, coords.y], jump, true)
                .map(w => universe.lookupWorld(w[0], w[1]));
        }
        const worlds = worldList.map(w => w?.jumpWorld()).filter(jw => !!jw);
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

    server.registerJson('/api/universe', 'get', async(req) => {
        const universe = await requestUniverse(req);
        return {
            Sectors: universe.sectors().map(sector => ({
                X: sector.x,
                Y: sector.y,
                Milieu: sector.milieu,
                Abbreviation: sector.abbreviation,
                Names: [{ Text: sector.name}],
                Tags: sector.tags,
            }))
        };
    })

    server.staticRoute('/', path.join(process.cwd(), 'static'));

    server.restartRoute('/_restart_');


}