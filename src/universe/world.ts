// Sector	SS	Hex	Name	UWP	Bases	Remarks	Zone	PBG	Allegiance	Stars	{Ix}	(Ex)	[Cx]	Nobility	W	RU
import {Sector} from "./sector.js";
import {Universe} from "../universe.js";
import {OverrideWorld} from "./override.js";
import {fluxAmount} from "../util.js";
import {WorldGen} from "./worldGen.js";

export type HexType = [number,number]|[number,number,string];
export const STAR_PRIMARY = 0;
export const STAR_PRIMARY_COMPANION = 1;
export const STAR_CLOSE = 2;
export const STAR_CLOSE_COMPANION = 3;
export const STAR_NEAR = 4;
export const STAR_NEAR_COMPANION = 5;
export const STAR_FAR = 6;
export const STAR_FAR_COMPANION = 7;

export enum Notes {
    NO_JUMP = '!J',
    NO_MANOUVER = '!M',
}

export class World {
    sector_: Sector;
    secName_: string;
    name_: string;
    sec_: [number, number];
    hex_: HexType;
    uwp_: string;
    bases_: string;
    notes_: Set<string>;
    zone_: string;
    PBG_: string;
    allegiance_: string;
    stars_: string;
    ix_: string;
    ex_: string;
    cx_: string;
    nobility_: string;
    w_: string;
    ru_: string;
    hasOverride_: boolean;

    constructor(sector: Sector, x: number, y: number);
    constructor(sector: Sector, data: Record<string,string>, ignored?:number);
    constructor(sector: Sector, x: Record<string,string>|number, y: number|undefined) {
        this.sector_ = sector;
        this.uwp_ = 'X******-*';
        this.sec_ = [sector.x, sector.y];
        this.bases_ = '';
        this.notes_ = new Set();
        this.zone_ = '';
        this.PBG_ = '';
        this.allegiance_ = 'Na';
        this.stars_ = '';
        this.ix_ = '';
        this.ex_ = '';
        this.cx_ = '';
        this.nobility_ = '';
        this.w_ = '';
        this.ru_ = '';
        this.secName_ = sector.name
        this.hasOverride_ = false;

        if(typeof x === 'object') {
            this.hex_ = World.hexToCoords(x.Hex);
            this.name_ = '#' + this.sec + '-' + this.hex;
            this.applyData(<any>x);
        } else {
            y ??= 0;
            this.sec_ = [
                Math.trunc((x - 1) / Sector.SECTOR_WIDTH) - (x < 0 ? 1 : 0),
                Math.trunc((y - 1) / Sector.SECTOR_HEIGHT) - (y < 0 ? 1 : 0)
            ];
            this.secName_ = this.sec_.map(v => World.zeroPad(v)).join('');
            this.hex_ = [x - this.sec_[0] * Sector.SECTOR_WIDTH, y - this.sec_[1] * Sector.SECTOR_HEIGHT];
            this.name_ = '#' + this.sec + '-' + this.hex;
        }

    }

    protected applyData(x: Record<string,string> & { hasOverride: boolean}) {
        const notes = this.notes_ ?? new Set();
        if(x.Remarks) {
            for(const remark of x.Remarks.split(/\s+/)) {
                if(remark.startsWith('-')) {
                    notes.delete(remark.substring(1));
                } else {
                    notes.add(remark);
                }
            }
        }

        this.secName_ = x.Sector ?? this.secName_;
        this.name_ = x.Name ?? this.name_;
        this.uwp_ = x.UWP ?? this.uwp_;
        this.bases_ = x.Bases ?? this.bases_;
        this.notes_ = notes;
        this.zone_ = x.Zone ?? this.zone_;
        this.PBG_ = x.PBG ?? this.PBG_;
        this.allegiance_ = x.Allegiance ?? this.allegiance_;
        this.stars_ = x.Stars ?? this.stars_;
        this.ix_ = x['{Ix}'] ?? this.ix_;
        this.ex_ = x['(Ex)'] ?? this.ex_;
        this.cx_ = x['[Cx]'] ?? this.cx_;
        this.nobility_ = x.Nobility ?? this.nobility_;
        this.w_ = x.W ?? this.w_;
        this.ru_ = x.RU ?? this.ru_;
        this.hasOverride_ = x.hasOverride ?? this.hasOverride_;
    }


    static zeroPad(value: number, digits = 2): string {
        let v = 1;
        if (value < 0) {
            return '-' + this.zeroPad(-value,digits);
        }
        value = Math.trunc(value);
        let result = value === 0 ? '' : value.toString();
        while(digits-- > 0) {
            if(value < v) {
                result = '0' + result;
            }
            v *= 10;
        }
        return result;
    }

