import {fluxAmountCalc, FluxRandom} from "../util.js";
import {Notes, World} from "./world.js";
import {OverrideWorld} from "./override.js";
import logger from "../logger.js";
import {processClassifications} from "./tables.js";
import {StellarBody, StellarBodyPlanet, StellarBodyStar, StellarBodyType, UWPElements} from "./stellarBody.js";


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
    fixedStarport?: string;
    spaceportVsStarportDm?: number;
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
    //worlds: StellarBodyPlanet[];
    factions: Map<string, UWPElements>;

    constructor(protected world: World) {
        this.uwp = WorldGen.getUWP(world);
        const pbg = WorldGen.getPBG(world);
        this.planets = WorldGen.getPlanets(world);
        this.gg = pbg.gg;
        this.belts = pbg.belts;

        this.random = new FluxRandom(`PLANET-DETAILS/${world.sec}/${world.hex}`);

        this.stars = StellarBodyStar.getStars(world);
        this.starBodies = this.stellarBodyForStars(this.random, world.name, this.stars);
        this.system = this.starBodies[0];
        this.worldIdx = 0;
        this.factions = new Map();
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

        // TODO - deal with missing stuff / load existing worlds
        return this.extractUWP(uwp, world?.notes, world?.pbg, world.owner);
    }

    static extractUWP(uwp: string, notes: Set<string>, pbg: string|undefined, faction: string): UWPElements {
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
            faction,
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


    static checkInCodeList(value: number|undefined, codeList: string): boolean {
        const code = World.encodedValue(value??-1);
        return codeList.indexOf(code) >= 0;
    }


    stellarBodyForStar(name: string, star: string, maxOrbits: number, previous?: StellarBodyStar): StellarBodyStar {
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

    stellarBodyForStars(random: FluxRandom, name: string, stars: (string|undefined)[]): StellarBodyStar[] {
        const rv: StellarBodyStar[] = [
            this.stellarBodyForStar(name, stars[0]??'', 20),
        ];
        if(stars[1]) {
            rv[0].addStar(this, stars[1], 0);
        }

        for(let pos = 2; pos < stars.length; pos += 2) {
            if(stars[pos]) {
                const orbit = random.die(6) + pos * 3 - 6;
                const parent = rv[0].addStar(this, <string>stars[pos], orbit);
                rv.push(parent);

                if(stars[pos+1]) {
                    parent.addStar(this, <string>stars[pos+1], 0);
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

    static makeGasGiant(random: FluxRandom) : UWPElements {
        const size = random.die(6,2)+19;

        return {
            starport: 'X',
            size,
            atmosphere: 14,
            hydrographic: 0,
            population: 0,
            populationDigit: 0,
            govt: 0,
            lawLevel: 0,
            techLevel: 0,
            notes: new Set<string>(['GG']),
            faction: undefined,
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

    //static makeStarport(random: FluxRandom, dm: number, maxStarport: string|undefined, spaceportVsStarportDm: number = 0) {
    static makeStarport(random: FluxRandom, maxStarport: string|undefined, dms: UWPDMs) {
        if(dms.fixedStarport) {
            return dms.fixedStarport;
        }
        // We are possibly making a spaceport.
        if(maxStarport) {
            if(maxStarport < 'F' && (random.die()+(dms.spaceportVsStarportDm??0)) >= 2) {
                // Generate a spaceport. 2:3 normally if not primary, but more chance if faction
                const spRoll = random.die() + (dms.starport??0);
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

    static makePhysicalWorld(random: FluxRandom, dms: UWPDMs) : Pick<UWPElements,'size'|'atmosphere'|'hydrographic'|'notes'> {
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
            populationDigit: 0,
            faction: undefined,
        }
    }

    determineColonyOwner(random: FluxRandom, exclude: string[] = []): undefined|UWPElements {
        const factions = [...this.factions.entries() ];
        if(factions.length - (exclude !== undefined ? exclude.length : 0) < 1) {
            return undefined;
        }

        if(random.die(4+factions.length) < factions.length) {
            // Use existing faction
            while(true) {
                const worldDie = random.die(factions.length) - 1;
                const faction = factions[worldDie];

                if(exclude.find(x => x === faction[0])) {
                    continue;
                }
                return faction[1];
            }
        } else {
            return undefined;
        }
    }

    createUWPExistingFaction(random: FluxRandom, world: Partial<UWPElements> & Pick<UWPElements,'size'|'atmosphere'|'hydrographic'>,
                          fromFaction: UWPElements, dms: UWPDMs): UWPElements {
        const popVars =
            WorldGen.makePopulation(random, {
                maxPopulation: world?.population ?? this.uwp.population,
                maxPopulationDigit: world?.populationDigit ?? this.uwp.populationDigit,
            });
        let starport, population, populationDigit, govt, lawLevel, techLevel;
        population = popVars.population;
        populationDigit = popVars.population;

        let die = random.die();
        let roll = 0;
        do {
            die = random.die();
            roll += die;
        } while (die == 6);

        if (roll < 1 + Math.floor(population / 2)) {
            govt = fromFaction.govt ?? 0;
            techLevel = (fromFaction.techLevel ?? 0) + fluxAmountCalc(random, 2, 1);
            lawLevel = (fromFaction.techLevel ?? 0) + fluxAmountCalc(random, 5, 5);
            starport = WorldGen.makeStarport(random, this.uwp.starport, dms);
        } else {
            govt = 6; // Captive / colony
            techLevel = fromFaction.techLevel ?? 0;
            lawLevel = fromFaction.lawLevel ?? 0;
            starport = WorldGen.makeStarport(random, 'E', dms);
        }

        const uwp: UWPElements = {
            ...world,
            notes: new Set<string>([`O:${fromFaction.faction}`]),
            starport, population, populationDigit, govt, lawLevel, techLevel,
            faction: fromFaction.faction,
        };

        // Update the faction population.  This isn't really important because nothing should reference it, but it's
        // good to be consistent.
        if(fromFaction.population < uwp.population) {
            fromFaction.population = uwp.population;
            fromFaction.populationDigit = uwp.populationDigit;
        } else if(fromFaction.population === uwp.population) {
            fromFaction.populationDigit += uwp.populationDigit;
            if(fromFaction.populationDigit > 14) {
                ++fromFaction.population;
                fromFaction.populationDigit = 1;
            } else if(fromFaction.populationDigit > 9) {
                ++fromFaction.population;
                fromFaction.populationDigit = 2;
            }
        }
        return uwp;
    }

    createNewFaction(random: FluxRandom, world: Pick<UWPElements,'size'|'atmosphere'|'hydrographic'>, dms: UWPDMs, prevalentTL: number|undefined): UWPElements {
        const factionId = this.factions.size - 1;
        const factionName = `faction-${WorldGen.factionCode(factionId)}`;
        const elements = {
            size: world.size,
            atmosphere: world.atmosphere,
            hydrographic: world.hydrographic,
        };
        const starport = WorldGen.makeStarport(random, this.uwp.starport, dms);
        const {population,populationDigit} = WorldGen.makePopulation(random, dms);
        const notes: string[] = [`O:${factionName}`];
        let govt = 7;

        let iterations = 0;
        // If we have a prevelantTL this implies we are creating a faction in a balkanized state so we need to keep trying
        // until we don't get another balkanized state.  However, if we do reroll 7 we mark as having unrest
        while (govt == 7 && (iterations == 0 || prevalentTL!==undefined)) {
            govt = (population == 0 ? 0 : Math.max(0, Math.min(15, (population ?? 0) + random.flux())));
            ++iterations;
        }
        if(iterations > 1) {
            notes.push(Notes.UNREST);
        }
        const lawLevel = population == 0 ? 0 : Math.max(0, Math.min(18, (govt??0) + random.flux() + (dms.lawLevel??0)));


        let techLevel;
        if(prevalentTL) {
            techLevel = prevalentTL + fluxAmountCalc(random, 5, 1);
        } else {
            techLevel = WorldGen.makeTL(random, starport, elements.size ?? 0, elements.atmosphere ?? 0, elements.hydrographic ?? 0, population, govt??0, dms.techLevel ?? 0);
        }

        const uwp: UWPElements = {
            ...elements,
            notes: new Set<string>(notes),
            starport, population, populationDigit, govt: govt??0, lawLevel, techLevel,
            faction: factionName,
        };
        this.factions.set(factionName,uwp);
        return uwp;
    }



    createFactionsForWorld(random: FluxRandom, world: StellarBodyPlanet) {
        let factions = 2 + (random.die()-1)%4;
        let bestStarportFaction = random.die(factions);
        const knownFactions: string[] = [];

        for(let faction = 1; faction <= factions; ++faction) {
            const subRandom = random.sub(`Faction=${faction}`);
            let factionUPP;
            const dms: UWPDMs = { spaceportVsStarportDm: -2};
            if(faction === bestStarportFaction) {
                dms.fixedStarport = world.uwp.starport;
            }
            if(faction == 1) {
                if(!world.uwp.faction || this.factions.get(world.uwp.faction) === undefined) {
                    throw new Error(`Attempt to create faction on an unowned world! ${world.name}`);
                }
                factionUPP = this.createUWPExistingFaction(subRandom, world.uwp, <UWPElements>this.factions.get(world.uwp.faction), dms);
            } else {
                const owner = this.determineColonyOwner(subRandom, knownFactions);
                if(owner) {
                    factionUPP = this.createUWPExistingFaction(subRandom, world.uwp, owner, dms);
                } else {
                    factionUPP = this.createNewFaction(subRandom, world.uwp, dms, world.uwp.techLevel);
                }
            }
            if(!factionUPP.faction) {
                throw new Error(`created faction has no assigned faction name`);
            }
            knownFactions.push(factionUPP.faction);
            WorldGen.enrichNotes(random, factionUPP);
            processClassifications(world, factionUPP, this.mainWorld?.uwp ?? this.uwp);
            world.factions.push(factionUPP);
        }
    }


    postProcessWorld(random: FluxRandom, world: StellarBodyPlanet) {
        processClassifications(world, world.uwp, this.mainWorld?.uwp ?? this.uwp);

        if(world.uwp.govt == 7) { // Balkanisation
            const subRandom = random.sub('balkans');
            this.createFactionsForWorld(subRandom, world)
        } else {
            WorldGen.enrichNotes(random, world.uwp);
        }

    }

    makeOneWorld(random: FluxRandom, dms: UWPDMs) {
        const elements = WorldGen.makePhysicalWorld(random, dms);
        const owner = this.determineColonyOwner(random, []);
        let uwp;
        if(owner) {
            uwp = this.createUWPExistingFaction(random, elements, owner, dms);
        } else {
            uwp = this.createNewFaction(random, elements, dms, undefined);
        }

        if (uwp.population > 0) {
            if (uwp.techLevel < 8 && uwp.techLevel - 5 >= random.die()) {
                uwp.population = 0;
            }
        }
        if (uwp.population == 0) {
            uwp.techLevel = 0;
            uwp.lawLevel = 0;
            uwp.starport = 'X';
            uwp.populationDigit = 0;
            uwp.notes.add('Re');
        }
        return uwp;
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

        if(this.factions.size === 0 || dms.maxPopulation === 0) {
            //console.log(`Creating empty world at ${bodyIn.name}-${orbitIn}`);
            return WorldGen.makeEmptyWorld(random, dms);
        } else {
            return this.makeOneWorld(random, dms)
        }

    }

    nextStar(): StellarBodyStar {
        while(true) {
            const body = this.starBodies[this.worldIdx++ % this.starBodies.length];
            if(body.minOrbit() <= body.maxOrbit()) {
                return body;
            }
        }
    }

    placeGG(random: FluxRandom) {
        const body = this.nextStar();

        // Note we haven't done ice giants here....

        const hz = body.driveLimits.hz;
        const gg = WorldGen.makeGasGiant(random);
        const offset = (gg.size > 22) ? -5 : -4;
        const posOffset = random.die(6,2)+offset;
        const ggPos = body.orbitalPosition(Math.max(0,hz+posOffset));
        if(ggPos < 0) {
            logger.error(`Unable to place world on ${body.name}`);
            return;
        }
        body.setOrbit(ggPos,this.stellarBodyForPlanet(`${body.name}-${ggPos}`, StellarBodyPlanet.maxSatelliteOrbitsForOrbit(ggPos, gg.size), gg, body));
        --this.gg;
        --this.planets;
    }

    placeBelt(random: FluxRandom, isPrimary = false): StellarBodyPlanet|undefined {
        const body = this.nextStar();

        const hz = body.driveLimits.hz;
        const posOffset = random.die(6,2)-3;
        const pos = body.orbitalPosition(Math.max(0,hz+posOffset));
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
        const hz = body.driveLimits.hz;
        const pos = body.orbitalPosition(hz+posOffset);
        if(pos < 0) {
            logger.error(`Unable to place world on ${body.name}`);
            // Don't decrement planets ... we will try again
            return;
        }

        let dms: UWPDMs = this.uwpDMs(posOffset, false);

        const world = this.makeWorld(body, pos, { ...dms, maxPopulation: this.uwp.population ?? 0, maxPopulationDigit: this.uwp.populationDigit ?? 1,});
        const newBody = this.stellarBodyForPlanet(`${body.name}-${pos}`, StellarBodyPlanet.maxSatelliteOrbitsForOrbit(pos, world.size), world, body);
        body.setOrbit(pos, newBody);
        this.postProcessWorld(random, newBody);
        --this.planets;
    }

    satelliteOrbit(random: FluxRandom, orbitIdx: number, orbit: StellarBodyPlanet) {
        const orbits = StellarBodyPlanet.maxSatelliteOrbitsForOrbit(orbitIdx, orbit.uwp.size);
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
        if(!body.hasAvailableOrbits()) {
            //console.log(`${body.name} - placeSatelite - no orbits`);
            return;
        }
        const target = body.orbitalPosition(this.satelliteOrbit(random, orbitIdx, body));

        const hz = star.driveLimits.hz;
        let dms: UWPDMs = this.uwpDMs(target-hz, true);
        const maxSize = body.uwp.size;

        const world = this.makeWorld(body, target, { ...dms, maxSize, maxPopulation: this.uwp.population ?? 0, maxPopulationDigit: this.uwp.populationDigit ?? 1,})
        const newBody = this.stellarBodyForPlanet(`${body.name}-${target}`, 0, world, body);
        body.setOrbit(target, newBody);
        this.postProcessWorld(random, newBody);
    }

    placeRing(orbitIdx: number, body: StellarBodyPlanet) {
        const random = this.random.sub(`ring/${body.name}/${orbitIdx}`);
        if(!body.hasAvailableOrbits()) {
            //console.log(`${body.name} - placeRing - no orbits`);
            return;
        }
        const target = body.orbitalPosition(this.satelliteOrbit(random, orbitIdx, body));

        const world = {
            starport: 'X',
            size: 0,
            atmosphere: 1,
            hydrographic: 0,
            population: 0,
            govt: 0,
            lawLevel: 0,
            techLevel: 0,
            populationDigit: 0,
            faction: undefined,
            notes: new Set<string>(['Ring']),
        };
        body.setOrbit(target, this.stellarBodyForPlanet(`${body.name}-${target}`, 0, world, body));
    }

    static primaryOrbit(w: World, star: StellarBodyStar, starDm: number) {
        const uwp = this.getUWP(w);
        if(uwp.size === 0) {
            return -1;
            // asteroid belt
        }

        // MW is satelite?
        let habitableZone = star.driveLimits.hz + starDm;
        if(w.notes.has('Tr') || w.notes.has('Ho')) {
            ++habitableZone;
        } else if(w.notes.has('Tu') || w.notes.has('Co')) {
            --habitableZone;
        } else if(w.notes.has('Fr')) {
            habitableZone -= 2;
        }
        return Math.max(0,habitableZone);
    }



    addPrimaryWorld() {
        let primarySystem = this.system;
        let primaryOrbit = WorldGen.primaryOrbit(this.world, this.system, 0);
        const random = this.random.sub('primary');

        if(this.system.orbits[primaryOrbit]) {
            // If the primary orbit is occupied by a star we have two options.  Firstly we will try to put the primary
            // in some orbit of the companion star.
            const newPrimaryOrbit =
                (<StellarBodyStar>this.system.orbits[primaryOrbit]).orbitalPosition(
                    WorldGen.primaryOrbit(this.world, <StellarBodyStar>this.system.orbits[primaryOrbit], 0));

            if(newPrimaryOrbit < 0) {
                // If we can't do that, we will just adjust the original with the orbitalPosition method
                primaryOrbit = this.system.orbitalPosition(primaryOrbit);
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
                    satelliteOrbits = StellarBodyPlanet.maxSatelliteOrbitsForOrbit(primaryOrbit, uwp.size ?? 0);
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

                    uwp = this.makeOneWorld(random, { maxPopulation: this.uwp.population ?? 0, maxPopulationDigit: this.uwp.populationDigit ?? 1, size: 8});
                    satelliteOrbits = StellarBodyPlanet.maxSatelliteOrbitsForOrbit(primaryOrbit, uwp.size ?? 0);
                    if(!satelliteOrbits) {
                        uwp = undefined;
                    } else {
                        --this.planets;
                    }
                }
                if(uwp === undefined) {
                    logger.error(`Can't generate primary for ${this.world.hex}:${this.world.name} @${primaryOrbit} [${this.stars[0]}] - no satelite orbits`);
                    this.mainWorld = this.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}`,
                        StellarBodyPlanet.maxSatelliteOrbitsForOrbit(primaryOrbit, this.uwp.size), this.uwp, primarySystem)
                    primarySystem.setOrbit(primaryOrbit, this.mainWorld);
                    --this.planets;
                } else {
                    primarySystem.setOrbit(primaryOrbit,
                        this.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}`, satelliteOrbits, uwp, primarySystem));
                    const orbit = primarySystem.orbits[primaryOrbit]?.orbitalPosition(
                        this.satelliteOrbit(random, primaryOrbit, <StellarBodyPlanet>primarySystem.orbits[primaryOrbit])) ?? 0;
                    this.mainWorld = this.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}-${orbit}`,
                        StellarBodyPlanet.maxSatelliteOrbitsForOrbit(orbit, this.uwp.size), this.uwp, <any>primarySystem.orbits[primaryOrbit])
                    primarySystem.orbits[primaryOrbit]?.setOrbit(orbit, this.mainWorld);
                }
            } else {
                this.mainWorld = this.stellarBodyForPlanet(`${primarySystem.name}-${primaryOrbit}`,
                    StellarBodyPlanet.maxSatelliteOrbitsForOrbit(primaryOrbit, this.uwp.size), this.uwp, primarySystem);
                primarySystem.setOrbit(primaryOrbit, this.mainWorld);
                --this.planets;
            }

            this.mainWorld.primary = true;
            this.postProcessWorld(random, this.mainWorld);
        } else {
            logger.error(`Can't generate primary for ${this.world.hex}:${this.world.name} - no orbits`);
        }
    }


    generatePlanets() {
        // Initialize the main faction ... if there is no main faction then all worlds will be empty
        if(this.uwp.population > 0) {
            this.factions.set(this.world.owner, this.uwp);
        }

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

    static factionCode(idx: number) {
        return World.encodedValue(idx+10);
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
                    hex: this.world.hex + (suffix ? suffix : '-*') + WorldGen.factionCode(idx),
                    name: body.name + '-' + f.faction,
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

