import fs from "node:fs";
import {XMLParser} from "fast-xml-parser";


export class XML {
    constructor(protected path_: string, protected data: any) {
    }

    static async fromFile(name: string): Promise<XML> {
        const parser = new XMLParser({
            parseAttributeValue: true,
            ignoreAttributes: false,
            alwaysCreateTextNode: true,
        });
        let xmlRaw;
        try {
            xmlRaw = await fs.promises.readFile(name);
        } catch(e) {
            return new XML('^', {});
        }
        const xmlData = parser.parse(xmlRaw);

        return new XML('^', xmlData);
    }

    path(path: string): XML {
        const pathSplit = path.split('.');
        let data = this.data;

        for(const pathComp of pathSplit) {
            data = this.followComp(data, pathComp);
        }
        return new XML(this.path_ + '.' + path, data);
    }

    value(): any {
        const value = XML.extractValue(this.data);
        if(value === null) {
            throw new Error(`${this.path_}: data is an array (${JSON.stringify(this.data)})`);
        }
        return value;
    }

    array(): Array<XML> {
        if(this.data === undefined) {
            return [];
        }
        if(Array.isArray(this.data)) {
            return this.data.map((entry: any,idx) => new XML(`${this.path_}[${idx}]`, entry));
        }
        return [this];
    }

    arrayValue(filter?: (value: any) => boolean): any[] {
        return XML.extractArrayValue(this.data, filter);
    }

    static extractArrayValue(data: any, filter?: (value: any) => boolean): any[] {
        if(!Array.isArray(data)) {
            return [ this.extractValue(data) ];
        }
        return data.filter(filter === undefined ? () => true : filter)
            .flatMap((elem: any) => this.extractArrayValue(elem));
    }

    static extractValue(data: any): any {
        if(Array.isArray(data)) {
            return null;
        }
        if(typeof(data) === 'object') {
            return data['#text'];
        }
        return data;

    }

    followComp(data: any, comp: string): any {
        if(data === undefined) {
            return undefined;
        }
        if(Array.isArray(data)) {
            return data.flatMap(value => this.followComp(value,comp)).filter(value => value !== undefined);
        }
        if(typeof(data) === 'object') {
            return data[comp];
        }
        return undefined;
    }

}