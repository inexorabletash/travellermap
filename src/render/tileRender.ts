import {Canvas, CanvasRenderingContext2D, createCanvas, DOMPoint} from "canvas";
import {Universe} from "../universe.js";
import {Sector} from "../universe/sector.js";
import {PosterRenderer} from "./posterRenderer.js";
import {HEX_X_SCALE} from "./constants.js";
import {borderPath, hexRadius, toCoordsWorld} from "./border.js";


export class TileRender {
    static readonly DEFAULT_TILE_SIZE_PIXELS = 256;
    static readonly DEFAULT_SCALE_PIXELS = 128;
    static readonly DEFAULT_OPTIONS = 9207;
    static readonly DEFAULT_STYLE = 'poster';

    static readonly SCALE_SECTOR = 24;
    static readonly SCALE_FINE = 120;

    static readonly RenderSectorGrid=0x0001;       // 1 	Show sector grid.
	static readonly RenderSubsectorGrid=0x0002;    // 2 	Show subsector grid.
	static readonly RenderSubSectors=0x0004;       // 4 	At low scales, show only some sector names
	static readonly RenderSectorsAll=0x0008;       // 8 	Show all sector names
	static readonly RenderBordersMajor=0x0010;     // 16 	Show major region borders.
	static readonly RenderBordersMinor=0x0020;     // 32 	Show minor region borders.
	static readonly RenderNamesMajor=0x0040;       // 64 	Show major region names.
	static readonly RenderNamesMinor=0x0080;       // 128 	Show minor region names.
	static readonly RenderWorldsCapitals=0x0100;   // 256 	Show important worlds (capitals).
	static readonly RenderWorldsHomeworlds=0x0200; // 512 	Show important worlds (homeworlds).
	static readonly RenderForceHexes=0x2000;       // 8192 	Never render square hexes.
	static readonly RenderWorldColors=0x4000;      // 16384 	Use additional world colors for Atm/Hyd.
	static readonly RenderFilledBorders=0x8000;    // 32768 	Background colors for bordered regions.

    static readonly XRenderRoutes = 0x100000;       // Render routes  (passed in as routes=0/1)


    static parseNumeric(v: any, deflt: number): number {
        if(v === undefined || v === null) {
            return deflt;
        }
        const value = Number.parseFloat(v.toString());
        if(Number.isFinite(value)) {
            return value;
        }
        return deflt;
    }

    sanifyCoords(x: number, y: number, scale: number, baseTileWidth: number, baseTileHeight: number): [number, number] {
        const xScale = baseTileWidth / scale / HEX_X_SCALE;
        const yScale = baseTileHeight / scale;

        // In the tile APIs, x and y are in tile coords and not absolute coords so you have to normalize by
        // the number of tiles
        return [(x * xScale) + 1, (y * yScale)]; //  + Sector.SECTOR_HEIGHT
    }

    defaultRenderOpts(query: Record<string, any>, contentType: string) {
        const opts: Record<string,any> = {};

        opts.scale = TileRender.parseNumeric(query.scale, TileRender.DEFAULT_SCALE_PIXELS);
        opts.options = TileRender.parseNumeric(query.options, TileRender.DEFAULT_OPTIONS);
        opts.dpr = TileRender.parseNumeric(query.dpr, 1.5);
        opts.style = query.style ?? TileRender.DEFAULT_STYLE;
        opts.scaleFactor = opts.scale * opts.dpr;
        opts.canvasType = contentType === 'application/pdf' ? 'pdf' :
            contentType === 'image/svg+xml' ? 'svg' : undefined;

        if(query.routes === undefined || query.routes != 0) {
            opts.options |= TileRender.XRenderRoutes;
        }

        return opts;
    }

    renderTile(universe: Universe, query: Record<string, any>, contentType = 'image/png'): Buffer {
        // Note the width,height parameters don't work as you might expect in travellermap, since they
        // seem to actually scale the coordinates by the ratio of width/256.  We're not goind to do that.
        const opts = this.defaultRenderOpts(query, contentType);

        opts.x = TileRender.parseNumeric(query.x, 0);
        opts.y = TileRender.parseNumeric(query.y, 0);

        const baseTileWidth = TileRender.parseNumeric(opts.w, TileRender.DEFAULT_TILE_SIZE_PIXELS);
        const baseTileHeight = TileRender.parseNumeric(opts.h, TileRender.DEFAULT_TILE_SIZE_PIXELS);
        const tileWidth = baseTileWidth * opts.dpr;
        const tileHeight = baseTileHeight * opts.dpr;
        const width = tileWidth / opts.scaleFactor / HEX_X_SCALE;
        const height = tileHeight / opts.scaleFactor;
        const [x, y] = this.sanifyCoords(opts.x, opts.y, opts.scale, baseTileWidth, baseTileHeight); // Note this is the original x,y and pre-scaled to DPR scale value
        const canvas = createCanvas(tileWidth, tileHeight, opts.canvasType);
        const context = canvas.getContext("2d");
        this.draw(universe, context, x, y, width, height, opts.scaleFactor, opts.options, opts.style);
        return canvas.toBuffer(contentType as any);
    }

