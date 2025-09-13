//const Glyphs: Record<s>
import {World} from "../universe/world.js";
import {Canvas, CanvasRenderingContext2D, createCanvas} from "canvas";
import {Sector} from "../universe/sector.js";
import {Glyph} from "./glyph.js";
import {TileRender} from "./tileRender.js";
import {borderPath, parseHex, renderBorder, toCoordsWorld} from "./border.js";
import {HEX_SIDE_EXTRA, HEX_SIDE_LENGTH, HEX_X_SCALE} from "./constants.js";
import {inspect} from "node:util";
import {Universe} from "../universe.js";

export abstract class Renderer {
    abstract worldColor(w: World, options: number): { line: typeof CanvasRenderingContext2D.prototype.fillStyle, fill: typeof CanvasRenderingContext2D.prototype.fillStyle};

    abstract textColor(w: World | Sector, options: number, type: 'hex' | 'upp' | 'name' | 'sector' | 'subsector' | 'allegiance'): typeof CanvasRenderingContext2D.prototype.fillStyle;

    abstract lineColor(options: number, type: 'hex' | 'sector' | 'subsector' | 'border' | 'route' | 'zone-R' | 'zone-A' | 'zone-U'): typeof CanvasRenderingContext2D.prototype.strokeStyle;

    abstract iconColor(w: World | Sector, options: number, glyph: Glyph): typeof CanvasRenderingContext2D.prototype.fillStyle;

    abstract font(w: World | Sector, options: number, type: string, data?: any): typeof CanvasRenderingContext2D.prototype.font;

    createCanvas(width: number, height: number, type?: 'pdf'|'svg'): Canvas {
        return createCanvas(width, height, type);
    }

    setBackground(context: CanvasRenderingContext2D, width: number, height: number, scale: number, options: number) {
        context.fillStyle = '#000';
        context.globalCompositeOperation = 'destination-over';
        context.fillRect(0, 0, width / HEX_X_SCALE, height);

        this.setupContextDefaults(context);
    }

    renderSectorName(ctx: CanvasRenderingContext2D, s: Sector, x: number, y: number, scale: number, options: number) {
        // Render at the subsector level
        if (scale > TileRender.SCALE_SECTOR) {
            if(options & TileRender.RenderSubSectors) {
                ctx.font = this.font(s, options, 'subsector');
                ctx.fillStyle = this.textColor(s, options, 'subsector');
                for (let dx = 0; dx < 4; ++dx) {
                    for (let dy = 0; dy < 4; ++dy) {
                        const name = s.subsectorName(dx, dy);
                        if (name === undefined) {
                            continue;
                        }

                        this.centerWords(ctx, name, -0.375 + dx * 0.25, -0.375 + dy * 0.25, false, -Math.PI / 4);
                    }
                }
            }
        } else {
            if(options & TileRender.RenderSectorsAll) {
                ctx.font = this.font(s, options, 'sector');
                ctx.fillStyle = this.textColor(s, options, 'sector');
                this.centerWords(ctx, s.name, 0, 0, false, -Math.PI / 4);
            }
        }
    }

    renderRoutes(ctx: CanvasRenderingContext2D, s: Sector, x: number, y: number, scale: number, options: number) {
        if(!(options & TileRender.XRenderRoutes)) {
            return;
        }

        const lco = ctx.lineCap;
        ctx.lineCap = 'round';
        ctx.lineWidth = 0.003;
        ctx.globalCompositeOperation = 'copy';

        for(const route of s.routes) {
            const color = route.color ?? s.allegiances?.[route.allegiance ?? '']?.routeColor ?? this.lineColor(options, 'route');
            const start = World.hexToCoords(route.start);
            const end = World.hexToCoords(route.end);
            if(route.endOffsetX) {
                end[0] += route.endOffsetX * Sector.SECTOR_WIDTH;
            }
            if(route.endOffsetY) {
                end[1] += route.endOffsetY * Sector.SECTOR_HEIGHT;
            }
            if(route.startOffsetX) {
                start[0] += route.startOffsetX * Sector.SECTOR_WIDTH;
            }
            if(route.startOffsetY) {
                start[1] += route.startOffsetY * Sector.SECTOR_HEIGHT;
            }
            ctx.strokeStyle = color;
            const worldStart = this.sectorCoordScale(start);
            const worldEnd = this.sectorCoordScale(end);
            const delta = [(worldStart[0] - worldEnd[0])*HEX_X_SCALE, (worldStart[1] - worldEnd[1])];
            const hyp = Math.sqrt(delta[0]*delta[0] + delta[1]*delta[1]);
            const offset = [delta[0]/hyp * 0.3/Sector.SECTOR_WIDTH, delta[1]/hyp * 0.3/Sector.SECTOR_HEIGHT];
            const actStart = [worldStart[0] - offset[0], worldStart[1] - offset[1]];
            const actEnd = [worldEnd[0] + offset[0], worldEnd[1] + offset[1]];
            ctx.beginPath();
            ctx.moveTo(actStart[0], actStart[1]);
            ctx.lineTo(actEnd[0], actEnd[1]);
            ctx.stroke();
        }
        ctx.lineCap = lco;

        this.setupContextDefaults(ctx);

    }

