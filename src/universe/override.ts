import {Allegiance, Border, Route} from "./sectorMetadata.js";

export type Override = {
    sector: OverrideSector[] | OverrideSector | string;
    allegiance: OverrideAllegiance[] | OverrideAllegiance;
    border: OverrideBorder[] | OverrideBorder;
    route: OverrideRoute[] | OverrideRoute;
    world: OverrideWorld[] | OverrideWorld;
}

export type OverrideCommon = {
    sector: string;
}

export type OverrideSector = {
    abbreviation?: string;
    name?: string;
    x?: number;
    y?: number;
    subsector: Record<string,string>;
} & OverrideCommon;

export type OverrideAllegiance = Allegiance;

export type OverrideBorder = Border & {
    hexes?: string[];
    replace?: string;
} & OverrideCommon;

export type OverrideRoute = Route & {
    replace?: string|[string,string];
} & OverrideCommon;

export type OverrideWorld = {
    hex: string|number;
    name?: string;
    uwp?: string;
    bases?: string;
    notes?: string[]|string;
    zone?: string;
    pbg?: string|number;
    allegiance?: string;
    stars?: string;
    ix?: string;
    ex?: string;
    cx?: string;
    nobility?: string;
    w?: string|number;
    ru?: string|number;
    delete?: boolean;
} & OverrideCommon;

