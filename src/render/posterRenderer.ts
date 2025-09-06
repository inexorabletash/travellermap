import {Renderer} from "./renderer.js";
import {World} from "../universe/world.js";
import {Sector} from "../universe/sector.js";
import {Glyph} from "./glyph.js";
import {CanvasRenderingContext2D} from "canvas";
import {TileRender} from "./tileRender.js";

export class PosterRenderer extends Renderer {
    iconColor(w: World | Sector, options: number, glyph: Glyph): typeof CanvasRenderingContext2D.prototype.fillStyle {
        return glyph.defaultColor;
    }

    lineColor(options: number, type: string): typeof CanvasRenderingContext2D.prototype.strokeStyle {
        const ZONE_COLORS: Record<string, typeof CanvasRenderingContext2D.prototype.strokeStyle> = {
            'zone-R': '#ff0000',
            'zone-A': '#FFFF00',
            'zone-U': '#0000FF',
            border: 'red',
            route: 'forestgreen',
            hex: '#333333',
            sector: '#b3b3b3',
            subsector: '#b3b3b3',
        };

        return ZONE_COLORS[type];
    }

    toStyle(s: typeof CanvasRenderingContext2D.prototype.fillStyle) {
        return { line: s, fill: s};
    }

    worldColor(w: World, options: number): { line: typeof CanvasRenderingContext2D.prototype.fillStyle, fill: typeof CanvasRenderingContext2D.prototype.fillStyle} {
        const hydro = World.digitValue(w.uwp.substring(3, 4)) ?? 0;
        const atmos = World.digitValue(w.uwp.substring(2, 3)) ?? 0;

        if(options & TileRender.RenderWorldColors) {
            if (w.notes.has('Ri') && w.notes.has('Ag')) {
                return this.toStyle('#ffbf00');
            } else if (w.notes.has('Ag')) {
                return this.toStyle('#008000');
            } else if (w.notes.has('Ri')) {
                return this.toStyle('#800080');
            } else if (w.notes.has('In')) {
                return this.toStyle('#888888');
            } else if (atmos > 10) {
                return this.toStyle('#cc6626');
            } else if (w.notes.has('Va')) {
                return { line: 'white', fill:'black'};
            }
        }

        if (atmos > 2 && atmos < 10 && hydro > 0) {
            return this.toStyle('#33ccff');
        }
        return this.toStyle('#ffffff');
    }

    textColor(w: World, options: number, type: string): typeof CanvasRenderingContext2D.prototype.fillStyle {
        if (type === 'sector') {
            return '#333333';
        } else if (type === 'subsector') {
            return '#555555';
        } else if (type === 'name') {
            if(w.notes.has('Cp')) {
                return 'red';
            }
            if(w.notes.has('Cx')) {
                return '#D4AF37';
            }
        }
        return '#ffffff';
    }

    font(w: World | Sector, options: number, type: string, data: any): typeof CanvasRenderingContext2D.prototype.font {
        switch(type) {
            case 'region':
                return '0.01px serif';
            case 'subsector':
                return 'bold 0.05px sans-serif';
            case 'sector':
                return 'bold 0.2px sans-serif';
            case 'hex':
                return 'bold 0.1px sans-serif';
            case 'upp':
                return '0.1px sans-serif';
            case 'allegiance':
                return '0.15px sans-serif';
            case 'glyph':
                return '0.15px sans-serif';
            case 'name':
                if((w as World).notes.has('Cp') || (w as World).notes.has('Cx')) {
                    return 'bold 0.18px sans-serif';
                }
                return 'bold 0.12px sans-serif';
        }

        // Default case is huge to highlight
        return 'bold 0.5px sans-serif';
    }

}