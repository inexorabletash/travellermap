import {World} from "./universe/world.js";
import {Sector} from "./universe/sector.js";
import fs, {unlink} from "node:fs";
import path from "node:path";
import {XML} from "./xml.js";
import csv from "csv-parser";
import {inspect} from "node:util";
import {Allegiance, SectorMetadata} from "./universe/sectorMetadata.js";
import {Override, OverrideCommon, OverrideSector} from "./universe/override.js";
import * as YAML from 'yaml';
import logger from './logger.js';
import {CaseInsensitiveFileResolver} from "./caseInsensitiveFileResolver.js";

export type Milieu = {
    code: string;
    mtime?: number;
    ltime?: number;
    tags: string;
    name: string;
    configPath: string;
}

export type Sophont = {
    code: string,
    name: string,
    location: string,
};

export type SearchElement = {
    World: {
        HexX: number,
        HexY: number,
        SectorX: number,
        SectorY: number,
        Sector: string,
        Uwp: string,
        Name: string,
        SectorTags: string,
    }
}

export type SearchResponse = {
    Count: number,
    Items: SearchElement[],
}

export class Universe {
    static readonly MILIEU_TAB = 'milieu.tab';
    static readonly SOPHONT_TAB_GLOBAL = 'sophonts.tab';
    static readonly ALLEGIANCE_TAB_GLOBAL = 'allegiance_global.tab';
    static readonly DEFAULT_MILIEU = 'M1105';
    protected static universes: Map<string,Promise<Universe>> = new Map();
    protected static milieuTab: Promise<Record<string,Milieu>>;
    protected static sophontTab: Promise<Record<string,Sophont>>;
    protected static allegianceTab: Promise<Record<string,Allegiance>>;
    protected static baseDir = path.join(process.cwd(), 'static', 'res', 'Sectors');
    public static readonly OVERRIDE_DIR = path.join(process.cwd(), 'static', 'res', 'overrides');
    protected sectors: Map<string,Sector> = new Map<string, Sector>();
    static readonly LOADER_LOOKUP: Record<string,(metadata: SectorMetadata, file: string) => Promise<Sector|undefined>> = {
        TabDelimited: (metadata, file) => Sector.loadFileTab(metadata, file),
        SecondSurvey: (metadata, file) => Sector.loadFileSecondSurvey(metadata, file),
        '': (metadata, file) => Sector.loadFileSec(metadata, file),
        'SEC': (metadata, file) => Sector.loadFileSec(metadata, file),
    };

    static async getUniverse(key: string|undefined): Promise<Universe> {
        key ??= Universe.DEFAULT_MILIEU;
        if(this.milieuTab === undefined) {
            this.milieuTab = this.loadCsv(path.join(Universe.baseDir, Universe.MILIEU_TAB),
                (data) => {
                    const code = path.dirname(data.Path);
                    return [ code, {
                        code,
                        name: code,
                        configPath: data.Path,
                        tags: data.Tags,
                    }];
                });
        }

        const milieuTab = await Universe.milieuTab;
        if(Universe.universes.get(key) === undefined) {
            if(milieuTab[key] === undefined) {
                throw new Error(`Unknown Milieu ${key}`);
            }
            const loaded = Universe.loadConfig(
                path.join(Universe.baseDir, milieuTab[key].configPath),
                path.join(Universe.OVERRIDE_DIR, path.dirname(milieuTab[key].configPath)),
                );
            Universe.universes.set(key, loaded);
        }
        return Universe.universes.get(key) as Promise<Universe>;
    }

    static async getAllegiances(): Promise<Record<string,Allegiance>> {
        if(Universe.allegianceTab === undefined) {
            this.allegianceTab = this.loadCsv(path.join(Universe.baseDir, Universe.ALLEGIANCE_TAB_GLOBAL),
                (data) => {
                //Code	Legacy	BaseCode	Name	Location
                    return [ data.Code, {
                        code: data.Code,
                        legacy: data.Legacy,
                        baseCode: data.BaseCode,
                        name: data.Name,
                        location: data.Location,
                        borderColor: data.BorderColor,
                        routeColor: data.RouteColor,
                    }];
                });
        }
        await Universe.allegianceTab;
        return Universe.allegianceTab;
    }

    static async getSophonts(): Promise<Record<string,Sophont>> {
        if(Universe.sophontTab === undefined) {
            this.sophontTab = this.loadCsv(path.join(Universe.baseDir, Universe.SOPHONT_TAB_GLOBAL),
                (data) => {
                    //Code	Name	Location
                    return [ data.Code, {
                        code: data.Code,
                        name: data.Name,
                        location: data.Location,
                    }];
                });

        }
        return Universe.sophontTab;

    }

    lookupWorld(absx: number, absy: number): World|undefined {
        const secx = Math.floor((absx) / Sector.SECTOR_WIDTH);
        const secyBase = Math.floor((absy - 1) / Sector.SECTOR_HEIGHT);
        const secy = secyBase + 1;

        const worldx = absx - secx * Sector.SECTOR_WIDTH + 1;
        const worldy = absy - secyBase * Sector.SECTOR_HEIGHT;
        const secKey = Universe.sectorKey(secx, secy);

        const sector = this.sectors.get(secKey);
        return sector?.lookupWorld(worldx, worldy);
    }

