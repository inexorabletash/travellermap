import {fluxAmountCalc, FluxRandom} from "../util.js";
import {Notes, World} from "./world.js";
import {OverrideWorld} from "./override.js";
import logger from "../logger.js";
import {processClassifications} from "./tables.js";

export type StellarBodyType=StellarBodyPlanet|StellarBodyStar|StellarBodyNoOrbit;

export type NotFunction<T,N> = T extends (...args: any[]) => any
    ? never
    : N;

export type FieldsOnly<T> = {
    [K in keyof T as NotFunction<T[K],K>]: T[K]
}
export type ExcludeFields<T,LL> = {
    [K in keyof T as Exclude<K,LL>]: T[K]
}
export type SBFields<T> = ExcludeFields<FieldsOnly<T>,'parentStar'|'orbits'|'driveLimits'|'suffix'|'factions'> & {orbitCnt: number};

export class StellarBody {
    name: string;
    orbit?: number;
    orbits: (undefined | StellarBodyType)[];
    primary: boolean;
    parent: StellarBodyType | undefined;
    worldGen: WorldGen;

    constructor(body: SBFields<StellarBody>) {
        this.name = body.name;
        this.orbits = Array<undefined | StellarBodyType>(body.orbitCnt);
        this.primary = body.primary;
        this.parent = body.parent;
        this.worldGen = body.worldGen;
    }

    get suffix(): string {
        let suffix = '';
        let sb: StellarBody|undefined = this;
        while(sb.parent) {
            suffix = `-${sb.orbit}${suffix}`;
            sb = sb.parent;
        }
        return suffix;
    }

    get parentStar(): StellarBodyStar | undefined {
        if (this.parent === undefined) {
            return undefined;
        }
        if (this.parent instanceof StellarBodyStar) {
            return this.parent;
        }
        return this.parent.parentStar;
    }

    setOrbit(index: number, to: StellarBodyStar | StellarBodyPlanet) {
        if (this?.orbits === undefined) {
            throw new Error(`setOrbit on ${this?.name} - no orbits`);
        }
        if (!Number.isInteger(index)) {
            throw new Error(`setOrbit on ${this.name} - ${index} isn't valid`);
        }
        if (index < 0 || index >= this.orbits.length) {
            throw new Error(`setOrbit on ${this.name} - ${index} is out of range (max ${this.orbits.length})`);
        }
        if (this.orbits[index] !== undefined) {
            throw new Error(`setOrbit on ${this.name} - ${index} already has something present`);
        }
        this.orbits[index] = to;
        to.orbit = index;

        if (to.star === undefined && to.uwp?.population && to.uwp?.govt !== 6) {
            this.worldGen.worlds.push(<StellarBodyPlanet>to);
        }
    }

    applyStellarNotes(orbit: number, uwp: UWPElements) {
    }
}

export class StellarBodyStar extends StellarBody {
    star: string;
    driveLimits: DriveLimitsStar;
    uwp?: undefined;

    constructor(body: SBFields<StellarBodyStar>) {
        super(body);
        this.star = body.star;

        this.driveLimits = StellarBodyStar.starLimits(this.star, this.parentStar?.driveLimits);

        // Fill precluded orbits
        for(let idx = 0; idx < this.driveLimits?.precluded; ++idx) {
            this.orbits[idx] = new StellarBodyNoOrbit(this.worldGen, this);
        }
    }

    setOrbit(index: number, to: StellarBodyStar|StellarBodyPlanet) {
        super.setOrbit(index, to);

        if(to.uwp) {
            this.applyStellarNotes(index, to.uwp);
        }
    }

    applyStellarNotes(orbit: number, uwp: UWPElements) {
        if (orbit < 2) {
            uwp.notes.add('Tz');
        }
        if (orbit === this.driveLimits.hz) {
            uwp.notes.add('Hz');
        }

        if(orbit > this.driveLimits.hz + 1) {
            uwp.notes.add('Fr');
        } else if(orbit == this.driveLimits.hz-1) {
            if(WorldGen.checkInCodeList(uwp.size ?? -1,'6789') &&
                WorldGen.checkInCodeList(uwp.atmosphere ?? -1,'456789') &&
                WorldGen.checkInCodeList(uwp.hydrographic ?? -1, '34567')) {
                uwp.notes.add('Tr');
            } else {
                uwp.notes.add('Ho');
            }
        } else if(orbit == this.driveLimits.hz+1) {
            if(WorldGen.checkInCodeList(uwp.size ?? -1,'6789') &&
                WorldGen.checkInCodeList(uwp.atmosphere ?? -1,'456789') &&
                WorldGen.checkInCodeList(uwp.hydrographic ?? -1, '34567')) {
                uwp.notes.add('Tu');
            } else {
                uwp.notes.add('Co');
            }
        } else if(orbit == this.driveLimits.hz) {
            uwp.notes.add('Hz');
        }

    }

    static starLimits(star: string, previous?: DriveLimitsStar) : DriveLimitsStar {
        const jdrive = WorldGen.starTable(star, WorldGen.JUMP_LIMIT_TABLE) ?? -1;
        let mdrive = jdrive;
        let gdrive = jdrive;
        if(jdrive >= 0) {
            while(gdrive > 0 && ORBIT_AUS[gdrive-1] > ORBIT_AUS[jdrive]/10) {
                --gdrive;
            }
            while(mdrive+1 < ORBIT_AUS.length && ORBIT_AUS[mdrive+1] < ORBIT_AUS[jdrive]*10) {
                ++mdrive;
            }
        }
        const precluded = WorldGen.starTable(star, WorldGen.PRECLUDED_ORBITS_TABLE) ?? -1
        return {
            mdrive: Math.max(previous?.mdrive ?? -1, mdrive),
            jdrive: Math.max(previous?.jdrive ?? -1, jdrive),
            gdrive: Math.max(previous?.gdrive ?? -1, gdrive),
            precluded,
            hz: WorldGen.habitableZone(star),
        };
    }
}

export class StellarBodyPlanet extends StellarBody {
    uwp: UWPElements;
    star?: undefined;
    factions: UWPElements[];

    constructor(body: SBFields<StellarBodyPlanet>) {
        super(body);
        this.uwp = body.uwp;
        this.factions = [];
    }

    setOrbit(index: number, to: StellarBodyStar|StellarBodyPlanet) {
        super.setOrbit(index, to);

        if(!to.uwp) {
            throw new Error(`Star added to planetary orbit?`);
        }
        this.applyStellarNotes(index, to.uwp);
    }

    addFaction(uwp: UWPElements) {
        this.factions.push(uwp);
    }

    applyStellarNotes(orbit: number, uwp: UWPElements) {
        this.parent?.applyStellarNotes(this.orbit ?? 0, uwp);
    }
}

export class StellarBodyNoOrbit extends StellarBody {
    uwp?: undefined;
    star?: undefined;

    constructor(worldGen: WorldGen, parent: StellarBodyStar) {
        super({
            worldGen,
            name: '***',
            orbitCnt: 0,
            primary: false,
            parent,
        });
    }
}


export type DriveLimitsStar = {
    mdrive: number,
    jdrive: number,
    gdrive: number
    precluded: number,
    hz: number,
};


export const ORBIT_AUS =
    [
        0.2,0.4,0.7,1.0,1.6,2.8,5.2,10,20,40,
        77,154,308,615,1230,2500,4900,9800,19500,39500,
        78700,150000, // anything beyond this is a parsec [206266]

    ];


