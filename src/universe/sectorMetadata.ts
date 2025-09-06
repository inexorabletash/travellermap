import {XML} from "../xml.js";
import {World} from "./world.js";
import {combinePartials} from "../util.js";

export type CreditDetails = {
    Text?: string,
    SectorSource?: string,
    SectorMilieu?: string,
    SectorTags?: Set<string>,
    ProductPublisher?: string,
    ProductTitle?: string,
    ProductAuthor?: string,
    ProductRef?: string,
}
export type Border = {
    allegiance: string;
    color: string | undefined;
    label: string | undefined;
    labelPosition: string | undefined;
    wrapLabel: boolean;
    hexes: string[];
};
export type Route = {
    start: string;
    end: string;
    type?: string;
    allegiance?: string;
    color?: string;
    style?: string;
    startOffsetX?: number;
    startOffsetY?: number;
    endOffsetX?: number;
    endOffsetY?: number;
};
export type Allegiance = {
    code: string;
    legacy?: string;
    baseCode?: string;
    name: string;
    location?: string;
    borderColor?: string;
    routeColor?: string;
};

export class SectorMetadata {
    protected routes_: Route[] = [];
    protected borders_: Border[] = [];

    constructor(protected data: Record<string, any>) {
    }

    name(lang: string = '') {
        if (!lang) {
            return Object.values(this.data.name ?? {})?.[0] ?? '';
        }
        return this.data.name[lang] ?? '';
    }

    setName(name: string, lang: string = '') {
        this.data.name[lang] = name;
    }

    get x() {
        return this.data.sectorX;
    }

    set x(value: number) {
        this.data.sectorX = value;
    }

    get y() {
        return this.data.sectorY;
    }

    set y(value: number) {
        this.data.sectorY = value;
    }

    get abbreviation() {
        return this.data.abbreviation;
    }

    set abbreviation(value: string) {
        this.data.abbreviation = value;
    }

    get subsectors() {
        return this.data.subsectors;
    }

    get credits() {
        return this.data.credits;
    }

    get borders(): Border[] {
        return this.borders_;
    }

    get routes(): Route[] {
        return this.routes_;
    }

    get allegiances(): Record<string, Allegiance> {
        return Object.fromEntries(this.data.allegiances?.map(
            (v: XML) => {
                const name = v.value();
                const code = v.path('@_Code').value();
                const borderColor = v.path('@_BorderColor').value();
                const routeColor = v.path('@_RouteColor').value();
                return [code, {code, name, borderColor, routeColor}];
            }
        ) ?? []);
    }

    buildBorders(xml: XML[]) {
        const knownHexes = new Set(this.borders_.flatMap(b => b.hexes));
        const newBorders = xml.map(
            (v: XML) => {
                const allegiance = v.path('@_Allegiance').value() ?? 'Na';
                const label = v.path('@_Label').value();
                const labelPosition = v.path('@_LabelPosition').value();
                const wrapLabel = !!(v.path('@_WrapLabel').value() ?? 'true');
                const color = v.path('@_Color').value();
                const hexes = v.value().toString().split(/\s+/);
                return {allegiance, label, labelPosition, wrapLabel, hexes, color} as Border;
            }
        ).filter(border => border.hexes.find(hex => !knownHexes.has(hex)) !== undefined);
        this.borders_ = [...this.borders_, ...newBorders];
    }