    sectorCoordScale(coord: [number,number]): [number, number] {
        const centerx = ((coord[0]-0.5) / Sector.SECTOR_WIDTH) - 0.5;
        const centery = ((coord[1]-(coord[0]%2)/2) / Sector.SECTOR_HEIGHT) - 0.5;

        return [centerx, centery];
    }

    renderBorders(ctx: CanvasRenderingContext2D, s: Sector, x: number, y: number, scale: number, options: number) {
        const scaleFactor = Math.max(32/scale,1);

        for(const border of s.borders) {
            const color = s.allegiances?.[border.allegiance ?? '']?.borderColor ?? border.color ?? this.lineColor(options, 'border');
            if (options & TileRender.RenderNamesMajor) {
                if (border.label && border.labelPosition) {
                    ctx.fillStyle = color
                    ctx.font = this.font(s, options, 'region');
                    const coords = this.sectorCoordScale(parseHex(border.labelPosition));
                    if (border.wrapLabel) {
                        this.centerWords(ctx, border.label, coords[0], coords[1], false, 0);
                    } else {
                        this.centerText(ctx, border.label, coords[0], coords[1], false, 0);
                    }
                }
            }
            ctx.lineWidth = 0.03 / Sector.SECTOR_HEIGHT * scaleFactor;
            //ctx.lineWidth = 0.001 / Sector.SECTOR_HEIGHT * scaleFactor;
            if ((options & (TileRender.RenderBordersMajor | TileRender.RenderBordersMinor))) {
                renderBorder(ctx, s, border.hexes, color, (options & TileRender.RenderFilledBorders) ? color: undefined);
            }
        }
    }


    renderSectorBorder(ctx: CanvasRenderingContext2D, x: number, y: number, scale: number, options: number) {
        if(options & TileRender.RenderSectorGrid) {
            ctx.globalCompositeOperation = 'copy';
            ctx.strokeStyle = this.lineColor(options, 'sector');
            ctx.lineWidth = (0.03 / Sector.SECTOR_HEIGHT);
            ctx.beginPath();
            ctx.moveTo(-0.5, -0.5);
            ctx.lineTo(-0.5, 0.5);
            ctx.lineTo(0.5, 0.5);
            ctx.lineTo(0.5, -0.5);
            ctx.lineTo(-0.5, -0.5);
            ctx.stroke();

            if (options & TileRender.RenderSubsectorGrid) {
                ctx.strokeStyle = this.lineColor(options, 'sector');
                ctx.lineWidth = (0.01 / Sector.SECTOR_HEIGHT);
                for (let coord = -0.25; coord <= 0.25; coord += 0.25) {
                    ctx.beginPath();
                    ctx.moveTo(-0.5, coord);
                    ctx.lineTo(0.5, coord);
                    ctx.stroke();

                    ctx.beginPath();
                    ctx.moveTo(coord, -0.5);
                    ctx.lineTo(coord, 0.5);
                    ctx.stroke();
                }
            }
        }

        this.setupContextDefaults(ctx);

    }


    renderWorld(ctx: CanvasRenderingContext2D, w: World, scale: number, options: number) {
        const styles = this.worldColor(w, options);
        ctx.fillStyle = styles.fill;
        ctx.strokeStyle = styles.line;
        ctx.lineWidth = 0.01;
        ctx.globalCompositeOperation = 'copy';


        // Special case for asteroid belt
        if(w.uwp.charAt(1) === '0') {
            [
                [0.09,-0.1,0.03],
                [-0.03,-0.08,0.02],
                [0.02,-0.03,0.01],
                [-0.02,-0.01,0.01],
                [-0.10,0,0.01],
                [-0.06,0.01,0.02],
                [0.05, 0, 0.01],
                [0.10, 0.01, 0.02],
                [0.02, 0.05, 0.02],
            ].forEach(([x,y,r]) => {
                ctx.beginPath();
                ctx.ellipse(x/HEX_X_SCALE, y, r / HEX_X_SCALE, r, 0, 0, 2 * Math.PI);
                ctx.stroke();
                ctx.fill();
            });
        } else {
            ctx.beginPath();
            ctx.ellipse(0, 0, 0.1 / HEX_X_SCALE, 0.1, 0, 0, 2 * Math.PI);
            ctx.stroke();
            ctx.fill();
        }

        this.setupContextDefaults(ctx);

    }

