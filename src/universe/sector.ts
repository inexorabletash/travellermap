import * as fs from "node:fs";
import csv from 'csv-parser';
import {World} from "./world.js";
import {Transform} from "node:stream";
import {XML} from "../xml.js";
import {Universe} from "../universe.js";
import {
    Override,
    OverrideAllegiance,
    OverrideBorder,
    OverrideCommon,
    OverrideRoute,
    OverrideSector
} from "./override.js";
import {Allegiance, Border, CreditDetails, Route, SectorMetadata} from "./sectorMetadata.js";
import {combinePartials} from "../util.js";
import logger from "../logger.js";

export class Sector {
    static readonly SECTOR_WIDTH = 32;
    static readonly SECTOR_HEIGHT = 40;
    static readonly SUBSECTOR_WIDTH = 8;
    static readonly SUBSECTOR_HEIGHT = 10;

    protected worlds: Map<string, World> = new Map<string, World>();
    protected allegiances_!: Record<string,Allegiance>;
    protected borders_!: Border[];
    protected routes_!: Route[];
    protected subsectors_!: Record<string,string>;

    public constructor(protected metadata: SectorMetadata) {
        this.allegiances_ = this.metadata.allegiances;
        this.borders_ = this.metadata.borders;
        this.routes_ = this.metadata.routes;
        this.subsectors_ = this.metadata.subsectors ?? {};
    }

    async mergeGlobalAllegiances(universe: Universe) {
        const globalAllegiances = await universe.getAllegiances()
        for(const allegiance of Object.values(globalAllegiances)) {
            if (!this.allegiances_[allegiance.code]) {
                this.allegiances_[allegiance.code] = allegiance;
            } else {
                this.tryMergeAllegiance(this.allegiances_[allegiance.code], allegiance);
            }
        }
        for(const allegiance of Object.values(this.allegiances_)) {
            if(allegiance.legacy) {
                this.tryMergeAllegiance(allegiance, globalAllegiances[allegiance.legacy]);
            }
            if(allegiance.baseCode) {
                this.tryMergeAllegiance(allegiance, globalAllegiances[allegiance.baseCode]);
            }
        }
    }

    lookupWorld(hex: string, _?: undefined): World;
    lookupWorld(x:number, y: number): World;
    lookupWorld(hex: string|number, y?: number|undefined): World|undefined {
        if(y !== undefined) {
            hex = World.coordsToHex([hex as number, y]);
        }
        return this.worlds.get(hex as string);
    }

    lookupWorldByName(name: string): World|undefined {
        for(const world of this.worlds.values()) {
            if(world.name.toLowerCase() === name.toLowerCase()) {
                return world;
            }
        }
        return undefined;
    }

    get name(): string {
        return this.metadata.name();
    }

    get x(): number {
        return this.metadata.x;
    }

    get y(): number {
        return this.metadata.y;
    }

    get borders(): Border[] {
        return this.borders_;
    }

    get routes(): Route[] {
        return this.routes_;
    }

    get credits(): CreditDetails {
        return this.metadata.credits;
    }

    get tags(): string {
        return this.metadata.credits.SectorTags;
    }

    get milieu(): string {
        return this.metadata.credits.SectorMilieu;
    }

    get allegiances(): Record<string,Allegiance> {
        return this.allegiances_;
    }

    get abbreviation() {
        return this.metadata.abbreviation;
    }

    getWorlds() {
        return this.worlds.values();
    }

    subsectorName(dx: number, dy: number) : string|undefined {
        return this.subsectors_[this.subsectorCode(dx, dy)];
    }

    subsectorCode(dx: number, dy: number) : string {
        return String.fromCharCode((dy * 4) + dx + 65);
    }

