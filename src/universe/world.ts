// Sector	SS	Hex	Name	UWP	Bases	Remarks	Zone	PBG	Allegiance	Stars	{Ix}	(Ex)	[Cx]	Nobility	W	RU
import {Sector} from "./sector.js";
import {Universe} from "../universe.js";
import {OverrideWorld} from "./override.js";

export class World {
    sector_: Sector;
    secName_: string;
    name_: string;
    sec_: [number, number];
    hex_: [number, number];
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

        if(typeof x === 'object') {
            this.hex_ = [World.parsePossibleInt(x.Hex.substring(0,2)), World.parsePossibleInt(x.Hex.substring(2))];
            this.name_ = '#' + this.sec + '-' + this.hex;
            this.applyData(x);
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

    protected applyData(x: Record<string,string>) {
        this.secName_ = x.Sector ?? this.secName_;
        this.name_ = x.Name ?? this.name_;
        this.uwp_ = x.UWP ?? this.uwp_;
        this.bases_ = x.Bases ?? this.bases_;
        this.notes_ = x.Remarks ? new Set(x.Remarks.split(/\s+/)) : this.notes_;
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

    get uwp(): string {
        return this.uwp_;
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

    get globalCoords(): [number, number] {
        return Universe.universeCoords({sx: this.sector_.x, sy: this.sector_.y, hx: this.x, hy: this.y})
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
        }
    }

    static digitValue(digit: string|undefined) {
        if(digit === undefined) {
            return undefined;
        }
        const val = digit.charAt(0);
        return Number.parseInt(val,36);
    }

    static parsePossibleInt(value: any): number {
        if(typeof value === 'number') {
            return value;
        } else if(typeof value === 'string') {
            return Number.parseInt(value);
        }
        return Number.NaN;
    }

    static hexToCoords(hex: string): [number, number] {
        return [Number.parseInt(hex.substring(0,2)), Number.parseInt(hex.substring(2,4))];
    }

    static coordsToHex(coords: [number, number]) : string {
        return coords.map(v => this.zeroPad(v)).join('');
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
        const def: Record<string,any> = {
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