    renderHexBorder(ctx: CanvasRenderingContext2D, w: World | undefined, x: number, y: number, scale: number, options: number) {
        if (scale <= TileRender.SCALE_SECTOR) {
            return;
        }

        ctx.globalCompositeOperation = 'destination-over';

        ctx.lineWidth = 0.01;
        ctx.strokeStyle = this.lineColor(options, 'hex');
        ctx.beginPath();
        ctx.moveTo(-(HEX_SIDE_EXTRA + HEX_SIDE_LENGTH / 2) / HEX_X_SCALE, 0);
        [
            [-HEX_SIDE_LENGTH / 2 / HEX_X_SCALE, -0.5],
            [HEX_SIDE_LENGTH / 2 / HEX_X_SCALE, -0.5],
            [(HEX_SIDE_LENGTH / 2 + HEX_SIDE_EXTRA) / HEX_X_SCALE, 0],
            [+HEX_SIDE_LENGTH / 2 / HEX_X_SCALE, 0.5],
            [-HEX_SIDE_LENGTH / 2 / HEX_X_SCALE, 0.5],
            [-(HEX_SIDE_EXTRA + HEX_SIDE_LENGTH / 2) / HEX_X_SCALE, 0],
        ].map(([xc, yc]) => ctx.lineTo(xc, yc));
        ctx.stroke();

        this.setupContextDefaults(ctx);

    }

    renderHexName(ctx: CanvasRenderingContext2D, w: World, scale: number, options: number) {
        if (scale <= TileRender.SCALE_SECTOR) {
            return;
        }

        ctx.textAlign = 'left';
        ctx.textBaseline = 'top';
        const hex = w.hex;
        ctx.font = this.font(w, options, 'hex', hex);

        const size = ctx.measureText(hex);
        ctx.fillStyle = this.textColor(w, options, 'hex')
        ctx.fillText(hex, -size.width / 2, -0.45);
    }

    renderWorldUpp(ctx: CanvasRenderingContext2D, w: World, scale: number, options: number) {
        if (scale <= TileRender.SCALE_FINE) {
            return;
        }

        ctx.textAlign = 'left';
        ctx.textBaseline = 'bottom';
        const hex = w.uwp;
        ctx.font = this.font(w, options, 'upp', hex);

        const size = ctx.measureText(hex);

        ctx.fillStyle = this.textColor(w, options, 'upp')
        ctx.fillText(hex, -size.width / 2, 0.30);

    }

    renderIcons(ctx: CanvasRenderingContext2D, w: World, scale: number, options: number) {
        if (scale <= TileRender.SCALE_SECTOR) {
            return;
        }

        this.renderTextIcons(ctx, Glyph.fromStarport(w.uwp?.substring(0, 1)), w, scale, options, 0, 0.25);
        this.renderTextIcons(ctx, Glyph.fromBaseCode(w.allegiance_, w.bases), w, scale, options, -Math.PI / 3, 0.25, -Math.PI / 3);
        this.renderTextIcons(ctx, Glyph.fromNoteCode(w.notes), w, scale, options, -Math.PI / 2, 0.25);
        if (Number.parseInt(w.pbg.substring(2, 3) ?? '0') > 0) {
            const x = 0.3 * Math.sin(Math.PI / 3);
            const y = -0.3 * Math.cos(Math.PI / 3);

            const color = this.iconColor(w, options, Glyph.GasGiant);
            ctx.fillStyle = color;
            ctx.strokeStyle = color;
            ctx.lineWidth = 0.01;
            ctx.beginPath();
            ctx.ellipse(x, y, 0.03 / HEX_X_SCALE, 0.03, 0, 0, 2 * Math.PI);
            ctx.fill();

            ctx.beginPath();
            ctx.ellipse(x, y, 0.06 / HEX_X_SCALE, 0.01, -Math.PI / 6, 0, 2 * Math.PI);
            ctx.stroke();
        }
        const font = this.font(w, options, 'allegiance');
        this.renderTextAtRadialPosition(ctx, w.allegiance ?? '', 3 * Math.PI / 5, 0.3,
            this.textColor(w, options, 'allegiance'),
            font);
    }

    renderTextIcons(ctx: CanvasRenderingContext2D, icons: Glyph[], w: World, scale: number, options: number, theta: number, r: number, delta_theta: number = 0) {
        icons.forEach((glyph, index) => {
            const color = this.iconColor(w, options, glyph);
            const font = this.font(w, options, 'glyph', glyph);
            this.renderTextAtRadialPosition(ctx, glyph.code, theta + delta_theta * index, r, color, font);
        })

    }

    renderTextAtRadialPosition(ctx: CanvasRenderingContext2D, text: string, theta: number, r: number,
                               color: typeof CanvasRenderingContext2D.prototype.fillStyle,
                               font: typeof CanvasRenderingContext2D.prototype.font) {
        const x = r * Math.sin(theta);
        const y = -r * Math.cos(theta);
        ctx.fillStyle = color;
        ctx.strokeStyle = color;
        ctx.font = font;
        this.centerText(ctx, text, x, y, false, 0);
    }