    buildRoutes(xml: XML[]) {
        const newRoutes = xml.map(
            (v: XML) => {
                const start = World.zeroPad(v.path('@_Start').value(), 4);
                const end = World.zeroPad(v.path('@_End').value(), 4);
                const type = v.path('@_Type').value();
                const color = v.path('@_Color').value();
                const style = v.path('@_Style').value();
                const allegiance = v.path('@_Allegiance')?.value();
                const endOffsetX = v.path('@_EndOffsetX')?.value();
                const endOffsetY = v.path('@_EndOffsetY')?.value();
                const startOffsetX = v.path('@_StartOffsetX')?.value();
                const startOffsetY = v.path('@_StartOffsetY')?.value();
                return {
                    start,
                    end,
                    type,
                    color,
                    style,
                    allegiance,
                    startOffsetX,
                    startOffsetY,
                    endOffsetX,
                    endOffsetY,
                };
            }).filter(
            (route: Route) => {
                if(this.routes_.findIndex(v => v.start === route.start && v.end === route.end)>=0) {
                    return false;
                }
                return true;
            });

        this.routes_ = [ ...this.routes_, ...newRoutes];
    }

    static parseCredits(xml: XML): CreditDetails {
        const Text = xml.path('Credits').value();
        //    <DataFile Source="Traveller 5 Second Survey" Milieu="M1105" Type="TabDelimited">Deneb.tab</DataFile>
        //    <MetadataFile Author="Jeff Zeitlin" Source="The Zhodani Base" Ref="http://zho.berka.com/data/CLASSIC/sector.pl?sector=STARSEND">Star's End.xml</MetadataFile>
        //      <Product Publisher="Mongoose Publishing" Author="Gareth Hanrahan" Title="The Pirates of Drinax, Book 2: The Trojan Reach" Ref="https://www.mongoosepublishing.com/products/the-pirates-of-drinax" />
        const SectorSource = xml.path('DataFile.@_Source').value();
        const SectorMilieu = xml.path('DataFile.@_Milieu').value();
        const SectorTags = new Set<string>(xml.path('@_Tags').value()?.split(/\s+/) ?? []);
        const ProductPublisher = xml.path('Product.@_Publisher').value();
        const ProductTitle = xml.path('Product.@_Title').value();
        const ProductAuthor = xml.path('Product.@_Author').value();
        const ProductRef = xml.path('Product.@_Ref').value();
        return {
            Text,
            SectorSource,
            SectorMilieu,
            SectorTags,
            ProductAuthor,
            ProductPublisher,
            ProductRef,
            ProductTitle
        };
    }

    processSectorMetadata(xml: XML) {
        const sectorX = xml.path('X').value();
        const sectorY = xml.path('Y').value();
        const names = SectorMetadata.extractNameMap(xml.path('Name'));
        const allegiances = xml.path('Allegiances.Allegiance').array();
        const subsectors = SectorMetadata.extractIndexedMap(xml.path('Subsectors.Subsector').array());

        const styleSheet = xml.path('Stylesheet').value();
        const credits = SectorMetadata.parseCredits(xml);
        const abbreviation = xml.path('@_Abbreviation').value();

        this.buildBorders(xml.path('Borders.Border').array());
        this.buildRoutes(xml.path('Routes.Route').array());

        this.data.name = combinePartials(this.data.name ?? {}, names);
        this.data.sectorX = sectorX ?? this.data.sectorX;
        this.data.sectorY = sectorY ?? this.data.sectorY;
        if (allegiances.length > 0) {
            this.data.allegiances = allegiances;
        }
        this.data.subsectors = combinePartials(this.data.subsectors, subsectors);
        if (credits) {
            this.data.credits = combinePartials(this.data.credits, credits);
        }
        if (abbreviation) {
            this.data.abbreviation = abbreviation;
        }
    }

    async loadMetadataFile(file: string): Promise<void> {
        const xml = await XML.fromFile(file);
        this.processSectorMetadata(xml.path('Sector'));
    }

    static extractIndexedMap(xml: XML[], keyAttribute: string = "@_Index"): Record<string, string> {
        const rv: Record<string, string> = {};
        for (const ss of xml) {
            const key = ss.path(keyAttribute).value();
            if (key === undefined) {
                continue;
            }
            rv[key] = ss.value();
        }
        return rv;
    }

    static extractNameMap(xml: XML) {
        return Object.fromEntries(xml.array().map(entry => [entry.path('@_Lang').value() ?? '', entry.value()]));
    }

}