    renderSector(universe: Universe, query: Record<string, any>, contentType: string): Buffer {
        const sectorName = query.sector;
        const opts = this.defaultRenderOpts(query, contentType);
        let range!: [number,number,number,number];

        if(query.sector) {
            const sector = universe.getSectorByName(sectorName);
            if(sector) {
                range = sector.subsectorCodeToRange(query.subsector ?? query.quadrant);
                const absCoords = Universe.universeCoords({sx: sector.x, sy: sector.y, hx: range[0], hy: range[1]});
                range[0] = absCoords[0];
                range[1] = absCoords[1];
            }

        }
        if(range === undefined) {
            range = [
                TileRender.parseNumeric(query.x,0), TileRender.parseNumeric(query.y, 0),
                TileRender.parseNumeric(query.width, Sector.SECTOR_WIDTH),
                TileRender.parseNumeric(query.height, Sector.SECTOR_HEIGHT)
            ];
        }
        --range[1]; // Kludge me
        //range[2] *= HEX_X_SCALE;
        const canvas = createCanvas(range[2]*opts.scaleFactor*HEX_X_SCALE, range[3]*opts.scaleFactor, opts.canvasType);
        const context = canvas.getContext("2d");
        this.draw(universe, context, ...range, opts.scaleFactor, opts.options, opts.style);
        return canvas.toBuffer(contentType as any);
    }

    renderJump(universe: Universe, query: Record<string, any>, contentType: string): Buffer {
        const opts = this.defaultRenderOpts(query, contentType);
        const midx = query.x;
        const midy = query.y;
        const jump = TileRender.parseNumeric(query.jump,6);

        const x = midx - jump - 0.5;
        const y = midy - jump - 1 + ((midx+1) % 2)/2;
        const dims = jump*2+1 + 1;

        const canvas = createCanvas(dims*opts.scaleFactor*HEX_X_SCALE, dims*opts.scaleFactor, opts.canvasType);
        const context = canvas.getContext("2d");
        this.draw(universe, context, x, y, dims, dims, opts.scaleFactor, opts.options, opts.style);
        this.drawClearBorder(context, x,y, opts.scaleFactor, hexRadius([midx,midy], jump, false));

        return canvas.toBuffer(contentType as any);
    }


    protected draw(universe: Universe, ctx: CanvasRenderingContext2D, startX: number, startY: number, width: number, height: number, scale: number, options: number, style: string) {
        const renderer = new PosterRenderer();
        const baseX = Math.floor(startX);
        const baseY = Math.floor(startY);

        const endX = Math.ceil(startX + width) - baseX;
        const endY = Math.ceil(startY + height) - baseY;

        // Planets are placed at .5 offsets of X
        // Planets are placed at .5 offset of Y if X is event or 0 offset if Y is odd.

        // Sector-level rendering
        const secx = Math.floor((baseX-1) / Sector.SECTOR_WIDTH);
        const secy = Math.floor((baseY-1) / Sector.SECTOR_HEIGHT)+1;
        const secMaxX = Math.floor((baseX-1 + endX) / Sector.SECTOR_WIDTH);
        const secMaxY = Math.floor((baseY-1 + endY) / Sector.SECTOR_HEIGHT)+1;
        for(let sx = secx; sx <= secMaxX; ++sx) {
            for(let sy = secy; sy <= secMaxY; ++sy) {
                // 0,0 should be the middle of the sector
                ctx.setTransform(scale * HEX_X_SCALE * Sector.SECTOR_WIDTH, 0, 0, scale * Sector.SECTOR_HEIGHT,
                    HEX_X_SCALE * scale * (-startX + (sx+0.5) * Sector.SECTOR_WIDTH),
                    scale * (-startY + (sy-0.5) * Sector.SECTOR_HEIGHT));
                renderer.renderSectorBorder(ctx, sx, sy, scale, options);
                const sector = universe.getSector(sx, sy);

                ctx.globalCompositeOperation = 'screen';
                if(sector !== undefined) {
                    renderer.renderSectorName(ctx, sector, sx, sy, scale, options);
                    renderer.renderRoutes(ctx, sector, sx, sy, scale, options);
                    renderer.renderBorders(ctx, sector, sx, sy, scale, options);
                }
            }
        }

        // World-level rendering
        const xOff = baseX - startX;
        const yOff = baseY - startY;
        for (let x = 0; x <= endX; x += 1) {
            for (let y = 0; y <= endY; y += 1) {
                const w = universe.lookupWorld(x + baseX, y + baseY)

                //ctx.setTransform(scale * HEX_X_SCALE, 0, 0, scale, HEX_X_SCALE * scale * (xOff+x+0.5), scale * (yOff + y - ((Math.abs(baseX+x) % 2)/2)));
                ctx.setTransform(scale * HEX_X_SCALE, 0, 0, scale, HEX_X_SCALE * scale * (xOff+x+0.5), scale * (yOff + y - ((Math.abs(baseX+x+1) % 2)/2)));
                renderer.renderHexBorder(ctx, w, x + baseX, y + baseY, scale, options);

                if(w === undefined) {
                    continue;
                }
                ctx.globalCompositeOperation = 'screen';
                renderer.renderWorld(ctx, w, scale, options)
                renderer.renderZone(ctx, w, scale, options);
                renderer.renderHexName(ctx, w, scale, options);
                renderer.renderWorldUpp(ctx, w, scale, options);
                renderer.renderIcons(ctx, w, scale, options);
                renderer.renderWorldName(ctx, w, scale, options);
            }
        }


        // backgroud rendering
        ctx.setTransform(scale * HEX_X_SCALE, 0, 0, scale, 0, 0);
        renderer.setBackground(ctx, width * HEX_X_SCALE, height, scale, options);



    }