export type UWPElements = {
    starport?: string,
    size?: number,
    atmosphere?: number,
    hydrographic?: number,
    population?: number,
    govt?: number,
    lawLevel?: number,
    techLevel?: number,
    notes: Set<string>,
    populationDigit?: number,
};

export type UWPDMs = {
    starport?: number,
    size?: number,
    atmosphere?: number,
    hydrographic?: number,
    population?: number,
    govt?: number,
    lawLevel?: number,
    techLevel?: number,
    sizeDice?: number,
    maxSize?: number,
    maxPopulation?: number,
    maxPopulationDigit?: number,
    notes?: string[],
};


export class WorldGen {
    gg: number;
    belts: number;
    planets: number;
    uwp: UWPElements;
    random: FluxRandom;
    stars: (string|undefined)[];
    system: StellarBodyStar;
    starBodies: StellarBodyStar[];
    mainWorld!: StellarBodyPlanet;
    worldIdx: number;
    worlds: StellarBodyPlanet[];

    constructor(protected world: World) {
        this.uwp = WorldGen.getUWP(world);
        const pbg = WorldGen.getPBG(world);
        this.planets = WorldGen.getPlanets(world);
        this.gg = pbg.gg;
        this.belts = pbg.belts;

        this.random = new FluxRandom(`PLANET-DETAILS/${world.sec}/${world.hex}`);

        this.stars = WorldGen.getStars(world);
        this.starBodies = this.stellarBodyForStars(this.random, world.name, this.stars);
        this.system = this.starBodies[0];
        this.worldIdx = 0;
        this.worlds = [];
    }

    static TECH_TYPES = [
        'Extraction',
        'Military',
        'Transportation',
        'LifeStyle',
        'Computer',
        'Medical',
        'Production/Food',
        'Production/Extraction',
        'Production/Military',
        'Production/Transportation',
        'Production/LifeStyle',
        'Production/Computer',
        'Production/Medical',
    ];

    static LAW_TYPES = [
        'Weapons',
        'Drugs',
        'Movement',
        'Gatherings',
        'Speech',
    ];



    static LUMINOSITY: Record<string,number> = {
        "Ia": 0,
        "Ib": 100,
        "II": 200,
        "III": 300,
        "IV": 400,
        "V": 500,
        "D": 600,
        "DM": 699,
    };

    static CLASSIFICATION: Record<string,number> = {
        O: 0,
        B: 10,
        A: 20,
        F: 30,
        G: 40,
        K: 50,
        M: 60,
    };

    static PRECLUDED_ORBITS_TABLE: Record<number, number> = {
        [this.starClassIndex('A0 Ia')]: 4,
        [this.starClassIndex('G0 Ia')]: 5,
        [this.starClassIndex('G5 Ia')]: 6,
        [this.starClassIndex('K5 Ia')]: 7,
        [this.starClassIndex('M0 Ia')]: 8,
        [this.starClassIndex('M5 Ia')]: 9,

        [this.starClassIndex('A0 Ib')]: 1,
        [this.starClassIndex('A5 Ib')]: 2,
        [this.starClassIndex('G5 Ib')]: 4,
        [this.starClassIndex('K0 Ib')]: 5,
        [this.starClassIndex('K5 Ib')]: 6,
        [this.starClassIndex('M0 Ib')]: 7,
        [this.starClassIndex('M5 Ib')]: 8,

        [this.starClassIndex('A0 II')]: 0,
        [this.starClassIndex('G0 II')]: 1,
        [this.starClassIndex('K0 II')]: 2,
        [this.starClassIndex('K5 II')]: 4,
        [this.starClassIndex('M0 II')]: 5,
        [this.starClassIndex('M5 II')]: 6,
        [this.starClassIndex('M9 II')]: 7,

        [this.starClassIndex('A0 III')]: 0,
        [this.starClassIndex('K5 III')]: 1,
        [this.starClassIndex('M0 III')]: 2,
        [this.starClassIndex('M5 III')]: 5,
        [this.starClassIndex('M9 III')]: 6,

        [this.starClassIndex('O0 IV')]: -1,
    };

    static HABITABLE_ZONE_TABLE = {
        [this.starClassIndex('O0 Ia')]: 15,
        [this.starClassIndex('B0 Ia')]: 13,
        [this.starClassIndex('A0 Ia')]: 12,
        [this.starClassIndex('F0 Ia')]: 11,
        [this.starClassIndex('G0 Ia')]: 12,
        [this.starClassIndex('K0 Ia')]: 12,
        [this.starClassIndex('M0 Ia')]: 12,

        [this.starClassIndex('O0 Ib')]: 15,
        [this.starClassIndex('B0 Ib')]: 13,
        [this.starClassIndex('A0 Ib')]: 11,
        [this.starClassIndex('F0 Ib')]: 10,
        [this.starClassIndex('G0 Ib')]: 10,
        [this.starClassIndex('K0 Ib')]: 10,
        [this.starClassIndex('M0 Ib')]: 11,

        [this.starClassIndex('O0 II')]: 14,
        [this.starClassIndex('B0 II')]: 12,
        [this.starClassIndex('A0 II')]: 9,
        [this.starClassIndex('F0 II')]: 9,
        [this.starClassIndex('G0 II')]: 9,
        [this.starClassIndex('K0 II')]: 9,
        [this.starClassIndex('M0 II')]: 10,

        [this.starClassIndex('O0 III')]: 13,
        [this.starClassIndex('B0 III')]: 11,
        [this.starClassIndex('A0 III')]: 7,
        [this.starClassIndex('F0 III')]: 6,
        [this.starClassIndex('G0 III')]: 7,
        [this.starClassIndex('K0 III')]: 8,
        [this.starClassIndex('M0 III')]: 9,

        [this.starClassIndex('O0 IV')]: 12,
        [this.starClassIndex('B0 IV')]: 10,
        [this.starClassIndex('A0 IV')]: 7,
        [this.starClassIndex('F0 IV')]: 6,
        [this.starClassIndex('G0 IV')]: 5,
        [this.starClassIndex('K0 IV')]: 5,
        [this.starClassIndex('M0 IV')]: 5,

        [this.starClassIndex('O0 V')]: 11,
        [this.starClassIndex('B0 V')]: 9,
        [this.starClassIndex('A0 V')]: 7,
        [this.starClassIndex('F0 V')]: 5,
        [this.starClassIndex('G0 V')]: 3,
        [this.starClassIndex('K0 V')]: 2,
        [this.starClassIndex('M0 V')]: 0,

        [this.starClassIndex('O0 VI')]: 3,
        [this.starClassIndex('B0 VI')]: 3,
        [this.starClassIndex('A0 VI')]: 3,
        [this.starClassIndex('F0 VI')]: 3,
        [this.starClassIndex('G0 VI')]: 2,
        [this.starClassIndex('K0 VI')]: 1,
        [this.starClassIndex('M0 VI')]: 0,

        [this.starClassIndex('O0 D')]: 1,
        [this.starClassIndex('B0 D')]: 0,
        [this.starClassIndex('DM')]: 0,
    }

