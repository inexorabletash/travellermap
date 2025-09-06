export class Glyph {
    public code: string;
    public highlight: boolean;
    public order: number;
    public defaultColor: string;

    constructor(code: string | Glyph, highlight: boolean = false, order = 0, defaultColor?: string) {
        if (code instanceof Glyph) {
            this.code = code.code;
        } else {
            this.code = code;
        }
        this.highlight = highlight;
        this.order = order;
        this.defaultColor = defaultColor ?? (highlight ? 'red' : 'white');
    }

    public static readonly None = new Glyph("");
    public static readonly Diamond = new Glyph("\u2666"); // U+2666 (BLACK DIAMOND SUIT)
    public static readonly DiamondX = new Glyph("\u2756"); // U+2756 (BLACK DIAMOND MINUS WHITE X)
    public static readonly Circle = new Glyph("\u2022"); // U+2022 (BULLET); alternate:  U+25CF (BLACK CIRCLE)
    public static readonly Triangle = new Glyph("\u25B2"); // U+25B2 (BLACK UP-POINTING TRIANGLE)
    public static readonly Square = new Glyph("\u25A0"); // U+25A0 (BLACK SQUARE)
    public static readonly Star4Point = new Glyph("\u2726"); // U+2726 (BLACK FOUR POINTED STAR)
    public static readonly Star5Point = new Glyph("\u2605"); // U+2605 (BLACK STAR)
    public static readonly StarStar = new Glyph("**"); // Would prefer U+2217 (ASTERISK OPERATOR) but font coverage is poor
    public static readonly GasGiant = new Glyph("\u2022"); // NOTE this is not really used, the render exists for color handling

    // Research Stations
    public static readonly Alpha = new Glyph("\u0391", true);
    public static readonly Beta = new Glyph("\u0392", true);
    public static readonly Gamma = new Glyph("\u0393", true);
    public static readonly Delta = new Glyph("\u0394", true);
    public static readonly Epsilon = new Glyph("\u0395", true);
    public static readonly Zeta = new Glyph("\u0396", true);
    public static readonly Eta = new Glyph("\u0397", true);
    public static readonly Theta = new Glyph("\u0398", true);
    public static readonly Omicron = new Glyph("\u039F", true);

    // Other Textual
    public static readonly Prison = new Glyph("P", true);
    public static readonly Reserve = new Glyph("R");
    public static readonly ExileCamp = new Glyph("X");

    // TNE
    public static readonly HiverSupplyBase = new Glyph("\u2297");
    public static readonly Terminus = new Glyph("\u2297");
    public static readonly Interface = new Glyph("\u2297");

    // Starport
    public static readonly StarportA = new Glyph("A", true, 0, 'chartreuse');
    public static readonly StarportB = new Glyph("B", true, 0, 'lightgreen');
    public static readonly StarportC = new Glyph("C", false, 0, 'darkgreen');
    public static readonly StarportD = new Glyph("D", false, 0, 'darksalmon');
    public static readonly StarportE = new Glyph("E", false, 0, 'darkgoldenrod');
    public static readonly StarportX = new Glyph("X", false, 0, 'dimgrey');
    public static readonly StarportUnknown = new Glyph("*");


    public static fromStarport(rs: string | undefined): Glyph[] {
        if (rs == undefined) {
            return [];
        }
        const starports: Record<string, Glyph> = {
            A: this.StarportA,
            B: this.StarportB,
            C: this.StarportC,
            D: this.StarportD,
            E: this.StarportE,
            X: this.StarportX,
        }
        return [starports[rs] ?? this.StarportUnknown];
    }


    public static fromResearchCode(rs: string): Glyph {
        if (rs.length == 3) {
            const c = rs.charAt(2);
            return {
                'A': Glyph.Alpha,
                'B': Glyph.Beta,
                'G': Glyph.Gamma,
                'D': Glyph.Delta,
                'E': Glyph.Epsilon,
                'Z': Glyph.Zeta,
                'H': Glyph.Eta,
                'T': Glyph.Theta,
                'O': Glyph.Omicron,
            }[c] ?? Glyph.Gamma;
        }
        return Glyph.Gamma;
    }

    public static fromNoteCode(notes: Set<string>) {
        return [...notes].map(note => {
            if(note.startsWith('Rs')) {
                return Glyph.fromResearchCode(note);
            }
            return {
                'Re': Glyph.Reserve,
                'Px': Glyph.ExileCamp,
            }[note];
        }).filter(glyph => !!glyph);
    }

    static readonly BASE_CODES: Record<string, Glyph> = {
        C: new Glyph(Glyph.StarStar, false, 1), // Vargr Corsair Base
        IM_D: new Glyph(Glyph.Square, false, 1), // Imperial Depot
        D: new Glyph(Glyph.Square, true), // Depot
        E: new Glyph(Glyph.StarStar, false, 1), // Hiver Embassy
        K: new Glyph(Glyph.Star5Point, true, -1), // Naval Base
        M: Glyph.Star4Point, // Military Base
        N: new Glyph(Glyph.Star5Point, false, -1), // Imperial Naval Base
        O: new Glyph(Glyph.Square, true, -1), // K'kree Naval Outpost (non-standard)
        R: Glyph.StarStar, // Aslan Clan Base
        S: Glyph.Triangle, // Imperial Scout Base
        T: new Glyph(Glyph.Star5Point, true, -1),  // Aslan Tlaukhu Base
        V: Glyph.Circle, // Exploration Base
        ZH_W: new Glyph(Glyph.Diamond, true), // Zhodani Relay Station
        W: new Glyph(Glyph.Triangle, true), // Imperial Scout Waystation
        Zh_Z: Glyph.Diamond, // Zhodani Base (Special case for "Zh.KM")
        // TNE
        H: Glyph.HiverSupplyBase,
        I: Glyph.Interface,
        // T: Glyph.Terminus // unmatchable?
    };
    static readonly BASE_CODE_DEFAULT = Glyph.Circle; // Independent Base

    public static fromOneBaseCode(allegiance: string | undefined, code: string): Glyph {
        const value = this.BASE_CODES[`${allegiance ?? ''}_${code}`];
        if (value) {
            return value;
        }
        if (this.BASE_CODES[code]) {
            return this.BASE_CODES[code];
        }
        return this.BASE_CODE_DEFAULT;
    }


    public static fromBaseCode(allegiance: string, code: string): Glyph[] {
        if (allegiance == 'Zh' && code == 'KM') {
            return [this.fromOneBaseCode(allegiance, 'Z')];
        }

        return code.split('').map(ch => this.fromOneBaseCode(allegiance, ch))
            .sort((a, b) => a.order - b.order);
    }


}