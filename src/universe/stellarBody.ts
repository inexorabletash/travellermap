import {WorldGen} from "./worldGen.js";
import {World} from "./world.js";
import {FluxRandom} from "../util.js";

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

    minOrbit() {
        return this.orbits?.findIndex(orbit => orbit == undefined) ?? -1;
    }

    maxOrbit() {
        return this.orbits?.findLastIndex(orbit => orbit == undefined) ?? -1;
    }

    orbitalPosition(target: number): number {
        const base = Math.max(Math.min(target, this.maxOrbit()),this.minOrbit());
        let pos = base;

        while(this.orbits[pos]) {
            ++pos;
        }

        return pos;
    }

    hasAvailableOrbits() {
        return this.maxOrbit() != -1;
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

    addStar(worldGen: WorldGen, star: string, target: number) {
        const orbit = this.orbitalPosition(target);
        const orbitCnt = StellarBodyStar.maxOrbitsForOrbit(orbit);

        const body = new StellarBodyStar({
                worldGen,
                name: `${parent.name}-${orbit}`,
                parent: this,
                star,
                orbitCnt,
                primary: false,
            }
        );
        this.setOrbit(orbit, body);
        return body;
    }



    static starLimits(star: string, previous?: DriveLimitsStar) : DriveLimitsStar {
        const jdrive = StellarBodyStar.starTable(star, StellarBodyStar.JUMP_LIMIT_TABLE) ?? -1;
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
        const precluded = StellarBodyStar.starTable(star, StellarBodyStar.PRECLUDED_ORBITS_TABLE) ?? -1
        return {
            mdrive: Math.max(previous?.mdrive ?? -1, mdrive),
            jdrive: Math.max(previous?.jdrive ?? -1, jdrive),
            gdrive: Math.max(previous?.gdrive ?? -1, gdrive),
            precluded,
            hz: this.habitableZone(star),
        };
    }

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

    static maxOrbitsForOrbit(orbit: number) {
        let maxOrbits = orbit - 2;
        if(maxOrbits == 0) {
            maxOrbits = 1;
        } else if(maxOrbits < 0) {
            maxOrbits = 0;
        }
        return maxOrbits;
    }

    // FIXME: populate as we create

    static habitableZone(star: string) : number {
        return this.starTable(star, this.HABITABLE_ZONE_TABLE) ?? 0;
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

    static worldSizeMult(worldSize: number|undefined) {
        worldSize ??= 0;
        const szMult = worldSize <= 20 ? Math.max(1,worldSize) : ([20,30,40,50,60,70,80,90,125,180,220,250][worldSize-20]);
        return szMult;
    }

    static maxSatelliteOrbitsForOrbit(orbit: number, worldSize: number|undefined) {
        const szMult = this.worldSizeMult(worldSize);
        const maxStellarOrbit = StellarBodyStar.maxOrbitsForOrbit(orbit);
        const maxAu = ORBIT_AUS[Math.min(ORBIT_AUS.length-1, maxStellarOrbit)];
        const maxMm = maxAu * 1.5e5;

        // Assuming orbits are linearly spaced....
        const orbitLimit = Math.trunc(Math.log10(maxMm / szMult / 7));

        return Math.min(Math.max(0, orbitLimit),24);
        // max will be < 5k for orbit 24 (base is 0 == A)
        //5k * 250 / 15000 => 1/3*250 => 85 au which is O==11 which given maxOrbits rule is O==13

        // AU = Mm/15000
        //const orbit
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