    static JUMP_LIMIT_TABLE = {
        [this.starClassIndex('O0 Ia')]: 10,
        [this.starClassIndex('B0 Ia')]: 10,
        [this.starClassIndex('A0 Ia')]: 10,
        [this.starClassIndex('F0 Ia')]: 11,
        [this.starClassIndex('G0 Ia')]: 11,
        [this.starClassIndex('G5 Ia')]: 12,
        [this.starClassIndex('K0 Ia')]: 12,
        [this.starClassIndex('K5 Ia')]: 13,
        [this.starClassIndex('M0 Ia')]: 14,
        [this.starClassIndex('M5 Ia')]: 15,

        [this.starClassIndex('O0 Ib')]: 9,
        [this.starClassIndex('B0 Ib')]: 9,
        [this.starClassIndex('A0 Ib')]: 9,
        [this.starClassIndex('F0 Ib')]: 9,
        [this.starClassIndex('G0 Ib')]: 10,
        [this.starClassIndex('K0 Ib')]: 11,
        [this.starClassIndex('K5 Ib')]: 12,
        [this.starClassIndex('M0 Ib')]: 13,
        [this.starClassIndex('M5 Ib')]: 14,
        [this.starClassIndex('M9 Ib')]: 15,

        [this.starClassIndex('O0 II')]: 7,
        [this.starClassIndex('B0 II')]: 7,
        [this.starClassIndex('A0 II')]: 7,
        [this.starClassIndex('F0 II')]: 7,
        [this.starClassIndex('G0 II')]: 8,
        [this.starClassIndex('K0 II')]: 9,
        [this.starClassIndex('K5 II')]: 10,
        [this.starClassIndex('M0 II')]: 11,
        [this.starClassIndex('M5 II')]: 13,

        [this.starClassIndex('O0 III')]: 6,
        [this.starClassIndex('B0 III')]: 6,
        [this.starClassIndex('A0 III')]: 6,
        [this.starClassIndex('A5 III')]: 5,
        [this.starClassIndex('F0 III')]: 5,
        [this.starClassIndex('G0 III')]: 6,
        [this.starClassIndex('G5 III')]: 7,
        [this.starClassIndex('K0 III')]: 7,
        [this.starClassIndex('K5 III')]: 9,
        [this.starClassIndex('M0 III')]: 9,
        [this.starClassIndex('M5 III')]: 11,
        [this.starClassIndex('M9 III')]: 12,

        [this.starClassIndex('O0 IV')]: 5,
        [this.starClassIndex('B0 IV')]: 5,
        [this.starClassIndex('A0 IV')]: 5,
        [this.starClassIndex('A5 IV')]: 4,
        [this.starClassIndex('F0 IV')]: 4,
        [this.starClassIndex('G0 IV')]: 5,
        [this.starClassIndex('K0 IV')]: 5,
        [this.starClassIndex('M0 IV')]: 5,

        [this.starClassIndex('O0 V')]: 5,
        [this.starClassIndex('B0 V')]: 5,
        [this.starClassIndex('A0 V')]: 5,
        [this.starClassIndex('A5 V')]: 4,
        [this.starClassIndex('F0 V')]: 3,
        [this.starClassIndex('G0 V')]: 2,
        [this.starClassIndex('K0 V')]: 2,
        [this.starClassIndex('K5 V')]: 1,
        [this.starClassIndex('M0 V')]: 1,
        [this.starClassIndex('M5 V')]: 0,
        [this.starClassIndex('M9 V')]: -1,

        [this.starClassIndex('O0 VI')]: 3,
        [this.starClassIndex('B0 VI')]: 3,
        [this.starClassIndex('A0 VI')]: 3,
        [this.starClassIndex('F0 VI')]: 3,
        [this.starClassIndex('G0 VI')]: 2,
        [this.starClassIndex('G5 VI')]: 1,
        [this.starClassIndex('K0 VI')]: 0,
        [this.starClassIndex('M0 VI')]: 0,
        [this.starClassIndex('M5 VI')]: -1,

        [this.starClassIndex('O0 D')]: -1,
    }


    static starTable<T>(star: string, table: Record<number,T>): T|undefined {
        const tableData = <Record<number,T> & Record<'Index',number[]>>table;
        const starIdx = this.starClassIndex(star);
        if(tableData.Index === undefined) {
            tableData.Index = Object.keys(tableData).map(k => Number.parseInt(k)).filter(k => Number.isInteger(k)).sort((a,b) => a-b);
        }

        for(let idx = tableData.Index.length-1; idx>=0; --idx) {
            if(tableData.Index[idx] <= starIdx) {
                return tableData[tableData.Index[idx]];
            }
        }
        return undefined;
    }

    static starClassIndex(star: string) {
        const spaceIndex = star.indexOf(' ');
        if(spaceIndex < 0) { // A dwarf - may use something more explicit if we have more than just this but for now this will do
            return this.LUMINOSITY['DM'];
        }
        const type = this.CLASSIFICATION[star.substring(0,1)];
        if(type === undefined) {
            return this.LUMINOSITY['DM'];
        }
        const subType = Math.min(9,Math.max(0,star.charCodeAt(1)-48));
        const luminosity = this.LUMINOSITY[star.substring(spaceIndex+1)] ?? this.LUMINOSITY['V'];

        return type + subType + luminosity;
    }

    static enrichType(random: FluxRandom, typeKey: string, list: string[], keyValue: number|undefined, fluxFraction: number, recurseFluxFraction: number) {
        if(keyValue === undefined) {
            return [];
        }
        const result=[];
        for(const enrichvalue of list) {
            const flux = fluxAmountCalc(random, fluxFraction, recurseFluxFraction);
            const value = Math.max(keyValue + flux, 0) - keyValue;
            if(value != 0) {
                result.push(`${typeKey}:${enrichvalue}:${value>0?'+':''}${value}`);
            }
        }
        return result;
    }

    static getPBG(world: World): {pop: number, belts: number, gg: number} {
        let pbg = world.pbg;
        let result;
        if(pbg) {
            result = {
                pop: World.digitValue(pbg.substring(0, 1)),
                belts: World.digitValue(pbg.substring(1, 2)),
                gg: World.digitValue(pbg.substring(2, 3)),
            };
        }
        const random = new FluxRandom(`PBG/${world.sec}/${world.hex}`);
        return {
            pop: result?.pop ?? random.die(9),
            belts: result?.belts ?? Math.max(0,random.die()-3),
            gg: result?.gg ?? Math.max(0,Math.floor(random.die(6,2)/2)-2)
        }
    }

    static getUWP(world: World): UWPElements {
        const uwp = world?.uwp;

        // TODO - deal with missing stuff
        return this.extractUWP(uwp, world?.notes, world?.pbg);
    }

    static extractUWP(uwp: string, notes: Set<string>, pbg?: string): UWPElements {
        const base = {
            starport: uwp.substring(0,1) ?? 'X',
            size: World.digitValue(uwp?.substring(1,2)) ?? 0,
            atmosphere: World.digitValue(uwp?.substring(2,3)) ?? 0,
            hydrographic: World.digitValue(uwp?.substring(3,4)) ?? 0,
            population: World.digitValue(uwp?.substring(4,5)) ?? 0,
            govt: World.digitValue(uwp?.substring(5,6)) ?? 0,
            lawLevel: World.digitValue(uwp?.substring(6,7)) ?? 0,
            techLevel: World.digitValue(uwp?.substring(8,9)) ?? 0,
            notes: notes ?? new Set<string>(),
            populationDigit: World.digitValue(pbg?.substring(0,1)) ?? 1,
        };
        return base;
    }

