import {StellarBodyType, UWPElements, WorldGen} from "./worldGen.js";

export type RangeOrFunction =
    string |
    ((value: number, data: StellarBodyType, worldUWP: UWPElements, mwUWP: UWPElements) => boolean);

export type Classification = {
    code: string;
    definition: string;
    siz?: RangeOrFunction;
    atm?: RangeOrFunction;
    hyd?: RangeOrFunction;
    pop?: RangeOrFunction;
    gov?: RangeOrFunction;
    law?: RangeOrFunction;
    tl?: RangeOrFunction;
    other?: (data: StellarBodyType, worldUWP: UWPElements, mwUWP: UWPElements) => boolean;
}


export const CLASSIFICATIONS: Classification[] = [
    { code: 'As', definition: 'Asteroid Belt', siz: '0', atm: '0', hyd: '0', other: (body, worldUWP) => !body.uwp?.notes?.has('Wlt')},
    { code: 'De', definition: 'Desert', atm: '23456789', hyd: '0'},
    { code: 'Fl', definition: 'Fluid', atm: 'ABC', hyd: '123456789A'},
    { code: 'Ga', definition: 'Garden World', siz: '678', atm: '568'},
    { code: 'He', definition: 'Hellworld', siz: '3456789ABC', atm: '2479ABC', hyd: '012' },
    { code: 'Ic', definition: 'Ice Capped', atm: '01', hyd: '123456789A' },
    { code: 'Oc', definition: 'Ocean World', siz: 'ABCDEF', atm: '3456789DEF', hyd: 'A' },
    { code: 'Va', definition: 'Vacuum World', atm: '0' },
    { code: 'Wa', definition: 'Water World', siz: '3456789', atm: '3456789DEF', hyd: 'A' },

    { code: 'Ba', definition: 'Barren', pop: '0' },
    { code: 'Lo', definition: 'Low Population', pop: '123' },
    { code: 'Ni', definition: 'Non-industrial', pop: '456' },
    { code: 'Ph', definition: 'Pre-High population', pop: '8' },
    { code: 'Hi', definition: 'High Population', pop: '9ABCDEF' },

    { code: 'Pa', definition: 'Pre-Agricultural', atm: '456789', hyd: '45678', pop: '48' },
    { code: 'Ag', definition: 'Agricultural', atm: '456789', hyd: '45678', pop: '567' },
    { code: 'Na', definition: 'Non-Agricultural', atm: '0123', hyd: '0123', pop: '6789ABCDEF' },
    { code: 'Px', definition: 'Prison / Exile Camp', atm: '23AB', hyd: '12345', pop: '3456', law: '6789ABCDEFGHJK' },
    { code: 'Pi', definition: 'Pre-Industrial', atm: '012479', pop: '78' },
    { code: 'In', definition: 'Industrial', atm: '012479ABC', pop: '9ABCDEF' },
    { code: 'Po', definition: 'Poor', atm: '2345', hyd: '0123' },
    { code: 'Pr', definition: 'Pre-Rich', atm: '68', pop: '89'},
    { code: 'Ri', definition: 'Rich', atm: '68', pop: '678' },

    { code: 'Fa', definition: 'Farming', atm: '456789', hyd: '45678', pop: '23456', other: (body, worldUWP) => !body.primary && !!body.uwp?.notes?.has('Hz')},
    { code: 'Mi', definition: 'Mining', pop: '23456',
        other: (body, worldUWP, mw) => !body.primary && WorldGen.checkInCodeList(mw.atmosphere, '012479ABC') && WorldGen.checkInCodeList(mw.population, '9ABCDEF'),
    }, // Not MW, but MW == industrial?
    //{ code: 'Mr', definition: 'Military Rule' }, ... possibly if O:.... and related world LL is significantly higher than parent
    { code: 'Pe', definition: 'Penal Colony', atm: '23AB', hyd: '12345', pop: '3456', gov: '6', law: '6789ABCDEFGHJK', other: (body) => !body.primary && !body.uwp?.notes?.has('Re') },
];

export function checkOneClassificationField(value: number|undefined, target: RangeOrFunction|undefined, body: StellarBodyType, worldUWP: UWPElements, mwUWP: UWPElements) {
    if(!target) {
        return true;
    }
    if(typeof target === 'function') {
        return target(value ?? -1, body, worldUWP, mwUWP);
    }
    return WorldGen.checkInCodeList(value, target);
}


export function processClassification(body: StellarBodyType, worldUWP: UWPElements, mwUWP: UWPElements, classification: Classification) {
    if(checkOneClassificationField(worldUWP.size, classification.siz, body, worldUWP, mwUWP) &&
        checkOneClassificationField(worldUWP.atmosphere, classification.atm, body, worldUWP, mwUWP) &&
        checkOneClassificationField(worldUWP.hydrographic, classification.hyd, body, worldUWP, mwUWP) &&
        checkOneClassificationField(worldUWP.population, classification.pop, body, worldUWP, mwUWP) &&
        checkOneClassificationField(worldUWP.govt, classification.gov, body, worldUWP, mwUWP) &&
        checkOneClassificationField(worldUWP.population, classification.law, body, worldUWP, mwUWP) &&
        checkOneClassificationField(worldUWP.techLevel, classification.tl, body, worldUWP, mwUWP) &&
        (!classification.other || classification.other(body, worldUWP, mwUWP))
    ) {
        worldUWP.notes?.add(classification.code);
    }
}

export function processClassifications(body: StellarBodyType, worldUWP: UWPElements, mwUWP: UWPElements) {
    for(const c of CLASSIFICATIONS) {
        processClassification(body, worldUWP, mwUWP, c);
    }
}