    lookupWorldByName(name: string|undefined): World|undefined {
        if(name === undefined) {
            return undefined;
        }
        if(name.indexOf('/') > 0) {
            const comps = name.split('/', 2);
            const sector = this.getSectorByName(comps[0]);
            const world = sector?.lookupWorldByName(comps[1]);
            return world;
        }
        const hexOffset = name.lastIndexOf(' ');
        if(hexOffset < 0) {
            return undefined;
        }
        const sectorName = name.substring(0, hexOffset).trim();
        const hex = name.substring(hexOffset+1).trim();
        const sector = this.getSectorByName(sectorName);
        return sector?.lookupWorld(hex);
    }



    translateCoords(absx: number, absy: number): { sx: number, sy: number, hx: number, hy: number } {
        const secx = Math.floor((absx) / Sector.SECTOR_WIDTH);
        const secyBase = Math.floor((absy - 1) / Sector.SECTOR_HEIGHT);
        const secy = secyBase + 1;

        const worldx = absx - secx * Sector.SECTOR_WIDTH + 1;
        const worldy = absy - secyBase * Sector.SECTOR_HEIGHT;
        return { sx: secx, sy: secy, hx: worldx, hy: worldy };
    }
    static universeCoords(data: { sx: number, sy: number, hx: number, hy: number }): [number, number] {
        return [
            data.hx + data.sx * Sector.SECTOR_WIDTH - 1,
            data.hy + (data.sy-1) * Sector.SECTOR_HEIGHT,
        ];
    }


    getSector(sx: number, sy: number) : Sector|undefined {
        return this.sectors.get(Universe.sectorKey(sx,sy));
    }
    getSectorByName(name: string) : Sector|undefined {
        return this.sectors.get(name.toLowerCase());
    }
    getSectorBySubsectorName(name: string): Sector|undefined {
        for(const s of this.sectors.values()) {
            if(s.subsectors().has(name)) {
                return s;
            }
        }
        return undefined;
    }

    doOneOverrideSector<T extends OverrideCommon>(overrides: T[]|T, defaultSector: string|undefined, apply: (sector: Sector|undefined, overrides: T[]) => void) {
        if(!Array.isArray(overrides)) {
            overrides = [overrides];
        }
        const groupedOverrides: Record<string,T[]> = overrides?.reduce((pv: Record<string,T[]>, cv) => {
            const sector = cv?.sector ?? defaultSector;
            pv[sector] ??= [];
            if(cv !== undefined) {
                pv[sector].push(cv);
            }
            return pv;
        }, {}) ?? {};
        Object.entries(groupedOverrides).forEach(([sName, data]) => {
            const sector = this.sectors.get(sName);
            apply(sector, data);
        });
    }

    applyOverride(override: Override) {
        let defaultSector: string|undefined = undefined;
        let sectors: OverrideSector[] = [];

        if(override.sector === undefined) {
        } else if(typeof override.sector === 'string') {
            defaultSector = override.sector;
        } else if(Array.isArray(override.sector)) {
            sectors = override.sector;
        } else {
            sectors = [ override.sector ];
        }
        sectors.forEach(ovr => {
            if(ovr.sector === undefined) {
                console.warn('Skipping sector override because it has no "sector" member');
            }
            const sector = this.sectors.get(ovr.sector);
            if(sector) {
                sector.applySectorOverride(this, ovr);
                if(defaultSector === undefined) {
                    defaultSector = sector.abbreviation.toLowerCase();
                }
            }
        });
        this.doOneOverrideSector(override.world, defaultSector, (sector, overrides) => {
            if(sector !== undefined) {
                overrides.forEach(override => World.applyOverride(sector, override));
            }
        });
        this.doOneOverrideSector(override.allegiance, defaultSector, (sector, overrides) => {
            if(sector !== undefined) {
                overrides.forEach(override => sector.applyAllegianceOverride(override));
            }
        });
        this.doOneOverrideSector(override.route, defaultSector, (sector, overrides) => {
            if(sector !== undefined) {
                sector.applyRouteOverride(overrides);
            }
        });
        this.doOneOverrideSector(override.border, defaultSector, (sector, overrides) => {
            if(sector !== undefined) {
                sector.applyBorderOverride(overrides);
            }
        });

    }