    static getPlanets(world: World) {
        const planets = world.planets;
        if(planets) {
            return planets;
        }
        const random = new FluxRandom(`PLANETS/${world.sec}/${world.hex}`);
        const pbg = this.getPBG(world);
        return 1 + pbg.belts + pbg.gg + random.die(6,2);
    }

    static getStars(world: World): (string|undefined)[] {
        const stars = world.stars;
        if(!stars) {
            // TODO: stargen - I don't think we need this case at all yet.
            const random = new FluxRandom(`STARS/${world.sec}/${world.hex}`);
            return ['M0 V', undefined, undefined, undefined, undefined, undefined, undefined, undefined ];
        }
        if(stars.length === 8) {
            return stars;
        }

        // In this case we have a load of stars but don't know what slot they are in.  Here we randomly generate
        // the slots given that each slot has equal weight, however, stars are consumed in order.
        const random = new FluxRandom(`STARS/${world.sec}/${world.hex}`);
        const result = [stars[0], undefined, undefined, undefined, undefined, undefined, undefined, undefined];
        const pickedSlots = [true, false, false, false, false, false, false, false];
        for(let nstars = stars.length-1; nstars > 0;) {
            const slot = (random.die(4)-1)*2;

            if(pickedSlots[slot]) {
                if(pickedSlots[slot+1]) {
                    continue;
                }
                pickedSlots[slot+1] = true;
            } else {
                pickedSlots[slot] = true;
            }

            --nstars;
        }
        let star = 1;
        for(let idx = 1; idx < pickedSlots.length; ++idx) {
            if(pickedSlots[idx]) {
                result[idx] = stars[star++];
            }
        }
        for(let idx = 0; idx < 8; idx += 2) {
            if(result[idx] && result[idx+1] && this.starClassIndex(result[idx] ?? '') < this.starClassIndex(result[idx+1] ?? '')) {
                const x = result[idx+1];
                result[idx+1] = result[idx];
                result[idx] = x;
            }
        }
        //console.log(`${world.name}: [${stars}] [${result}] [${pickedSlots}]`);

        return result;
    }


    static writeStars(stars: string[]) {
        let result = '';
        for(let idx = 0; idx < 8; idx += 2) {
            if(stars[idx]) {
                result += stars[idx];
                if(stars[idx+1]) {
                    result += ' - '+stars[idx+1];
                }
            }
            result += ' / ';
        }
        return result;
    }

    static checkInCodeList(value: number|undefined, codeList: string): boolean {
        const code = World.encodedValue(value??-1);
        return codeList.indexOf(code) >= 0;
    }

    static maxOrbitsForOrbit(orbit: number) {
        let maxOrbits = orbit - 2;
        if(maxOrbits == 0) {
            maxOrbits = 1;
        } else if(maxOrbits < 0) {
            maxOrbits = 0;
        }
        return maxOrbits;
    }

    static worldSizeMult(worldSize: number|undefined) {
        worldSize ??= 0;
        const szMult = worldSize <= 20 ? Math.max(1,worldSize) : ([20,30,40,50,60,70,80,90,125,180,220,250][worldSize-20]);
        return szMult;
    }

    static maxSatelliteOrbitsForOrbit(orbit: number, worldSize: number|undefined) {
        const szMult = this.worldSizeMult(worldSize);
        const maxStellarOrbit = this.maxOrbitsForOrbit(orbit);
        const maxAu = ORBIT_AUS[Math.min(ORBIT_AUS.length-1, maxStellarOrbit)];
        const maxMm = maxAu * 1.5e5;

        // Assuming orbits are linearly spaced....
        const orbitLimit = Math.trunc(Math.log10(maxMm / szMult / 7));
        //console.log(`MSOFO o=${orbit} sz=${worldSize} szm=${szMult} ${maxStellarOrbit} ${maxAu} ${maxMm} ${orbitLimit}`);
        return Math.min(Math.max(0, orbitLimit),24);
        // max will be < 5k for orbit 24 (base is 0 == A)
        //5k * 250 / 15000 => 1/3*250 => 85 au which is O==11 which given maxOrbits rule is O==13

        // AU = Mm/15000
        //const orbit
    }

    static habitableZone(star: string) : number {
        return this.starTable(star, this.HABITABLE_ZONE_TABLE) ?? 0;
    }

    // FIXME: populate as we create

    static primaryOrbit(w: World, star: string, starDm: number) {
        const uwp = this.getUWP(w);
        if(uwp.size === 0) {
            return -1;
            // asteroid belt
        }

        // MW is satelite?
        let habitableZone = this.habitableZone(star) + starDm;
        if(w.notes.has('Tr') || w.notes.has('Ho')) {
            ++habitableZone;
        } else if(w.notes.has('Tu') || w.notes.has('Co')) {
            --habitableZone;
        } else if(w.notes.has('Fr')) {
            habitableZone -= 2;
        }
        return Math.max(0,habitableZone);
    }

    static minOrbit(body: StellarBody) {
        return body.orbits?.findIndex(orbit => orbit == undefined) ?? -1;
    }

    static maxOrbit(body: StellarBody) {
        return body.orbits?.findLastIndex(orbit => orbit == undefined) ?? -1;
    }

    static hasAvailableOrbits(body: StellarBody) {
        return this.maxOrbit(body) != -1;
    }

    static orbitalPosition(body: StellarBodyStar|StellarBodyPlanet, target: number): number {
        const base = Math.max(Math.min(target, this.maxOrbit(body)),this.minOrbit(body));
        let pos = base;

        while(body.orbits[pos]) {
            ++pos;
        }

        return pos;
    }

    stellarBodyForStar(name: string, star: string, maxOrbits: number, previous?: StellarBodyStar): StellarBodyStar {
        const disallowed = WorldGen.starTable(star, WorldGen.PRECLUDED_ORBITS_TABLE) ?? -1;
        const result = new StellarBodyStar({
                worldGen: this,
                name: name,
                parent: previous,
                star,
                orbitCnt: maxOrbits,
                //driveLimits: WorldGen.starLimits(star, previous?.driveLimits),
                primary: false,
            }
        );
        return result;
    }

    addBodyForStar(name: string, star: string, parent: StellarBodyStar, target: number, bodyList: StellarBodyStar[]) {
        const orbit = WorldGen.orbitalPosition(parent, target);
        const body = this.stellarBodyForStar(`${name}-${orbit}`, star, WorldGen.maxOrbitsForOrbit(orbit), parent);
        parent.setOrbit(orbit, body);
        return body;
    }

    stellarBodyForStars(random: FluxRandom, name: string, stars: (string|undefined)[]): StellarBodyStar[] {
        const rv: StellarBodyStar[] = [
            this.stellarBodyForStar(name, stars[0]??'', 20),
        ];
        if(stars[1]) {
            this.addBodyForStar(rv[0].name, stars[1], rv[0], 0, rv);
        }

        for(let pos = 2; pos < stars.length; pos += 2) {
            if(stars[pos]) {
                const orbit = random.die(6) + pos * 3 - 6;
                const parent = this.addBodyForStar(rv[0].name, <string>stars[pos], rv[0], orbit, rv);
                rv.push(parent);

                if(stars[pos+1]) {
                    this.addBodyForStar(parent.name, <string>stars[pos+1], parent, 0, rv);
                }
            }

        }
        return rv;
    }