    subsectorCodeToRange(code: string): [number,number,number,number] {
        const uccode = code?.toUpperCase() ?? '';
        let index = Number.parseInt(code);
        if(uccode === 'ALPHA') {
            return [1,1,2*Sector.SUBSECTOR_WIDTH,2*Sector.SUBSECTOR_HEIGHT];
        } else if(uccode === 'BETA') {
            return [2*Sector.SUBSECTOR_WIDTH+1,1,2*Sector.SUBSECTOR_WIDTH,2*Sector.SUBSECTOR_HEIGHT];
        } else if(uccode === 'GAMMA') {
            return [1,2*Sector.SUBSECTOR_HEIGHT+1,2*Sector.SUBSECTOR_WIDTH,2*Sector.SUBSECTOR_HEIGHT];
        } else if(uccode === 'DELTA') {
            return [2*Sector.SUBSECTOR_WIDTH+1,2*Sector.SUBSECTOR_HEIGHT+1,2*Sector.SUBSECTOR_WIDTH,2*Sector.SUBSECTOR_HEIGHT];
        }
        if(!Number.isFinite(index)) {
            index = (uccode.charCodeAt(0) ?? 99)-0x40;
        }
        if(index < 16 && index > 0) {
            const y = Math.trunc((index - 1) / 4);
            const x = (index - 1) % 4;
            return [x * Sector.SUBSECTOR_WIDTH + 1, y * Sector.SUBSECTOR_HEIGHT + 1,
                Sector.SUBSECTOR_WIDTH, Sector.SUBSECTOR_HEIGHT];
        }
        return [1,1,Sector.SECTOR_WIDTH, Sector.SECTOR_HEIGHT];
    }

    subsectorIndex(dx: number, dy: number): number {
        return dy*4+dx+1;
    }

    subSectorCoords(hex: string): [number, number] {
        let [dx, dy] = World.hexToCoords(hex);
        dx = (dx - 1) / Sector.SUBSECTOR_WIDTH;
        dy = (dy - 1) / Sector.SUBSECTOR_HEIGHT;
        return [dx,dy];
    }

    subsectors(): Set<string> {
        return new Set(Object.values(this.subsectors_));
    }

    addWorld(world: World) {
        this.worlds.set(world.hex, world);
    }

    delWorld(world: World) {
        this.worlds.delete(world.hex);
    }

    applySectorOverride(universe: Universe, ovr: OverrideSector) {
        if(ovr.abbreviation) {
            universe.removeSector(this.abbreviation);
            universe.addSector(ovr.abbreviation, this);
            this.metadata.abbreviation = ovr.abbreviation;
        }
        if(ovr.name) {
            universe.removeSector(this.name);
            universe.addSector(ovr.name, this);
            this.metadata.setName(ovr.name);
        }
        if(ovr.x !== undefined || ovr.y !== undefined) {
            const oldKey = Universe.sectorKey(this.x, this.y);
            universe.removeSector(oldKey);
            this.metadata.x = ovr.x ?? this.x;
            this.metadata.y = ovr.y ?? this.y;
            universe.addSector(Universe.sectorKey(this.x, this.y), this);
        }
        if(ovr.milieu) {
            if(this.metadata.credits === undefined) {
                this.metadata.credits = {};
            }
            this.metadata.credits.SectorMilieu;
        }
        this.subsectors_ = combinePartials(this.subsectors_, ovr.subsector);
    }

    applyRouteOverride(ovr: OverrideRoute[]) {
        // remove duplicate old routes
        const oldRoutes = this.routes_?.filter(
            (route: Route) => {
                if(ovr.findIndex(v =>
                    ( (v.start === route.start || v.start === route.end) &&
                      (v.end === route.end || v.end === route.start)
                    )
                        || route.start === v.replace
                        || route.end === v.replace
                        ||
                    ( Array.isArray(v.replace) &&
                        (v.replace[0] === route.start || v.replace[0] === route.end) &&
                        (v.replace[1] === route.start || v.replace[1] === route.end)
                    )
                    )>=0) {
                    return false;
                }
                return true;
            }) ?? [];
        const newRoutes = ovr.filter(route => route.start && route.end);

        this.routes_ = [ ...oldRoutes, ...newRoutes]
    }

