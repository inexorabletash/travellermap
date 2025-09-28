import {fluxAmount, fluxAmountCalc, FluxRandom} from "../util.js";
import {World} from "./world.js";
import {OverrideWorld} from "./override.js";
import logger from "../logger.js";

export type StellarBody = {
    name: string;
    star?: string;
    uwp?: UWPElements;
    orbits?: (undefined|StellarBody)[];
    primary?: boolean;
}
export type StellarBodyStar = StellarBody & {
    orbits: (undefined|StellarBody)[];
    star: string;
    driveLimits: DriveLimitsStar;
};
export type StellarBodyPlanet = StellarBody & {
    orbits: (undefined|StellarBody)[];
    uwp: UWPElements;
};
export type StellarBodySatellite = StellarBody & {
    orbits: undefined;
    uwp?: string;
};

export type DriveLimitsStar = {
    mdrive: number,
    jdrive: number,
    gdrive: number
};


export const NO_ORBIT : StellarBody = {
    name: '***'
};

export const ORBIT_AUS =
    [
        0.2,0.4,0.7,1.0,1.6,2.8,5.2,10,20,40,
        77,154,308,615,1230,2500,4900,9800,19500,39500,
        78700,150000, // anything beyond this is a parsec [206266]

    ];


export type UWPElements = {
    starport: string,
    size: number|undefined,
    atmosphere: number|undefined,
    hydrographic: number|undefined,
    population: number|undefined,
    govt: number|undefined,
    lawLevel: number|undefined,
    techLevel: number|undefined,
    notes: Set<string>,
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

    constructor(protected world: World) {
        this.uwp = WorldGen.getUWP(world);
        const pbg = WorldGen.getPBG(world);
        this.planets = WorldGen.getPlanets(world);
        this.gg = pbg.gg;
        this.belts = pbg.belts;

        this.random = new FluxRandom(`PLANET-DETAILS/${world.sec}/${world.hex}`);

        this.stars = WorldGen.getStars(world);
        this.starBodies = WorldGen.stellarBodyForStars(this.random, world.name, this.stars);
        this.system = this.starBodies[0];
        this.worldIdx = 0;
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
        return this.extractUWP(uwp, world?.notes);
    }

    static extractUWP(uwp: string, notes: Set<string>): UWPElements {
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

    static starLimits(star: string, previous?: DriveLimitsStar) : DriveLimitsStar {
        const jdrive = this.starTable(star, this.JUMP_LIMIT_TABLE) ?? -1;
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
        return {
            mdrive: Math.max(previous?.mdrive ?? -1, mdrive),
            jdrive: Math.max(previous?.jdrive ?? -1, jdrive),
            gdrive: Math.max(previous?.gdrive ?? -1, gdrive),
        };
    }

    static primaryOrbit(w: World, star: string, starDm: number) {
        const uwp = this.getUWP(w);
        if(uwp.size === 0) {
            return -1;
            // asteroid belt
        }

        // MW is satelite?
        let habitableZone = this.habitableZone(star) + starDm;
        if(w.notes.has('Tr')) {
            ++habitableZone;
        } else if(w.notes.has('Tu')) {
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

    static stellarBodyForStar(name: string, star: string, maxOrbits: number, previous?: StellarBodyStar): StellarBodyStar {
        const disallowed = this.starTable(star, this.PRECLUDED_ORBITS_TABLE) ?? -1;
        return {
            name: name,
            star,
            orbits: Array(maxOrbits).map((_,idx) => idx >= disallowed ? NO_ORBIT : undefined),
            driveLimits: this.starLimits(star, previous?.driveLimits),
        };
    }

    static addBodyForStar(name: string, star: string, parent: StellarBodyStar, target: number, bodyList: StellarBodyStar[]) {
        const orbit = this.orbitalPosition(parent, target);
        const body = this.stellarBodyForStar(`${name}-${orbit}`, star, this.maxOrbitsForOrbit(orbit), parent);
        this.setOrbit(parent, orbit, body);
        return body;
    }

    static stellarBodyForStars(random: FluxRandom, name: string, stars: (string|undefined)[]): StellarBodyStar[] {
        console.log(`stellarBodyForStars: ${name} - ${stars}`);
        console.log(`\t0: ${stars[0]}`);
        const rv: StellarBodyStar[] = [
            this.stellarBodyForStar(name, stars[0]??'', 20),
        ];
        if(stars[1]) {
            this.addBodyForStar(rv[0].name, stars[1], rv[0], 0, rv);
            console.log(`\t0-0: ${stars[1]}`);
        }

        for(let pos = 2; pos < stars.length; pos += 2) {
            if(stars[pos]) {
                const orbit = random.die(6) + pos * 3 - 6;
                const parent = this.addBodyForStar(rv[0].name, <string>stars[pos], rv[0], orbit, rv);
                rv.push(parent);
                console.log(`\t${orbit}: ${stars[pos]}`);

                if(stars[pos+1]) {
                    console.log(`\t${orbit}-0: ${stars[pos+1]}`);
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

    static stellarBodyForPlanet(name: string, maxOrbits: number, uwp: UWPElements, parent: StellarBodyStar|StellarBodyPlanet): StellarBodyPlanet {
        return {
            name,
            orbits: Array(maxOrbits).map((_,idx) => undefined),
            uwp,
        };
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

    static makePopulation(random: FluxRandom, maxPopulation: number, dm: number) {
        const base = Math.max(0,random.die(6,2)-2 + dm);
        if(base == 10) {
            return Math.min(maxPopulation, random.die(6,2)+3);
        }
        return Math.min(maxPopulation, base);
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

    static makeWorld(random: FluxRandom, maxPopulation: number, dms: UWPDMs={}): UWPElements {
        const spRoll = random.die() + (dms.starport??0);
        const starport = spRoll >= 4 ? 'F' : spRoll == 3 ? 'G' : spRoll>0 ? 'H' : 'X';
        const size = Math.min(dms.maxSize ?? 24, Math.max(0,random.die(6,dms.sizeDice??2)+(dms.size ?? 0)));
        const atmosphere = Math.max(0,Math.min(15,size + random.flux() + (dms.atmosphere??0)));
        const hydrographic = this.makeHydrographic(random, size, atmosphere, dms.hydrographic??0);
        const population = this.makePopulation(random, maxPopulation, dms.population ??0);
        const govt = population == 0 ? 0 : Math.max(0, Math.min(15, population + random.flux() + (dms.population??0)));
        const lawLevel = population == 0 ? 0 : Math.max(0, Math.min(18, govt + random.flux() + (dms.lawLevel??0)));
        const techLevel = this.makeTL(random, starport, size, atmosphere, hydrographic, population, govt, dms.techLevel ?? 0);
        return {
            starport,
            size,
            atmosphere,
            hydrographic,
            population,
            govt,
            lawLevel,
            techLevel,
            notes: new Set(dms.notes ?? []),
        };
    }


    static makeBigWorld(random: FluxRandom, maxPopulation: number): UWPElements {
        return this.makeWorld(random, maxPopulation, {size:8})
    }

    nextStar(): StellarBodyStar {
        while(true) {
            const body = this.starBodies[this.worldIdx++ % this.starBodies.length];
            if(WorldGen.minOrbit(body) <= WorldGen.maxOrbit(body)) {
                return body;
            }
        }
    }

    static setOrbit(orbit: StellarBody|undefined, index: number, to: StellarBody) {
        if(orbit?.orbits === undefined) {
            throw new Error(`setOrbit on ${orbit?.name} - no orbits`);
        }
        if(!Number.isInteger(index)) {
            throw new Error(`setOrbit on ${orbit.name} - ${index} isn't valid`);
        }
        if(index < 0 || index >= orbit.orbits.length) {
            throw new Error(`setOrbit on ${orbit.name} - ${index} is out of range (max ${orbit.orbits.length})`);
        }
        if(orbit.orbits[index] !== undefined) {
            throw new Error(`setOrbit on ${orbit.name} - ${index} already has something present`);
        }
        orbit.orbits[index] = to;
    }

    placeGG() {
        const body = this.nextStar();

        // Note we haven't done ice giants here....

        const hz = WorldGen.habitableZone(body.star);
        const gg = WorldGen.makeGasGiant(this.random);
        const offset = (gg.size > 22) ? -5 : -4;
        const posOffset = this.random.die(6,2)+offset;
        const ggPos = WorldGen.orbitalPosition(body, Math.max(0,hz+posOffset));
        if(ggPos < 0) {
            logger.error(`Unable to place world on ${body.name}`);
            return;
        }
        WorldGen.setOrbit(body,ggPos,WorldGen.stellarBodyForPlanet(`${body.name}-${ggPos}`, WorldGen.maxSatelliteOrbitsForOrbit(ggPos, gg.size), gg,body));
        --this.gg;
        --this.planets;
    }

    placeBelt(isPrimary = false): StellarBodyPlanet|undefined {
        const body = this.nextStar();

        const hz = WorldGen.habitableZone(body.star);
        const posOffset = this.random.die(6,2)-3;
        const pos = WorldGen.orbitalPosition(body, Math.max(0,hz+posOffset));
        if(pos < 0) {
            logger.error(`Unable to place world on ${body.name}`);
            // Don't decrement planets ... we will try again
            return undefined;
        }
        const belt = isPrimary ? this.uwp : WorldGen.makeWorld(this.random, this.world?.populationDigit ?? 0, { size:-100, notes: ['As']});
        const beltBody = WorldGen.stellarBodyForPlanet(`${body.name}-${pos}`, 0, belt, body)
        WorldGen.setOrbit(body, pos, beltBody);
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

    placeWorld() {
        let body;
        let posOffset;

        if(this.planets === 1) {
            body = this.system;
            posOffset = this.random.die(6,2)+5;
        } else {
            body = this.nextStar();
            posOffset = [10,8,6,4,2,0,1,3,5,7,9][this.random.die(6,2)-2];
        }
        const hz = WorldGen.habitableZone(body.star);
        const pos = WorldGen.orbitalPosition(body, hz+posOffset);
        if(pos < 0) {
            logger.error(`Unable to place world on ${body.name}`);
            // Don't decrement planets ... we will try again
            return;
        }

        let dms: UWPDMs = this.uwpDMs(posOffset, false);

        const world = WorldGen.makeWorld(this.random, this.world?.populationDigit??0, dms)
        WorldGen.setOrbit(body, pos, WorldGen.stellarBodyForPlanet(`${body.name}-${pos}`, WorldGen.maxSatelliteOrbitsForOrbit(pos, world.size), world, body));
        --this.planets;
    }

    satelliteOrbit(orbitIdx: number, orbit: StellarBodyPlanet) {
        const orbits = WorldGen.maxSatelliteOrbitsForOrbit(orbitIdx, orbit.uwp.size);
        if(orbits <= 0) {
            return -1;
        }
        let target = orbits;

        while(target >= orbits) {
            const md = Math.floor((orbits - 5) / 3) - 1;
            const die = md <= 1 ? 0 : (this.random.die(md) - 1);
            const dm = die * 3 + 5;
            target = this.random.flux() + dm;
        }
        return target;
    }

    placeSatellite(orbitIdx: number, body: StellarBodyPlanet, star: StellarBodyStar) {
        if(!WorldGen.hasAvailableOrbits(body)) {
            //console.log(`${body.name} - placeSatelite - no orbits`);
            return;
        }
        const target = WorldGen.orbitalPosition(body, this.satelliteOrbit(orbitIdx, body));

        const hz = WorldGen.habitableZone(star.star);
        let dms: UWPDMs = this.uwpDMs(target-hz, true);
        const maxSize = body.uwp.size;

        const world = WorldGen.makeWorld(this.random, this.world?.populationDigit??0, { ...dms, maxSize})
        WorldGen.setOrbit(body, target, WorldGen.stellarBodyForPlanet(`${body.name}-${target}`, 0, world, body));
    }

    placeRing(orbitIdx: number, body: StellarBodyPlanet) {
        if(!WorldGen.hasAvailableOrbits(body)) {
            //console.log(`${body.name} - placeRing - no orbits`);
            return;
        }
        const target = WorldGen.orbitalPosition(body, this.satelliteOrbit(orbitIdx, body));

        const world = {
            starport: 'X',
            size: 0,
            atmosphere: 1,
            hydrographic: 0,
            population: 0,
            govt: 0,
            lawLevel: 0,
            techLevel: 0,
            notes: new Set<string>(),
        };
        WorldGen.setOrbit(body, target, WorldGen.stellarBodyForPlanet(`${body.name}-${target}`, 0, world, body));
    }


    addPrimaryWorld() {
        let primarySystem = this.system;
        let primaryOrbit = WorldGen.primaryOrbit(this.world, this.stars[0] ??'', 0);

        if(this.system.orbits[primaryOrbit]) {
            // If the primary orbit is occupied by a star we have two options.  Firstly we will try to put the primary
            // in some orbit of the companion star.
            const newPrimaryOrbit = WorldGen.orbitalPosition(
                <StellarBodyStar>this.system.orbits[primaryOrbit],WorldGen.primaryOrbit(this.world, this.stars[0] ??'', 1));
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
                this.mainWorld = <any>this.placeBelt(true);
            }
        } else if(primarySystem.orbits) {
            if(this.random.flux()<-2) {
                // MW is satellite.  Note that the near/far probabilities are equal so we just place the world in any
                // available slot assuming that will be near/far.  TBH I'm not sure how this is supposed to work since
                // for most systems the available orbits around a world at the HZ would mean there are no far orbits
                // except for I-III class stellar bodies.  Note that there must be at least one satelite orbit available
                // at the primaryOrbit otherwise we don't bother trying since we could not then place the mainworld

                // Do we have a GG?
                let uwp: UWPElements|undefined = undefined;
                let satelliteOrbits = 0;
                if (this.gg > 0) {
                    uwp = WorldGen.makeGasGiant(this.random);
                    satelliteOrbits = WorldGen.maxSatelliteOrbitsForOrbit(primaryOrbit, uwp.size ?? 0);
                    if(!satelliteOrbits) {
                        uwp = undefined;
                    } else {
                        --this.gg;
                        --this.planets;
                    }
                }
                if(uwp === undefined) {
                    uwp = WorldGen.makeBigWorld(this.random, this.uwp.population??0);
                    satelliteOrbits = WorldGen.maxSatelliteOrbitsForOrbit(primaryOrbit, uwp.size ?? 0);
                    if(!satelliteOrbits) {
                        uwp = undefined;
                    } else {
                        --this.planets;
                    }
                }
                if(uwp === undefined) {
                    logger.error(`Can't generate primary for ${this.world.hex}:${this.world.name} @${primaryOrbit} [${this.stars[0]}] - no satelite orbits`);
                    this.mainWorld = WorldGen.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}`, WorldGen.maxSatelliteOrbitsForOrbit(primaryOrbit, this.uwp.size), this.uwp, primarySystem)
                    WorldGen.setOrbit(primarySystem, primaryOrbit, this.mainWorld);
                    --this.planets;
                } else {
                    WorldGen.setOrbit(primarySystem, primaryOrbit,
                        WorldGen.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}`, satelliteOrbits, uwp, primarySystem));
                    const orbit = WorldGen.orbitalPosition(<StellarBodyPlanet>primarySystem.orbits[primaryOrbit],
                        this.satelliteOrbit(primaryOrbit, <StellarBodyPlanet>primarySystem.orbits[primaryOrbit]));
                    this.mainWorld = WorldGen.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}-${orbit}`, WorldGen.maxSatelliteOrbitsForOrbit(orbit, this.uwp.size), this.uwp, <any>primarySystem.orbits[primaryOrbit])
                    WorldGen.setOrbit(primarySystem.orbits[primaryOrbit], orbit, this.mainWorld);
                }
            } else {
                this.mainWorld = WorldGen.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}`, WorldGen.maxSatelliteOrbitsForOrbit(primaryOrbit, this.uwp.size), this.uwp, primarySystem);
                WorldGen.setOrbit(primarySystem, primaryOrbit, this.mainWorld);
                --this.planets;
            }
        } else {
            logger.error(`Can't generate primary for ${this.world.hex}:${this.world.name} - no orbits`);
        }
    }


    generatePlanets() {
        this.addPrimaryWorld();
        this.worldIdx = 1;

        // GGs
        while(this.gg > 0) {
            this.placeGG();
        }

        // Belts
        while(this.belts > 0) {
            this.placeBelt();
        }

        while(this.planets>0) {
            this.placeWorld();
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

    enrichNotes(body: StellarBody) {
        //uwp: UWPElements, sectorName: string, worldName: string) {
        if(!body.uwp) {
            return;
        }
        const uwp = body.uwp;

        if((uwp?.population ?? 0) === 0) {
            return;
        }

        const baseKey = '/' + this.world.secName + '/' + body.name;
        WorldGen.enrichType(new FluxRandom(baseKey+'TL'), 'TL', WorldGen.TECH_TYPES, uwp.techLevel, 2, 1).forEach(e => uwp.notes.add(e));
        WorldGen.enrichType(new FluxRandom(baseKey+'LL'), 'LL', WorldGen.LAW_TYPES, uwp.lawLevel, 5, 5).forEach(e => uwp.notes.add(e));
    }

    static enrichPlanets(world: World): OverrideWorld[] {
        const wg = new WorldGen(world);

        wg.generatePlanets();
        wg.iterateOverAll(wg.system, p => wg.enrichNotes(p))

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

    starsToWorld(suffix: string, body: StellarBody): OverrideWorld[] {
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
                notes: [...body.uwp.notes],
                ...(<any>body)?.driveLimits,
            };
        }
        const rv = [base];
        const children = body.orbits?.flatMap((child,idx) => child === undefined ? undefined : this.starsToWorld(`${suffix}-${idx}`, child))?.filter(v => v !== undefined) ?? [];
        rv.push(...children);
        return rv;
    }
}