    static encodeUwpElements(uwp: UWPElements): string {
        return `${uwp.starport}${World.encodedValue(uwp.size)}${World.encodedValue(uwp.atmosphere)}${World.encodedValue(uwp.hydrographic)}` +
            `${World.encodedValue(uwp.population)}${World.encodedValue(uwp.govt)}${World.encodedValue(uwp.lawLevel)}` +
            `-${World.encodedValue(uwp.techLevel)}`;
    }

    stellarBodyForPlanet(name: string, maxOrbits: number, uwp: UWPElements, parent: StellarBodyType): StellarBodyPlanet {
        return new StellarBodyPlanet({
            worldGen: this,
            name,
            parent,
            orbitCnt: maxOrbits,
            uwp,
            primary: false,
        });
    }

    static makeGasGiant(random: FluxRandom) {
        const size = random.die(6,2)+19;

        return {
            starport: 'X',
            size,
            atmosphere: 14,
            hydrographic: 0,
            population: 0,
            govt: 0,
            lawLevel: 0,
            techLevel: 0,
            notes: new Set<string>(['GG']),
        };

    }

    static makeHydrographic(random: FluxRandom, size: number, atmosphere: number, dm: number) {
        let base = atmosphere + random.flux() + dm;
        if(size < 2) {
            return 0;
        }
        if(atmosphere < 2 || atmosphere > 9) {
           base -= 4;
        }
        return Math.max(0,Math.min(10,base));
    }

    static makePopulation(random: FluxRandom, dms: UWPDMs): { population: number, populationDigit: number} {
        let population = Math.max(0,random.die(6,2)-2 + (dms.population ?? 0));
        if(population == 10) {
            population = random.die(6,2)+3;
        }
        return {
            population: Math.min(dms.maxPopulation??99, population),
            populationDigit: population > 0 ? population === dms.maxPopulation ? random.die(dms.maxPopulationDigit) : random.die(9) : 0,
        }
    }

    static makeTL(random: FluxRandom, starport: string, size: number, atm: number, hyd: number, pop: number, gov: number, dm: number): number {
        if(starport == 'A') {
            dm += 6;
        } else if(starport == 'B') {
            dm += 4;
        } else if(starport == 'C') {
            dm += 2;
        } else if(starport == 'X') {
            dm -= 4;
        } else if(starport == 'F') {
            dm += 1;
        }
        if(size < 2) {
            dm += 2;
        } else if(size < 5) {
            dm += 1;
        }

        if(atm < 4 || atm > 9) {
            dm += 1;
        }

        if(pop == 0) {
            return 0;
        } else if(pop < 6) {
            dm += 1;
        } else if(pop > 9) {
            dm += 4;
        } else if(pop == 9) {
            dm += 2;
        }

        if(gov == 0 || gov == 5) {
            dm += 1;
        } else if(gov == 13) {
            dm -= 2;
        }

        return Math.max(0, random.die(6,2) + dm);
    }

    static makeStarport(random: FluxRandom, dm: number, maxStarport: string|undefined, spaceportVsStarportDm: number = 0) {
        // We are possibly making a spaceport.
        if(maxStarport) {
            if(maxStarport < 'F' && (random.die()+spaceportVsStarportDm) >= 2) {
                // Generate a spaceport. 2:3 normally if not primary, but more chance if faction
                const spRoll = random.die() + (dm??0);
                const starport = spRoll >= 4 ? 'F' : spRoll == 3 ? 'G' : spRoll>0 ? 'H' : 'X';
                return starport;
            }
        }
        let sp = '0';
        while(sp < (maxStarport ?? 'A')) {
            const roll = random.die(6,2);
            if(roll < 5) {
                sp = 'A';
            } else if(roll < 7) {
                sp = 'B';
            } else if(roll < 9) {
                sp = 'C';
            } else if(roll < 10) {
                sp = 'D';
            } else if(roll < 12) {
                sp = 'E';
            } else {
                sp = 'X';
            }
        }
        return sp;
    }

    static makePhysicalWorld(random: FluxRandom, dms: UWPDMs) : UWPElements {
        const size = Math.min(dms.maxSize ?? 24, Math.max(0,random.die(6,dms.sizeDice??2)+(dms.size ?? 0)));
        const atmosphere = Math.max(0,Math.min(15,size + random.flux() + (dms.atmosphere??0)));
        const hydrographic = WorldGen.makeHydrographic(random, size, atmosphere, dms.hydrographic??0);

        return {
            size,
            atmosphere,
            hydrographic,
            notes: new Set(dms.notes),
        };
    }

    static makeEmptyWorld(random: FluxRandom, dms: UWPDMs) : UWPElements {
        return {
            ...this.makePhysicalWorld(random, dms),
            starport: 'X',
            population: 0,
            govt: 0,
            lawLevel: 0,
            techLevel: 0,
        }
    }

    determineWorldOwner(world: StellarBodyPlanet) : string|undefined {
        return [ ...world.uwp.notes ].find(n => n.startsWith('O:'));
    }

    determineColonyOwner(random: FluxRandom, exclude: StellarBodyPlanet[] = []): undefined|{ world: StellarBodyPlanet, owner: string } {
        if(this.worlds.length - (exclude !== undefined ? exclude.length : 0) < 1) {
            return undefined;
        }
        if(random.die(4+this.worlds.length) < 4) {
            while(true) {
                //console.log(`Creating colony in ${bodyIn.name}-${orbitIn}`);
                const worldDie = random.die(this.worlds.length) - 1;
                const world = this.worlds[worldDie];

                if(exclude.find(x => x === world)) {
                    continue;
                }
                const primaryOwner = world ? this.determineWorldOwner(world) : undefined;
                const owner = primaryOwner ? primaryOwner : `O:${this.world.hex}${world.suffix}`;

                return {
                    world,
                    owner,
                }
            }
        } else {
            //console.log(`Creating world at ${bodyIn.name}-${orbitIn}`);
            return undefined
        }
    }

    createFaction(random: FluxRandom, world: StellarBodyPlanet, owner: StellarBodyPlanet|undefined, ownerName: string|undefined, isPrimary: boolean, isBestStarport: boolean) {
        const notes:string[] = [];
        const elements = {
            size: world.uwp.size,
            atmosphere: world.uwp.atmosphere,
            hydrographic: world.uwp.hydrographic,
        };

        if(ownerName) {
            notes.push(ownerName);
            if(!world.primary && world.uwp.notes.has(ownerName)) {
                notes.push('Cy');
            }
        }

        let {starport, population, populationDigit, govt, lawLevel, techLevel}:
            {
                starport: string,
                population: number,
                populationDigit: number,
                govt: number,
                lawLevel: number,
                techLevel: number
            } = <any>world.uwp;
        // The primary world inherits population, TL and Law-level
        if (isPrimary) {
            const popVars =
                WorldGen.makePopulation(random, {
                    maxPopulation: world.uwp.population,
                    maxPopulationDigit: world.uwp.populationDigit,
                });
            population = popVars.population;
            populationDigit = popVars.population;
            techLevel += fluxAmountCalc(random, 5, 1);
            lawLevel = population == 0 ? 0 : Math.max(0, Math.min(18, govt + random.flux()));
        }

        if (!isBestStarport) {
            starport = WorldGen.makeStarport(random, 0, world.uwp.starport, -2);
        }

        // Generate a new government type - this cannot be a balkanized govt.  However if we reroll balkanized
        // we take it as an indicator of local unrest.
        let iterations = 0;
        while (govt == 7) {
            govt = (population == 0 ? 0 : Math.max(0, Math.min(15, (population ?? 0) + random.flux())));
            ++iterations;
        }
        if(iterations > 1) {
            notes.push(Notes.UNREST);
        }

        const uwp: UWPElements = {
            ...elements,
            notes: new Set<string>(notes),
            starport, population, populationDigit, govt, lawLevel, techLevel
        };
        world.addFaction(uwp);
        WorldGen.enrichNotes(random, uwp);
        processClassifications(world, uwp, this.mainWorld?.uwp ?? this.uwp);
    }