    get hex(): string {
        return World.coordsToHex(this.hex_);
    }

    get sec(): string {
        return this.sec_.map(v => World.zeroPad(v)).join('');
    }

    get secName(): string {
        return this.sector_.abbreviation ?? this.sector_.name;
    }

    get uwp(): string {
        return this.uwp_;
    }

    get techLevel(): number|undefined {
        return World.digitValue(this.uwp?.[8]);
    }

    get lawLevel(): number|undefined {
        return World.digitValue(this.uwp?.[6]);
    }

    get populationDigit(): number|undefined {
        return World.digitValue(this.uwp?.[5]);
    }

    get size(): number|undefined {
        return World.digitValue(this.uwp?.[1]);
    }

    get planets(): number|undefined {
        return this.w_ ? Number.parseInt(this.w_) : undefined;
    }

    get stars(): (string|undefined)[] | undefined {
        if(!this.stars_) {
            return undefined;
        }
        if(this.stars_.match(/[\/-]/)) {
            const result: (string|undefined)[] = [];
            const split = this.stars_.split(/\//);
            for(let idx = 0; idx < 4; ++idx) {
                if(!split[idx]) {
                    result.push(undefined, undefined);
                } else {
                    const splitComps = split[idx].split('-').map(sc => sc.trim());
                    if(splitComps.length > 1) {
                        result.push(splitComps[0], splitComps[1]);
                    }
                    result.push(splitComps[0], undefined);
                }
            }
            return result;
        }
        const STAR_MATCH_RE = /([OBAFGKMD][0-9]( *(III|II|Ia|Ib|IV|V|VI|D))?|(D|BD|DM))( *|$)/gi;
        return [...this.stars_.matchAll(STAR_MATCH_RE).map(it => it[0])];
    }

    get name(): string {
        return this.name_;
    }

    get zone(): string {
        return this.zone_;
    }

    get allegiance(): string {
        return this.allegiance_;
    }

    get bases(): string {
        return this.bases_;
    }

    get notes(): Set<string> {
        return this.notes_;
    }

    get pbg(): string {
        return this.PBG_ ?? '';
    }

    get x(): number {
        return this.hex_[0];
    }

    get y(): number {
        return this.hex_[1];
    }

    get ix(): string {
        return this.ix_;
    }

    get cx(): string {
        return this.cx_;
    }

    get ex(): string {
        return this.ex_;
    }

    get hasOverride(): boolean {
        return this.hasOverride_;
    }

    get globalCoords(): [number, number] {
        return Universe.universeCoords({sx: this.sector_.x, sy: this.sector_.y, hx: this.x, hy: this.y})
    }

    get children(): World[] {
        const baseName = World.coordsToHex(<[number,number]>this.hex_.slice(0,2));
        return [ ...this.sector_.getAllWorlds().filter(w => w.hex.startsWith(baseName)) ];
    }

    get parent(): World|undefined {
        const pos = this.hex.indexOf('-')
        if(pos >= 0) {
            return this.sector_.lookupWorld(this.hex.substring(0, pos));
        }
        return undefined;
    }



    credits(): Record<string,any> {
        const ssCoords = this.sector_.subSectorCoords(this.hex);
        const credits = this.sector_.credits;

        return {
            "Credits": credits.Text,
            "SectorX": this.sector_.x,
            "SectorY": this.sector_.y,
            "SectorName": this.sector_.name,
            "SectorSource": credits.SectorSource,
            "SectorMilieu": credits.SectorMilieu,
            "SectorTags": [...(credits.SectorTags ?? [])].join(' '),
            "SubsectorName": this.sector_.subsectorName(...ssCoords),
            "SubsectorIndex": this.sector_.subsectorCode(...ssCoords),
            "WorldName": this.name,
            "WorldHex": this.hex,
            "WorldUwp": this.uwp,
            "WorldRemarks": [ ...this.notes ].join(' '),
            "WorldIx": this.ix_,
            "WorldEx": this.ex_,
            "WorldCx": this.cx_,
            "WorldPbg": this.pbg,
            "WorldAllegiance": this.allegiance,
            "ProductPublisher": credits.ProductPublisher,
            "ProductTitle": credits.ProductTitle,
            "ProductAuthor": credits.ProductAuthor,
            "ProductRef": credits.ProductRef
        }

    }

    jumpWorld(): Record<string,any> {
        const ssCoords = this.sector_.subSectorCoords(this.hex);
        const System = this.children.map(child => ({
            Hex: child.hex,
            Name: child.name,
            UWP: child.uwp,
            Notes: [...child.notes],
            Flags:
                (child.notes.has(Notes.NO_JUMP) ? '*' : '') +
                    (child.notes.has(Notes.NO_MANOUVER) ? '!': ''),
            }))
        return {
            "Name": this.name,
            "Hex": this.hex,
            "UWP": this.uwp,
            "PBG": this.pbg,
            "Zone": this.zone,
            "Bases": this.bases,
            "Allegiance": this.allegiance,
            "Stellar": this.stars_,
            "SS": this.sector_.subsectorCode(...ssCoords),
            "Ix": this.ix_,
            "Ex": this.ex_,
            "Cx": this.cx_,
            "Nobility": this.nobility_,
            "Worlds": this.w_,
            "ResourceUnits": this.ru_,
            "WorldX": -119,
            "WorldY": -76,
            "Remarks": [ ...this.notes_ ].join(' '),
            "LegacyBaseCode": "",
            "Sector": this.sector_.name,
            "SubsectorName": this.sector_.subsectorName(...ssCoords),
            "SectorAbbreviation": this.sector_.abbreviation,
            "AllegianceName": this.sector_.allegiances[this.allegiance]?.name ?? '',
            "SectorX": this.sector_.x,
            "SectorY": this.sector_.y,
            "HexX": this.x,
            "HexY": this.y,
            "Parent": this.parent ? { Hex: this.parent.hex, UWP: this.parent.uwp } : undefined,
            "System": System.length ? System : undefined,
        }
    }

    enrichWorld(): OverrideWorld[]|undefined {
        const planets = WorldGen.enrichPlanets(this);
        return planets;
    }

    static digitValue(digit: string|undefined) {
        if(digit === undefined) {
            return undefined;
        }
        const val = digit.charAt(0);
        const result = Number.parseInt(val,36);

        // We ignore I and O because they are not used.
        if(result < 18) {
            return result;
        }
        if(result < 24) {
            return result-1;
        }
        return result-2;
    }

    static encodedValue(digit: number|undefined) {
        if(digit === undefined) {
            return '?';
        }
        if(digit >= 18) {
            ++digit;
        }
        if(digit >= 24) {
            ++digit;
        }
        return digit.toString(36).toUpperCase()
    }

    static parsePossibleInt(value: any): number {
        if(typeof value === 'number') {
            return value;
        } else if(typeof value === 'string') {
            return Number.parseInt(value);
        }
        return Number.NaN;
    }

    static hexToCoords(hex: string): HexType {
        const minuspos = hex.indexOf('-');
        if(minuspos >= 0) {
            return [Number.parseInt(hex.substring(0,2)), Number.parseInt(hex.substring(2,minuspos)), hex.substring(minuspos+1)];
        }
        return [Number.parseInt(hex.substring(0,2)), Number.parseInt(hex.substring(2,4))];
    }

    static coordsToHex(coords: HexType) : string {
        if(coords.length === 2 || coords[2] === '') {
            return `${this.zeroPad(coords[0])}${this.zeroPad(coords[1])}`;
        } else {
            return `${this.zeroPad(coords[0])}${this.zeroPad(coords[1])}-${coords[2]}`;
        }
    }

    static convertValue(value: number|string|undefined, digits: number) {
        if(value === undefined) {
            return value;
        }
        if(typeof value === 'number') {
            return this.zeroPad(value, digits);
        }
        return value.trim();
    }

    static applyOverride(sector: Sector, ovr: OverrideWorld): void {
        const def: any = {
            Name: ovr.name,
            Sector: sector.name,
            Hex: this.convertValue(ovr.hex,4),
            UWP: ovr.uwp,
            Bases: ovr.bases,
            Remarks: Array.isArray(ovr.notes) ? ovr.notes.join(' ') : ovr.notes,
            Zone: ovr.zone,
            PBG: this.convertValue(ovr.pbg, 3),
            Allegiance: ovr.allegiance,
            Stars: ovr.stars,
            '{Ix}': ovr.ix,
            '{Ex}': ovr.ex,
            '{Cx}': ovr.cx,
            Nobility: ovr.nobility,
            W: this.convertValue(ovr.w,1),
            RU: this.convertValue(ovr.ru,1),
            hasOverride: true,
        };
        if(!def.Hex) {
            return;
        }
        let world = sector.lookupWorld(def.Hex as string);
        if(world === undefined && !ovr.delete) {
           world = new World(sector, def);
           sector.addWorld(world);
           return;
        }
        if(ovr.delete) {
            sector.delWorld(world);
        } else {
            world.applyData(def);
        }
    }

}