    applyBorderOverride(ovr: OverrideBorder[]) {
        const newBorders: OverrideBorder[] = [...ovr];
        const existingBorders = [...this.borders_];

        OUTER:
        for(let newIdx = 0; newIdx < newBorders.length; ++newIdx) {
            const newBorder = newBorders[newIdx];

            const start = newBorder.hexes?.[0] ?? newBorder.replace ?? '';
            const end = newBorder.hexes?.[newBorder.hexes.length-1] ?? newBorder.replace ?? '';

            //const border = borders[borderidx];
            for(let existIdx = 0; existIdx < existingBorders.length; ++existIdx) {
                const border = existingBorders[existIdx];

                const startLoc = border.hexes?.findIndex(v => v === start) ?? -1;
                const endLoc = border.hexes?.findIndex(v => v === end) ?? -1;

                if(startLoc<0 && endLoc <0) {
                    continue;
                }

                // At this point we've found a candidate for merging.
                existingBorders.splice(existIdx, 1);

                if(!newBorder.hexes) {
                    // If there are no hexes, this must be a remove request, so just continue
                    continue OUTER;
                }

                let newHexes: string[] = [];
                if(startLoc >= 0 && endLoc >= 0) {
                    if(startLoc === endLoc) {
                        newHexes = newBorder.hexes;
                    } else if(startLoc < endLoc) {
                        newHexes = [ ...border.hexes.slice(0, startLoc), ...(newBorder.hexes ?? []), ...border.hexes.slice(endLoc+1) ]
                    } else {
                        newHexes = [ end, ...border.hexes.slice(endLoc+1, startLoc), ...(newBorder.hexes ?? []) ];
                    }
                } else if(startLoc >= 0) {
                    newHexes = [ ...border.hexes.slice(0, startLoc), ...(newBorder.hexes ?? []),  ]
                } else  {
                    newHexes = [ ...(newBorder.hexes ?? []), ...border.hexes.slice(endLoc+1) ]
                }
                existingBorders.push(combinePartials(border, newBorder, {hexes: newHexes}));
                continue OUTER;
            }
            // If there is no existing overlap, just add it to the border list.
            existingBorders.push(newBorder);
        }

        this.borders_ = existingBorders;
    }


    static async loadFileTab(metadata: SectorMetadata, filename: string): Promise<Sector> {
        let initialSkipDone = false;
        let startOfLine = true;

        const xml = await XML.fromFile(`${filename}.xml`);
        const sectorX = xml.path('Sector.X').value();
        const sectorY = xml.path('Sector.Y').value();
        const name = xml.path('Sector.Name')
            .arrayValue(value => !value?.['@_Lang'] || value?.['@_Lang'] === 'en')
            ?.[0] ?? 'Unknown';

        const sector = new Sector(metadata);
        let doneResolve: any;
        let doneReject: any;
        const done = new Promise<void>((resolve, reject) => {
            doneResolve = resolve;
            doneReject = reject;
        });

        fs.createReadStream(filename)
            .pipe(new Transform({
                transform: (chunk: Buffer|string, encoding: string, next) => {
                    if(encoding === 'buffer') {
                        const c = chunk as Buffer;
                        let pos = 0;
                        if(!startOfLine) {
                            pos = c.indexOf('\n');
                            if(pos < 0) {
                                next();
                                return;
                            }
                            startOfLine = true;
                        }
                        while(!initialSkipDone) {
                            while(c[pos] === 0x20 || c[pos] === 0x09 || c[pos] === 0x0a) {
                                ++pos;
                            }
                            if(c[pos] === 0x23) {
                                pos = c.indexOf('\n', pos);
                                if(pos < 0) {
                                    startOfLine = false;
                                    next();
                                    return;
                                }
                            } else {
                                initialSkipDone = true;
                            }
                        }
                        if(pos === 0) {
                            return next(null, chunk);
                        }
                        //console.log(Buffer.from(c,pos).toString('utf8'));
                        const slice = Uint8Array.prototype.slice.call(c,pos);
                        //const x = c.slice(pos);
                        return next(null, slice);
                    } else {
                        throw new Error(`expecting a buffer?`);
                    }
                }
            }))
            .pipe(csv({
                separator: '\t',
                skipComments: true,
            }))
            .on('data', (data) => sector.addWorld(new World(sector, data)) )
            .on('end', () => {
                doneResolve();
            })
            .on('error', e => {
                doneReject(e);
            })
                ;

        await done;
        return sector;
    }