    createFactions(random: FluxRandom, world: StellarBodyPlanet) {
        let factions = 2 + (random.die()-1)%4;
        let bestStarportFaction = random.die(factions);
        const knownFactions: StellarBodyPlanet[] = [];

        for(let faction = 1; faction <= factions; ++faction) {
            const subRandom = random.sub(`Faction=${faction}`);
            let owner;
            if(faction == 1) {
                owner = {
                    world,
                    owner: this.determineWorldOwner(world),
                }
            } else {
                owner = this.determineColonyOwner(subRandom, knownFactions)
            }
            knownFactions.push(world);
            this.createFaction(subRandom, world, owner?.world, owner?.owner, faction == 1, faction == bestStarportFaction);
        }
    }


    postProcessWorld(random: FluxRandom, world: StellarBodyPlanet) {
        processClassifications(world, world.uwp, this.mainWorld?.uwp ?? this.uwp);

        if(world.uwp.govt == 7) { // Balkanisation
            const subRandom = random.sub('balkans');
            this.createFactions(subRandom, world)
        } else {
            WorldGen.enrichNotes(random, world.uwp);
        }

    }

    makeWorldFromWorld(random: FluxRandom, from: StellarBodyPlanet, primaryOwner: string, dms: UWPDMs): UWPElements {
        const elements = WorldGen.makePhysicalWorld(random, dms);

        // Logic ([pop-size]/2+1 >= 1d*) then captive otherwise inherit.
        let { population, populationDigit } = WorldGen.makePopulation(random, {...dms, maxPopulation: from.uwp.population, maxPopulationDigit: from.uwp.populationDigit ?? 9 });
        let govt = 0;
        let techLevel = 0;
        let lawLevel = 0;
        //let populationDigit = 0;
        let starport = 'X';
        if (population > 0) {
            let die = random.die();
            let roll = 0;
            const faction = from.factions.length > 0 ? random.die(from.factions.length)-1 : -1;
            const fromUwp = faction >= 0 ? from.factions[faction] : from.uwp;
            const suffix = from.suffix + (faction >= 0 ? String.fromCharCode(65+faction): '');
            do {
                die = random.die();
                roll += die;
            } while (die == 6);

            if (roll < 1 + Math.floor(population / 2)) {
                govt = fromUwp.govt ?? 0;
                techLevel = (fromUwp.techLevel ?? 0) + fluxAmountCalc(random, 2, 1);
                lawLevel = (fromUwp.techLevel ?? 0) + fluxAmountCalc(random, 5, 5);
                starport = WorldGen.makeStarport(random, dms.starport??0, this.uwp.starport);
                elements.notes.add('Cy');
            } else {
                govt = 6; // Captive / colony
                techLevel = fromUwp.techLevel ?? 0;
                lawLevel = fromUwp.lawLevel ?? 0;
                starport = WorldGen.makeStarport(random, dms.starport??0, 'E');
                elements.notes.add('Cy');
            }
            if(primaryOwner) {
                elements.notes.add(primaryOwner);
            }
            if(techLevel < 8 && techLevel-5>=random.die()) {
                techLevel = 0;
                lawLevel = 0;
                starport = 'X';
                population = 0;
                populationDigit = 0;
                elements.notes.add('Re');
            }
        } else {
            elements.notes.add('Re');
        }

        return {
            ...elements,
            starport,
            population,
            govt,
            lawLevel,
            techLevel,
            populationDigit,
        }
    }


    makeWorldCore(random: FluxRandom, dms: UWPDMs={}): UWPElements {
        const elements = WorldGen.makePhysicalWorld(random, dms);
        const starport = WorldGen.makeStarport(random, dms.starport??0, this.uwp.starport, -2)
        const {population,populationDigit} = WorldGen.makePopulation(random, dms);
        const govt = population == 0 ? 0 : Math.max(0, Math.min(15, population + random.flux() + (dms.population??0)));
        const lawLevel = population == 0 ? 0 : Math.max(0, Math.min(18, govt + random.flux() + (dms.lawLevel??0)));
        const techLevel = WorldGen.makeTL(random, starport, elements.size??0, elements.atmosphere??0, elements.hydrographic??0, population, govt, dms.techLevel ?? 0);
        return {
            ...elements,
            starport,
            population,
            govt,
            lawLevel,
            techLevel,
            populationDigit,
        };
    }


    makeWorld(bodyIn: StellarBodyPlanet |StellarBodyStar, orbit: number, dms: UWPDMs={}): UWPElements {
        let body: StellarBodyStar;
        const orbitIn = orbit;
        const random = this.random.sub(`${bodyIn.name}-${orbitIn}`);
        if(bodyIn.star) {
            body = <StellarBodyStar>bodyIn;
        } else {
            body = <StellarBodyStar>bodyIn.parentStar;
            orbit = body.orbits.findIndex(b => b === bodyIn);
        }
        //const body: StellarBodyStar = <StellarBodyStar>(bodyIn.star ? bodyIn : bodyIn.parentStar);
        // If we are outside the m-drive range it is really unlikely to see a populated world
        const randomWt = this.random.sub('world-type');
        if(orbit > body.driveLimits.mdrive) {
            if(randomWt.die(10)<10) {
                //console.log(`Creating empty world at ${bodyIn.name}-${orbitIn}`);
                return WorldGen.makeEmptyWorld(random, dms);
            }
            //console.log(`${bodyIn.name}-${orbitIn} is outside mdrive range`);
        }

        if(dms.maxPopulation === 0) {
            //console.log(`Creating empty world at ${bodyIn.name}-${orbitIn}`);
            return WorldGen.makeEmptyWorld(random, dms);
        } else {
            const owner = this.determineColonyOwner(randomWt);
            if(owner) {
                return this.makeWorldFromWorld(random, owner.world, owner.owner, dms);
            } else {
                return this.makeWorldCore(random, dms);
            }
        }

    }

    nextStar(): StellarBodyStar {
        while(true) {
            const body = this.starBodies[this.worldIdx++ % this.starBodies.length];
            if(WorldGen.minOrbit(body) <= WorldGen.maxOrbit(body)) {
                return body;
            }
        }
    }

    placeGG(random: FluxRandom) {
        const body = this.nextStar();

        // Note we haven't done ice giants here....

        const hz = WorldGen.habitableZone(body.star);
        const gg = WorldGen.makeGasGiant(random);
        const offset = (gg.size > 22) ? -5 : -4;
        const posOffset = random.die(6,2)+offset;
        const ggPos = WorldGen.orbitalPosition(body, Math.max(0,hz+posOffset));
        if(ggPos < 0) {
            logger.error(`Unable to place world on ${body.name}`);
            return;
        }
        body.setOrbit(ggPos,this.stellarBodyForPlanet(`${body.name}-${ggPos}`, WorldGen.maxSatelliteOrbitsForOrbit(ggPos, gg.size), gg,body));
        --this.gg;
        --this.planets;
    }