    search(query: string): SearchResponse {
        const x = [ ...this.sectors.values() ];
        const y = x.flatMap(sector => [...sector.getWorlds()]);
        const z: Set<World> = new Set([...y]);
        let matches: Set<World>[] = [z];
        const words = query.toLowerCase().split(/\s+/);

        for(const word of words) {
           let newMatches: Set<World>[] = [ new Set() ];
           let allMatches: Set<World> = new Set();
           for(let idx = 0; idx < matches.length; ++idx) {
               [...matches[idx]].filter(w => !!w.name.toLowerCase().split(/\s+/).find(m => m.startsWith(word)))
                   .forEach(w => { const gc = w.globalCoords; if(!allMatches.has(w)) { newMatches[idx].add(w); allMatches.add(w); }});

               const partialMatches = [...matches[idx]].filter(w => !allMatches.has(w))
                   .filter(w => !!w.name.toLowerCase().split(/\s+/).find(m => m.includes(word)));
               partialMatches.forEach(w => allMatches.add(w));
               newMatches.push(new Set(partialMatches));
           }
           matches = newMatches;
        }
        const Items = matches.flatMap(matchSet => [...matchSet].map(w => ({
            World: {
                HexX: w.x,
                HexY: w.y,
                SectorX: w.sector_.x,
                SectorY: w.sector_.y,
                Sector: w.sector_.name,
                Uwp: w.uwp,
                Name: w.name,
                SectorTags: '',
            }
        })))
        const Count = Items.length;
        return {
            Items: Items.slice(0,10),
            Count,
        }
    }

    async loadOverrides(overrideDir: string) {
        try {
            const files = await fs.promises.readdir(overrideDir, {
                encoding: 'utf8',
                recursive: true,
                withFileTypes: true
            });
            for (const file of files) {
                try {
                    if (file.isFile() && !file.name.startsWith('#') && path.extname(file.name) === '.yml') {
                        const yamlData = await fs.promises.readFile(path.join(overrideDir, file.name), {encoding: 'utf8'});
                        const parsed = YAML.parse(yamlData);
                        this.applyOverride(parsed);
                    }
                } catch(e) {
                    logger.warn(e, `Failed to process override file: ${overrideDir}/${file.name}`);
                }
            }
        } catch(e) {
            logger.warn(`Failed to process directory ${overrideDir}`);
        }
    }

    static async loadConfig(file: string, overridePath: string): Promise<Universe> {
        const xml = await XML.fromFile(file);
        const dirName = path.dirname(file);
        const universe = new Universe();
        const resolver = new CaseInsensitiveFileResolver();

        const sectors = xml.path('Sectors.Sector');
        for(const sector of sectors.array()) {
            // TODO: name/Lang attribute parsing
            const names = SectorMetadata.extractNameMap(sector.path('Name'));
            const dataFile = sector.path('DataFile').value();
            const dataFileType = sector.path('DataFile.@_Type').value() ?? '';
            const metadataFile = sector.path('MetadataFile').value();

            const metadata = new SectorMetadata({});
            metadata.processSectorMetadata(sector);
            if(metadataFile !== undefined) {
                const rawFilePath = path.join(dirName, metadataFile);
                const metadataPath = await resolver.resolve(rawFilePath);
                if(metadataPath !== undefined) {
                    await metadata.loadMetadataFile(metadataPath);
                }
            }

            let data: Sector|undefined;
            if (metadata.x === undefined || metadata.y === undefined) {
                data = undefined;
            } else if (dataFile === undefined) {
                data = new Sector(metadata);
            } else {
                const rawFilePath = path.join(dirName, dataFile);
                const dataFilePath = await resolver.resolve(rawFilePath);
                if(dataFilePath !== undefined) {
                    data = await this.LOADER_LOOKUP[dataFileType](metadata, dataFilePath);
                }
            }

            if(data !== undefined) {
                await data.mergeGlobalAllegiances();
                const sectorKey = this.sectorKey(data.x, data.y);
                if(!universe.sectors.has(sectorKey)) {
                    // don't redefine existing sectors
                    universe.sectors.set(sectorKey, data);
                    universe.sectors.set(data.name.toLowerCase(), data);
                    if (data.abbreviation) {
                        universe.sectors.set(data.abbreviation.toLowerCase(), data);
                    }
                }
            }
        }
        await universe.loadOverrides(overridePath);

        return universe;
    }

    static sectorKey(secx: number, secy: number): string {
        return `${secx},${secy}`;
    }

    static async loadCsv<T>(filename: string, keyfn: (r: any) => [string, T]): Promise<Record<string,T>> {
        try {
            return await new Promise((resolve, reject) => {
                const loaded: Record<string, T> = {};
                try {
                    fs.createReadStream(filename)
                        .on('error', e => {
                            reject(e)
                        }).pipe(csv({
                            skipComments: true,
                            separator: '\t',
                        }))
                        .on('data', (data: any) => {
                            const record = keyfn(data);
                            loaded[record[0]] = record[1];
                        })
                        .on('headers', headers => {
                            //console.log(`${filename} => ${headers}`);
                        })
                        .on('end', () => {
                            resolve(loaded);
                        })
                        .on('error', e => {
                            reject(e)
                        });
                } catch(e) {
                    reject(e);
                }
            });
        } catch(e) {
            logger.error(e, `failed to load csv ${filename}`);
            return {};
        }
    }

    removeSector(name: string) {
        this.sectors.delete(name);
    }

    addSector(name: string, sector: Sector) {
        this.sectors.set(name, sector);
    }

}


