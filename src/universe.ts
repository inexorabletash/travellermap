import {World} from "./universe/world.js";
import {Sector} from "./universe/sector.js";
import fs, {unlink} from "node:fs";
import path from "node:path";
import {XML} from "./xml.js";
import csv from "csv-parser";
import {Allegiance, SectorMetadata} from "./universe/sectorMetadata.js";
import {Override, OverrideAllegiance, OverrideCommon, OverrideSector} from "./universe/override.js";
import * as YAML from 'yaml';
import logger from './logger.js';
import {CaseInsensitiveFileResolver} from "./caseInsensitiveFileResolver.js";
import {combinePartials} from "./util.js";
import {inspect} from "node:util";

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
    protected static milieuTab_: Promise<Record<string,Milieu>>;
    protected static milieuDefs: Promise<Record<string,Set<string>>>;
    protected static sophontTab: Promise<Record<string,Sophont>>;
    protected static allegianceTab: Promise<Record<string,Allegiance>>;
    static baseDir = path.join(process.cwd(), 'static', 'res', 'Sectors');
    static OVERRIDE_DIR = path.join(process.cwd(), 'static', 'res', 'overrides');
    protected _sectors: Map<string,Sector> = new Map<string, Sector>();
    static readonly LOADER_LOOKUP: Record<string,(metadata: SectorMetadata, file: string) => Promise<Sector|undefined>> = {
        TabDelimited: (metadata, file) => Sector.loadFileTab(metadata, file),
        SecondSurvey: (metadata, file) => Sector.loadFileSecondSurvey(metadata, file),
        '': (metadata, file) => Sector.loadFileSec(metadata, file),
        'SEC': (metadata, file) => Sector.loadFileSec(metadata, file),
    };

    static async milieuToLoad(key: string): Promise<Set<string>> {
        if(this.milieuDefs === undefined) {
            try {
                this.milieuDefs = this.loadCsv(path.join(Universe.OVERRIDE_DIR, Universe.MILIEU_TAB),
                    data => {
                        return [data.Name, new Set(data.Milieu?.split(/\s+/) ?? [])];
                    });
            } catch(e: any) {
                logger.warn(`Failed to load milieu definitions: ${e.stack}`);
            }
        }
        const defs = await this.milieuDefs;
        let milieuMatch = new Set([key]);
        if(defs[key]) {
            milieuMatch = defs[key] ?? new Set();
        }
        return milieuMatch;
    }

    static get milieuTab(): Promise<Record<string,Milieu>> {
        if(this.milieuTab_ === undefined) {
            this.milieuTab_ = this.loadCsv(path.join(Universe.baseDir, Universe.MILIEU_TAB),
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
        return this.milieuTab_;
    }

    static async loadUniverse(key: string, milieuMatch: Set<string>): Promise<Universe> {
        const universe = new Universe();
        const milieuTab = await this.milieuTab;
        const overrideFiles = await this.loadOverrideFiles(path.join(Universe.OVERRIDE_DIR, key));

        universe.applyAllegianceOverrides(overrideFiles);

        for(const milieu of Object.values(milieuTab)) {
            await Universe.loadConfig(
                universe,
                path.join(Universe.baseDir, milieu.configPath),
                overrideFiles,
                milieuMatch
            );
            milieu.configPath
        }
        return universe;
    }

    static async getUniverse(key: string|undefined): Promise<Universe> {
        key ??= this.DEFAULT_MILIEU;
        const milieuMatch = await this.milieuToLoad(key);
        let universe = Universe.universes.get(key);

        if(universe === undefined) {
            universe = this.loadUniverse(key, milieuMatch);
            Universe.universes.set(key,universe);
        }

        return universe;
    }

    static async getGlobalAllegiances(): Promise<Record<string,Allegiance>> {
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

    protected allegianceOverrides: OverrideAllegiance[] = [];
    protected allegiances_!: Record<string,Allegiance>;
    async getAllegiances(): Promise<Record<string,Allegiance>> {
        if(this.allegiances_ === undefined) {
            this.allegiances_ = {...await Universe.getGlobalAllegiances()};
            for(const ovr of this.allegianceOverrides) {
                this.allegiances_[ovr.code] = combinePartials(this.allegiances_[ovr.code] ?? {}, ovr);
            }
        }
        return this.allegiances_;
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

        const sector = this._sectors.get(secKey);
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

    sectors() : Sector[] {
        const sectorSet = new Set(this._sectors.values());
        return [...sectorSet.values()];
    }
    getSector(sx: number, sy: number) : Sector|undefined {
        return this._sectors.get(Universe.sectorKey(sx,sy));
    }
    getSectorByName(name: string) : Sector|undefined {
        return this._sectors.get(name.toLowerCase());
    }
    getSectorBySubsectorName(name: string): Sector|undefined {
        for(const s of this._sectors.values()) {
            if(s.subsectors().has(name)) {
                return s;
            }
        }
        return undefined;
    }

    applyAllegianceOverrides(overrides: Override[]) {
        for(const override of overrides) {
            if(override?.allegiance) {
                this.allegianceOverrides.push(...(Array.isArray(override.allegiance) ? override.allegiance : [override.allegiance]));
            }
        }
    }

    overrideSectors(override: Override) {
        let sectors: OverrideSector[] = [];

        if (override.sector == undefined) {
            return [];
        } else if (typeof override.sector === 'string') {
            return [ {sector: override.sector}];
        } else if (Array.isArray(override.sector)) {
            sectors = override.sector;
        } else {
            sectors = [override.sector];
        }
        return sectors;
    }

    search(query: string): SearchResponse {
        const x = [ ...this._sectors.values() ];
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

    static async loadOverrideFiles(overrideDir: string): Promise<Override[]> {
        // We don't use readdir withtypes here because that will return isSymlink() rather than isFile() for a
        // link to a file.  Instead we get the filenames and then stat each file explicitly which resolves symlinks
        // to the file/directory/non-existent entity.
        try {
            const files = await fs.promises.readdir(overrideDir, {
                encoding: 'utf8',
                recursive: true,
                withFileTypes: false
            });
            const overrideFiles = await Promise.all(files
                .map(async name => {
                    const fullPath = path.join(overrideDir, name);
                    try {
                        const st = await fs.promises.stat(fullPath);
                        if(st.isFile()) {
                            return fullPath;
                        }
                        return undefined;
                    } catch(e) {
                        return undefined;
                    }
                }));
            const allOverrides = await Promise.all((overrideFiles
                .filter(file => file !== undefined && !file.startsWith('#') && path.extname(file) === '.yml') as string[])
                .map(async (file) => {
                    try {
                        const yamlData = await fs.promises.readFile(file, {encoding: 'utf8'});
                        const parsed = YAML.parse(yamlData);
                        return parsed;
                    } catch (e) {
                        logger.warn(e, `Failed to process override file: ${file}`);
                        return undefined;
                    }
                }));
            const result = allOverrides
                .filter(result => result !== undefined && result !== null);
            logger.info(`Overrides: ${JSON.stringify(result, undefined, 2)}`);
            return result;
        } catch(e) {
            return [];
        }
    }

    static async loadConfig(universe: Universe, file: string, overrides: Override[], milieuMatch: Set<string>): Promise<Universe> {
        const xml = await XML.fromFile(file);
        const dirName = path.dirname(file);
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

            let sectorData: Sector|undefined;
            if (metadata.x === undefined || metadata.y === undefined) {
                sectorData = undefined;
            } else if (dataFile === undefined) {
                sectorData = new Sector(metadata);
            } else {
                const rawFilePath = path.join(dirName, dataFile);
                const dataFilePath = await resolver.resolve(rawFilePath);
                if(dataFilePath !== undefined) {
                    sectorData = await this.LOADER_LOOKUP[dataFileType](metadata, dataFilePath);
                }
            }

            if(sectorData !== undefined) {
                sectorData.applyOverrides(universe, overrides);
                await sectorData.mergeGlobalAllegiances(universe);

                if(sectorData.milieu !== undefined && !milieuMatch.has(sectorData.milieu)) {
                    continue;
                }
                const sectorKey = this.sectorKey(sectorData.x, sectorData.y);
                if(universe._sectors.has(sectorKey) &&
                    ((universe._sectors.get(sectorKey)?.milieu === undefined && sectorData.milieu !== undefined))) {
                    // If the sector is already defined but is not an explicit milieu match, drop the old one
                    universe.removeSector(sectorKey);
                }
                // If the sector is already defined use the existing
                if(!universe._sectors.has(sectorKey)) {
                    // don't redefine existing sectors
                    universe._sectors.set(sectorKey, sectorData);
                    universe._sectors.set(sectorData.name.toLowerCase(), sectorData);
                    if (sectorData.abbreviation) {
                        universe._sectors.set(sectorData.abbreviation.toLowerCase(), sectorData);
                    }
                }
            }

        }

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
                            if(Array.isArray(record)) {
                                loaded[record[0]] = record[1];
                            }
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
        this._sectors.delete(name);
    }

    addSector(name: string, sector: Sector) {
        this._sectors.set(name, sector);
    }

}