    private drawClearBorder(ctx: CanvasRenderingContext2D, x: number, y: number, scale: number, hexes: [number,number][]) {
        ctx.setTransform(scale * HEX_X_SCALE * Sector.SECTOR_WIDTH, 0, 0, scale * Sector.SECTOR_HEIGHT,
            scale * HEX_X_SCALE * (- x + 1),
            scale * (- y + 0.5 ));

        const pathHexes = hexes.map(hex => toCoordsWorld(hex));

        ctx.globalCompositeOperation = 'copy';
        const color = '#333333';
        ctx.strokeStyle = color;
        ctx.fillStyle = color;
        ctx.globalAlpha = 1;

        const fillPaths0 = borderPath(pathHexes.slice(0, pathHexes.length / 2 + 2), 0, ()=>false);
        const fillPaths1 = borderPath([...pathHexes.slice(pathHexes.length / 2), ...pathHexes.slice(0, 2)], 0, ()=>false);
        let path = fillPaths0[0];
        ctx.beginPath();
        ctx.moveTo(...path[0]);
        for (const comp of path.slice(1)) {
            ctx.lineTo(...comp);
        }
        let end = path[path.length-1];
        ctx.lineTo(end[0],end[1]+Sector.SECTOR_HEIGHT);
        ctx.lineTo(end[0]+Sector.SECTOR_WIDTH,end[1]+Sector.SECTOR_HEIGHT);
        ctx.lineTo(end[0]+Sector.SECTOR_WIDTH,path[0][1]-Sector.SECTOR_HEIGHT);
        ctx.lineTo(path[0][0],path[0][1]-Sector.SECTOR_HEIGHT);
        ctx.closePath();
        ctx.fill();

        path = fillPaths1[0];
        ctx.beginPath();
        ctx.moveTo(...path[0]);
        for (const comp of path.slice(1)) {
            ctx.lineTo(...comp);
        }
        end = path[path.length-2];
        ctx.lineTo(end[0],end[1]-Sector.SECTOR_HEIGHT);
        ctx.lineTo(end[0]-Sector.SECTOR_WIDTH,end[1]-Sector.SECTOR_HEIGHT);
        ctx.lineTo(end[0]-Sector.SECTOR_WIDTH,path[0][1]+Sector.SECTOR_HEIGHT);
        ctx.lineTo(path[0][0],path[0][1]+Sector.SECTOR_HEIGHT);
        ctx.closePath();
        ctx.fill();

        ctx.globalAlpha = 0;
        pathHexes.push(pathHexes[0]);
        const paths = borderPath(pathHexes, 0, () => false);

        const scaleFactor = Math.max(32 / scale, 1);
        ctx.lineWidth = 0.1 / Sector.SECTOR_HEIGHT * scaleFactor;
        const borderColor = '#cccccc';
        ctx.strokeStyle = borderColor;
        ctx.fillStyle = borderColor;
        ctx.globalCompositeOperation = 'copy';
        for (const path of paths) {
            ctx.beginPath();
            ctx.moveTo(...path[0]);
            for (const comp of path.slice(1)) {
                ctx.lineTo(...comp);
            }
            ctx.stroke();
        }
    }


}