    placeBelt(random: FluxRandom, isPrimary = false): StellarBodyPlanet|undefined {
        const body = this.nextStar();

        const hz = WorldGen.habitableZone(body.star);
        const posOffset = random.die(6,2)-3;
        const pos = WorldGen.orbitalPosition(body, Math.max(0,hz+posOffset));
        if(pos < 0) {
            logger.error(`Unable to place world on ${body.name}`);
            // Don't decrement planets ... we will try again
            return undefined;
        }
        const belt = isPrimary ? this.uwp : this.makeWorld(body, pos,
            { size:-100, notes: ['As'], maxPopulation: this.uwp.population ?? 0, maxPopulationDigit: this.uwp.populationDigit ?? 1,});
        const beltBody = this.stellarBodyForPlanet(`${body.name}-${pos}`, 0, belt, body)
        body.setOrbit(pos, beltBody);
        this.postProcessWorld(random, beltBody);
        --this.belts;
        --this.planets;
        return beltBody;
    }

    uwpDMs(posOffset: number, isSatellite: boolean): UWPDMs {
        const DM_Inferno = { sizeDice: 1, size: 6, notes: ['Inf']};
        const DM_Inner = {population: -4, hydrographic: -4, notes: ['Inner']};
        const DM_Big = {size: 8, notes: ['Big']};
        const DM_Storm = {atmosphere: 4, hydrographic: -4, population: -6, notes: ['Storm']};
        const DM_Rad = { notes: ['Rad']};
        const DM_Hospitable = { notes: ['Hsp']};
        const DM_Worldlet = {sizeDice: 1, size: -3, notes: ['Wlt']};
        const DM_Ice = {population: -6, notes: ['Ice']};

        const type = this.random.die();
        if(posOffset <= 0) {
            return [DM_Inferno, DM_Inner, DM_Big, DM_Storm, DM_Rad, DM_Hospitable][type-1];
        } else {
            return [DM_Worldlet, DM_Ice, DM_Big, isSatellite ? DM_Storm : DM_Ice, DM_Rad, DM_Ice][type-1];
        }
    }

    placeWorld(random: FluxRandom) {
        let body;
        let posOffset;

        if(this.planets === 1) {
            body = this.system;
            posOffset = this.random.die(6,2)+5;
        } else {
            body = this.nextStar();
            posOffset = [10,8,6,4,2,0,1,3,5,7,9][random.die(6,2)-2];
        }
        const hz = WorldGen.habitableZone(body.star);
        const pos = WorldGen.orbitalPosition(body, hz+posOffset);
        if(pos < 0) {
            logger.error(`Unable to place world on ${body.name}`);
            // Don't decrement planets ... we will try again
            return;
        }

        let dms: UWPDMs = this.uwpDMs(posOffset, false);

        const world = this.makeWorld(body, pos, { ...dms, maxPopulation: this.uwp.population ?? 0, maxPopulationDigit: this.uwp.populationDigit ?? 1,});
        const newBody = this.stellarBodyForPlanet(`${body.name}-${pos}`, WorldGen.maxSatelliteOrbitsForOrbit(pos, world.size), world, body);
        body.setOrbit(pos, newBody);
        this.postProcessWorld(random, newBody);
        --this.planets;
    }

    satelliteOrbit(random: FluxRandom, orbitIdx: number, orbit: StellarBodyPlanet) {
        const orbits = WorldGen.maxSatelliteOrbitsForOrbit(orbitIdx, orbit.uwp.size);
        if(orbits <= 0) {
            return -1;
        }
        let target = orbits;

        while(target >= orbits) {
            const md = Math.floor((orbits - 5) / 3) - 1;
            const die = md <= 1 ? 0 : (random.die(md) - 1);
            const dm = die * 3 + 5;
            target = random.flux() + dm;
        }
        return target;
    }

    placeSatellite(orbitIdx: number, body: StellarBodyPlanet, star: StellarBodyStar) {
        const random = this.random.sub(`satelite/${body.name}/${orbitIdx}`);
        if(!WorldGen.hasAvailableOrbits(body)) {
            //console.log(`${body.name} - placeSatelite - no orbits`);
            return;
        }
        const target = WorldGen.orbitalPosition(body, this.satelliteOrbit(random, orbitIdx, body));

        const hz = WorldGen.habitableZone(star.star);
        let dms: UWPDMs = this.uwpDMs(target-hz, true);
        const maxSize = body.uwp.size;

        const world = this.makeWorld(body, target, { ...dms, maxSize, maxPopulation: this.uwp.population ?? 0, maxPopulationDigit: this.uwp.populationDigit ?? 1,})
        const newBody = this.stellarBodyForPlanet(`${body.name}-${target}`, 0, world, body);
        body.setOrbit(target, newBody);
        this.postProcessWorld(random, newBody);
    }

    placeRing(orbitIdx: number, body: StellarBodyPlanet) {
        const random = this.random.sub(`ring/${body.name}/${orbitIdx}`);
        if(!WorldGen.hasAvailableOrbits(body)) {
            //console.log(`${body.name} - placeRing - no orbits`);
            return;
        }
        const target = WorldGen.orbitalPosition(body, this.satelliteOrbit(random, orbitIdx, body));

        const world = {
            starport: 'X',
            size: 0,
            atmosphere: 1,
            hydrographic: 0,
            population: 0,
            govt: 0,
            lawLevel: 0,
            techLevel: 0,
            notes: new Set<string>(['Ring']),
        };
        body.setOrbit(target, this.stellarBodyForPlanet(`${body.name}-${target}`, 0, world, body));
    }