    static readonly SEC_FIELDS = {
        Sector: 6,
        SS: 2,
        Hex: 4,
        Name: 20,
        UWP: 9,
        Bases: 2,
        Remarks: 43,
        Zone: 1,
        PBG: 3,
        Allegiance: 4,
        Stars: 14,
        '{Ix}': 6,
        '(Ex)': 7,
        '[Cx]': 6,
        Nobility: 6,
        W: 2,
        RU: 6,
    };

    static async loadFileSecondSurvey(metadata: SectorMetadata, filename: string): Promise<Sector|undefined> {
        const data = await fs.promises.readFile(filename, { encoding: 'utf8'});
        const lines = data.split('\n');
        const sector = new Sector(metadata);

        const sections = this.parsePlaceholdersSS(lines.shift());
        if(!lines.shift()?.startsWith('-')) {
            return undefined;
        }

        for(const line of lines) {
            const trimmed = line.trimStart();
            if (trimmed.length === 0) {
                continue;
            }
            if (trimmed.startsWith('#')) {
                continue;
            }
            const world = this.processLine(sector, sections, '', line);
            if(world) {
                sector.addWorld(world);
            }
        }
        return sector;
    }

    static readonly REVAL = '^(\\s*(?<Name>.*))' +
        '(\\s*(?<Hex>\\d\\d\\d\\d))' +
        '(\\s{1,2}(?<UWP>[ABCDEX][0-9A-Z]{6}-[0-9A-Z]))' +
        '(\\s{1,2}(?<Base>[A-Zr1-9* ]?))' +
        '(\\s{1,2}(?<Remarks>.{10,}?))' +
        '(\\s+(?<Zone>[GARBFU]))?' +
        '(\\s{1,2}(?<PBG>(\\d[0-9A-F][0-9A-F]|XXX)))' +
        '(\\s{1,2}(?<Allegiance>(\\w\\w\\b|\\w-|--)))' +
        '(\\s*(?<Stars>.*?))\\s*$';
    static LINE_RE = new RegExp(Sector.REVAL);

    static async loadFileSec(metadata: SectorMetadata, filename: string): Promise<Sector|undefined> {
        const data = await fs.promises.readFile(filename, { encoding: 'utf8'});
        const lines = data.split('\n');
        const sector = new Sector(metadata);
        for(let line of lines) {
            line = line.trim();
            if(line.length === 0 || line.startsWith('#') || line.startsWith('@') || line.startsWith('$')) {
                continue;
            }
            const m = line.match(Sector.LINE_RE);
            if(m && m.groups) {
                sector.addWorld(new World(sector, {
                    Sector: sector.name,
                    SectorX: sector.x.toString(),
                    SectorY: sector.y.toString(),
                    ...m.groups
                }));
            } else {
                //console.log(`Skipping ${line}`);
            }
        }
        return sector;
    }

    protected static parsePlaceholdersSS(line: string|undefined): Record<string,number> {
        if(line === undefined) {
            return {};
        }
        const rv: Record<string,number> = {};
        let lastPos = 0;
        let inWord = true;
        for(let pos = 0; pos < line.length; ++pos) {
            if(line.charAt(pos) !== ' ') {
                if(!inWord) {
                    rv[line.substring(lastPos, pos).trim()] = pos-lastPos;
                    lastPos = pos;
                    inWord = true;
                }
            } else {
                inWord = false;
            }
        }
        rv[line.substring(lastPos).trim()] = 100;
        return rv;
    }