    renderWorldName(ctx: CanvasRenderingContext2D, w: World, scale: number, options: number) {
        if (scale <= TileRender.SCALE_SECTOR) {
            return;
        }

        const popDigit = World.digitValue(w.uwp.substring(4, 5)) ?? 0
        ctx.font = this.font(w, options, 'name');
        ctx.textAlign = 'left';
        ctx.textBaseline = 'bottom';
        const hex = popDigit >= 9 ? w.name.toUpperCase() : w.name;
        const size = ctx.measureText(hex);
        ctx.globalCompositeOperation = 'copy';

        ctx.fillStyle = 'black';
        ctx.fillRect(-size.width/2, 0.50-(size.emHeightAscent + size.emHeightDescent), size.width,(size.emHeightAscent + size.emHeightDescent));

        ctx.fillStyle = this.textColor(w, options, 'name')
        ctx.fillText(hex, -size.width / 2, 0.50);

        this.setupContextDefaults(ctx);
    }

    renderZone(ctx: CanvasRenderingContext2D, w: World, scale: number, options: number) {
        if (scale <= TileRender.SCALE_SECTOR) {
            return;
        }

        const color = this.lineColor(options, `zone-${w.zone}` as any);
        if (color !== undefined) {
            ctx.strokeStyle = color;
            ctx.fillStyle = color;
            ctx.lineWidth = 0.03;

            ctx.beginPath();
            ctx.ellipse(0, 0, 0.4 / HEX_X_SCALE, 0.4, 0, 0, 2 * Math.PI);
            ctx.stroke();
        }

    }

    centerText(ctx: CanvasRenderingContext2D, text: string, xOff: number, yOff: number, isStroke: boolean, rotation: number) {
        // Assume font has been set
        ctx.textAlign = 'left';
        ctx.textBaseline = 'bottom';

        ctx.rotate(rotation);
        const size = ctx.measureText(text);
        const textx = -size.width / 2;
        const texty = (size.emHeightAscent + size.emHeightDescent) / 2 - size.emHeightDescent;
        const x = xOff + textx;
        const y = yOff + texty;
        if (isStroke) {
            ctx.strokeText(text, x, y);
        } else {
            ctx.fillText(text, x, y);
        }
        ctx.rotate(-rotation);
    }

    centerWords(ctx: CanvasRenderingContext2D, text: string, xOffIn: number, yOffIn: number, isStroke: boolean, rotation: number = 0) {
        if (text === undefined) {
            return;
        }
        ctx.textAlign = 'left';
        ctx.textBaseline = 'bottom';

        const sin = Math.sin(rotation);
        const cos = Math.cos(rotation);
        const xOff = xOffIn * cos + yOffIn * sin;
        const yOff = yOffIn * cos - xOffIn * sin;

        const words = text.trim().split(/\s+/);
        if (words.length === 0) {
            return;
        }
        const rowHeight = Math.max(...words.map(w => ctx.measureText(w)).map(rowsize => rowsize.emHeightAscent + rowsize.emHeightDescent));
        if (rowHeight === 0) {
            return;
        }
        const height = rowHeight * words.length;
        words.forEach((word, idx) => {
            const yposDelta = rowHeight / 2 + rowHeight * idx - height / 2;

            const drawx = xOff;
            const drawy = yOff + yposDelta;
            this.centerText(ctx, word, drawx, drawy, isStroke, rotation);
        })
    }

    setupHexTransform(ctx: CanvasRenderingContext2D, x: number, y: number, oddEven: number, scale: number) {
        // The hex transform.  renderer.is set so that (0,0) is the center of the hex
        ctx.setTransform(scale * HEX_X_SCALE, 0, 0, scale, HEX_X_SCALE * scale * (x+0.5), scale * (y - ((oddEven)/2)));
        this.setupContextDefaults(ctx);
    }

    setupSectorTransform(ctx: CanvasRenderingContext2D, x: number, y: number, scale: number) {
        ctx.setTransform(scale * HEX_X_SCALE * Sector.SECTOR_WIDTH, 0, 0, scale * Sector.SECTOR_HEIGHT,
            HEX_X_SCALE * scale * (x+ 0.5 * Sector.SECTOR_WIDTH),
            scale * (y - 0.5 * Sector.SECTOR_HEIGHT));
        this.setupContextDefaults(ctx);
    }

    setupContextDefaults(ctx: CanvasRenderingContext2D) {
        ctx.globalCompositeOperation = 'screen';

    }


    drawClearBorder(ctx: CanvasRenderingContext2D, x: number, y: number, scale: number, hexes: [number,number][]) {
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

        this.setupContextDefaults(ctx);

    }


}