    addPrimaryWorld() {
        let primarySystem = this.system;
        let primaryOrbit = WorldGen.primaryOrbit(this.world, this.stars[0] ??'', 0);
        const random = this.random.sub('primary');

        if(this.system.orbits[primaryOrbit]) {
            // If the primary orbit is occupied by a star we have two options.  Firstly we will try to put the primary
            // in some orbit of the companion star.
            const newPrimaryOrbit = WorldGen.orbitalPosition(
                <StellarBodyStar>this.system.orbits[primaryOrbit],WorldGen.primaryOrbit(this.world, this.stars[0] ??'', 0));
            if(newPrimaryOrbit < 0) {
                // If we can't do that, we will just adjust the original with the orbitalPosition method
                primaryOrbit = WorldGen.orbitalPosition(this.system, primaryOrbit);
                logger.error(`${this.world.name}: primary orbit conflicts with star - moving`);
            } else {
                primarySystem = <StellarBodyStar>this.system.orbits[primaryOrbit];
                primaryOrbit = newPrimaryOrbit;
                logger.error(`${this.world.name}: primary orbit conflicts with star - moving to companion`);
            }
        }

        if(primaryOrbit < 0) {
            // This is an asteroid belt
            while(this.mainWorld === undefined) {
                this.mainWorld = <any>this.placeBelt(random, true);
            }
        } else if(primarySystem.orbits) {
            if(random.flux()<-2) {
                // MW is satellite.  Note that the near/far probabilities are equal so we just place the world in any
                // available slot assuming that will be near/far.  TBH I'm not sure how this is supposed to work since
                // for most systems the available orbits around a world at the HZ would mean there are no far orbits
                // except for I-III class stellar bodies.  Note that there must be at least one satelite orbit available
                // at the primaryOrbit otherwise we don't bother trying since we could not then place the mainworld

                // Do we have a GG?
                let uwp: UWPElements|undefined = undefined;
                let satelliteOrbits = 0;
                if (this.gg > 0) {
                    uwp = WorldGen.makeGasGiant(random);
                    satelliteOrbits = WorldGen.maxSatelliteOrbitsForOrbit(primaryOrbit, uwp.size ?? 0);
                    if(!satelliteOrbits) {
                        uwp = undefined;
                    } else {
                        --this.gg;
                        --this.planets;
                    }
                }
                if(uwp === undefined) {
                    // Brutal kludge - this will get overwritten later but we need it for makeBigWorld
                    //this.worlds = [this.stellarBodyForPlanet(`${primarySystem.name}-tmp}`, 0, this.uwp, this.system)];

                    uwp = this.makeWorldCore(random, { maxPopulation: this.uwp.population ?? 0, maxPopulationDigit: this.uwp.populationDigit ?? 1, size: 8});
                    satelliteOrbits = WorldGen.maxSatelliteOrbitsForOrbit(primaryOrbit, uwp.size ?? 0);
                    if(!satelliteOrbits) {
                        uwp = undefined;
                    } else {
                        --this.planets;
                    }
                }
                if(uwp === undefined) {
                    logger.error(`Can't generate primary for ${this.world.hex}:${this.world.name} @${primaryOrbit} [${this.stars[0]}] - no satelite orbits`);
                    this.mainWorld = this.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}`, WorldGen.maxSatelliteOrbitsForOrbit(primaryOrbit, this.uwp.size), this.uwp, primarySystem)
                    primarySystem.setOrbit(primaryOrbit, this.mainWorld);
                    --this.planets;
                } else {
                    primarySystem.setOrbit(primaryOrbit,
                        this.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}`, satelliteOrbits, uwp, primarySystem));
                    const orbit = WorldGen.orbitalPosition(<StellarBodyPlanet>primarySystem.orbits[primaryOrbit],
                        this.satelliteOrbit(random, primaryOrbit, <StellarBodyPlanet>primarySystem.orbits[primaryOrbit]));
                    this.mainWorld = this.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}-${orbit}`, WorldGen.maxSatelliteOrbitsForOrbit(orbit, this.uwp.size), this.uwp, <any>primarySystem.orbits[primaryOrbit])
                    primarySystem.orbits[primaryOrbit]?.setOrbit(orbit, this.mainWorld);
                }
            } else {
                this.mainWorld = this.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}`, WorldGen.maxSatelliteOrbitsForOrbit(primaryOrbit, this.uwp.size), this.uwp, primarySystem);
                primarySystem.setOrbit(primaryOrbit, this.mainWorld);
                --this.planets;
            }

            this.worlds = [this.mainWorld];
            this.mainWorld.primary = true;
            this.postProcessWorld(random, this.mainWorld);
        } else {
            logger.error(`Can't generate primary for ${this.world.hex}:${this.world.name} - no orbits`);
        }
    }


    generatePlanets() {
        this.addPrimaryWorld();
        this.worldIdx = 1;

        // GGs
        let ggIdx = 0;
        while(this.gg > 0) {
            this.placeGG(this.random.sub(`GG-${ggIdx++}`));
        }

        // Belts
        let beltIdx = 0;
        while(this.belts > 0) {
            this.placeBelt(this.random.sub(`BELT-${beltIdx++}`));
        }

        let worldIdx = 0;
        while(this.planets>0) {
            this.placeWorld(this.random.sub(`WORLD-${worldIdx++}`));
        }

        // Now add satellites
        for(const star of this.starBodies) {
            star.orbits.forEach((orbit, orbitIdx) => {
                if(orbit && orbit.uwp) {
                    const applyLimits = (uwp: UWPElements) => {
                        if (orbitIdx <= star.driveLimits.jdrive) {
                            uwp.notes.add('!J');
                        }
                        if (orbitIdx > star.driveLimits.mdrive) {
                            uwp.notes.add('!M');
                        }
                    }
                    applyLimits(orbit.uwp);

                    const dm =
                        (orbit.uwp.size ?? 0) >= 20 ? 1 :
                            orbit.uwp.notes.has('Hsp') ? 4 :
                                orbitIdx <= 6 ? 5 : 3;
                    let satellites = 0;
                    while(satellites == 0) {
                        satellites = this.random.die() - dm;
                        if(satellites == 0) {
                            this.placeRing(orbitIdx, <StellarBodyPlanet>orbit);
                            continue;
                        }
                        // 0 === ring and reroll
                        for (let sateliteIdx = 0; sateliteIdx < satellites; ++sateliteIdx) {
                            this.placeSatellite(orbitIdx, <StellarBodyPlanet>orbit, star);
                        }
                    }
                    orbit.orbits?.forEach(v => { if(v?.uwp) {applyLimits(v.uwp);}} );
                }
            })
        }
    }

    static enrichNotes(random: FluxRandom, uwp: UWPElements) {
        if((uwp?.population ?? 0) === 0) {
            return;
        }

        WorldGen.enrichType(random.sub('TL'), 'TL', WorldGen.TECH_TYPES, uwp.techLevel, 2, 1).forEach(e => uwp.notes.add(e));
        WorldGen.enrichType(random.sub('LL'), 'LL', WorldGen.LAW_TYPES, uwp.lawLevel, 5, 5).forEach(e => uwp.notes.add(e));
    }

    static enrichPlanets(world: World): OverrideWorld[] {
        const wg = new WorldGen(world);

        wg.generatePlanets();
        //wg.iterateOverAll(wg.system, p => wg.enrichNotes(p))

        return [{
            hex: wg.world.hex,
            notes: [...wg.mainWorld.uwp.notes],
        },
            ...wg.starsToWorld('', wg.system)];
    }

    iterateOverAll(body: StellarBody|undefined, process: (p: StellarBody) => void) {
        if(body === undefined) {
            return;
        }
        process(body);
        body.orbits?.forEach(p => this.iterateOverAll(p, process));
    }

    starsToWorld(suffix: string, body: StellarBodyType): OverrideWorld[] {
        let factions: OverrideWorld[] = [];
        let base: OverrideWorld = {
            hex: this.world.hex + (suffix ? suffix : '-*'),
            name: body.name,
        };
        if(body.star) {
            base = {
                ...base,
                name: `${body.name} [${body.star.trim()}]`,
                stars: body.star.trim(),
                ...(<any>body)?.driveLimits,
            }
        }
        if(body.uwp) {
            base = {
                ...base,
                uwp: WorldGen.encodeUwpElements(body.uwp),
                notes: [...body.uwp.notes, ...(body.primary ? [Notes.MAINWORLD]: [])],
                pbg: (body.uwp.population ?? 0) > 0 ? `${body.uwp.populationDigit ?? 1}**` : undefined,
            };
            if(body.factions && body.factions.length) {
                factions = body.factions.map((f,idx) => ({
                    hex: this.world.hex + (suffix ? suffix : '-*') + String.fromCharCode(65 + idx),
                    name: body.name + '-faction-' + String.fromCharCode(65 + idx),
                    uwp: WorldGen.encodeUwpElements(f),
                    notes: [...f.notes ],
                    pbg: (f.population ?? 0) > 0 ? `${f.populationDigit ?? 1}**` : undefined,
                }));
            }
        }
        const rv = [base, ...factions];
        const children = body.orbits?.flatMap((child,idx) => child === undefined ? undefined : this.starsToWorld(`${suffix}-${idx}`, child))?.filter(v => v !== undefined) ?? [];
        rv.push(...children);
        return rv;
    }
}