    protected static parsePlaceholders(line: string): Record<string,number> {
        const defs = [
            'Nothing',
            'Name',
            'Hex',
            'UWP',
            'Bases',
            'Remarks',
            'Zone',
            'PBG',
            'Allegiance',
            'LRX',
            'Stars',
        ];
        let last=0;
        let columns: Record<string,number> = {};
        for(let i = 0; i < line.length; ++i) {
            const def = defs[line.charCodeAt(i)-0x30];
            if(def) {
                columns[def] = i-last;
                last = i;
            }
        }
        columns[defs[10]] = 20;
        return columns;
    }

    protected static processLine(sector: Sector, sections: Record<string,number>, secName: string, line: string): World {
        let pos = 0;
        let result: Record<string,string> = {
            Sector: secName,
        };
        for(const entry of Object.entries(sections)) {
            result[entry[0]] = line.substring(pos, pos+entry[1]).trim();
            pos += entry[1];
        }
        //console.log(JSON.stringify(result));
        return new World(sector, result);
    }


    private overrideAllegianceColors(allegiance: Allegiance, wth: Allegiance) {
        (['borderColor', 'routeColor'] as (keyof Allegiance)[]).forEach(
            attr => {
                if(wth?.[attr]) {
                    allegiance[attr] = wth[attr];
                }
            });
    }

    private tryMergeAllegiance(allegiance: Allegiance, wth: Allegiance) {
        (['name', 'location', 'borderColor', 'routeColor','legacy','baseCode'] as (keyof Allegiance)[]).forEach(
            attr => {
                if(!allegiance[attr] && wth?.[attr]) {
                    allegiance[attr] = wth[attr];
                }
            });
    }

    applyOverrides(universe: Universe, overrides: Override[]) {
        for(const override of overrides) {
            const overrideSectors = universe.overrideSectors(override);
            const defaultSector = overrideSectors.length == 1 ? overrideSectors[0]?.sector : undefined;

            this.doOneOverrideSector(overrideSectors, defaultSector, ovr => {
                this.applySectorOverride(universe, ovr);
            });
            this.doOneOverrideSector(override.world, defaultSector, ovr => {
                World.applyOverride(this, ovr);
            });
            this.doOneOverrideSector(override.route, defaultSector, ovr => {
                this.applyRouteOverride([ovr])
            });
            this.doOneOverrideSector(override.border, defaultSector, ovr => {
                this.applyBorderOverride([ovr]);
           });
        }

    }

    sectorMatch(name: string|undefined) {
        if(name === undefined) {
            return false;
        }
        return (name.toLowerCase() === this.name?.toLowerCase() || name.toLowerCase() === this.abbreviation?.toLowerCase());
    }

    doOneOverrideSector<T extends OverrideCommon>(overrides: T[]|T, defaultSector: string|undefined, apply: (overrides: T) => void) {
        if(!overrides) {
            return;
        }
        if(!Array.isArray(overrides)) {
            overrides = [overrides];
        }
        for(const ovr of overrides) {
            if(ovr === undefined || ovr === null) {
                continue;
            }
            if((ovr.sector === undefined && this.sectorMatch(defaultSector)) || this.sectorMatch(ovr.sector)) {
                logger.info(`processing override for ${ovr.sector ?? defaultSector}: ${JSON.stringify(ovr)}`);
                apply(ovr);
            }
        }
    }
}


//Sector.loadFileTab('/main/rod/WebStormProjects/travmap/app/static/res/Sectors/M1105/Spinward Marches');
//Sector.loadFileSec('/main/rod/WebStormProjects/travmap/app/static/res/Sectors/M1105/Gzaekfueg.sec')
