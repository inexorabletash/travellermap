import {anyNumber, anyString, anything, capture, instance, mock, verify, when} from "ts-mockito";
import {Universe} from "../../build/universe.js";
import {TileRender} from "../../build/render/tileRender.js";
import {Renderer} from "../../build/render/renderer.js";
import {Canvas, CanvasRenderingContext2D} from "canvas";
import {Sector} from "../../build/universe/sector.js";
import * as util from "node:util";
import {World} from "../../build/universe/world.js";
import {HEX_X_SCALE} from "../../src/render/constants";


function setupMocks() {
    const universe = mock(Universe);
    const renderer = mock(Renderer);
    const canvas = mock(Canvas);
    const buffer = Buffer.alloc(0);
    const graphicsContext = mock(CanvasRenderingContext2D);
    const contextInstance = instance(graphicsContext);

    when(renderer.createCanvas(anyNumber(), anyNumber(), anything())).thenReturn(instance(canvas));
    when(canvas.getContext('2d', anything())).thenReturn(contextInstance);
    when(canvas.getContext('2d')).thenReturn(contextInstance);
    when(canvas.toBuffer(anything())).thenReturn(buffer);

    return {
        universe,
        renderer,
        canvas,
        graphicsContext,
        contextInstance,
        buffer,
    }
}

function paramToString(v: any): string {
    if(v === undefined) {
        return 'undefined';
    } else if(v === null) {
        return 'null';
    } else if(typeof v === 'object') {
        if(Array.isArray(v)) {
            return v.map(vv => paramToString(vv)).toString();
        }
        return `[Object]`;
    } else {
        return v.toString();
    }
}

function allCalls(mock: any) {
    const rv = [];
    for(let i = 0; true; ++i) {
        try {
            const m = capture(mock).byCallIndex(i);
            rv.push(m.map(v => paramToString(v)));
        } catch(e) {
            return rv;
        }
    }
}

const options = 58359;
const optionsExpect = options + TileRender.XRenderRoutes;

test(`Basic sector render test`, () => {
    const mocks = setupMocks();
    const sector__4_0 = mock(Sector);
    const sector__4_0Instance = instance(sector__4_0);
    when(mocks.universe.getSector(-4, 0)).thenReturn(sector__4_0Instance);

    const tile = new TileRender(() => instance(mocks.renderer));

    const result = tile.renderTile(instance(mocks.universe), {
        options,
        style: 'poster',
        dpr: 1.5,
        x: -27,
        y: 0,
        scale: 64,
    });

    expect(result).toBe(mocks.buffer);
    verify(mocks.renderer.createCanvas(384,384,undefined)).once();
    verify(mocks.renderer.setupSectorTransform(mocks.contextInstance, -4.292341855040817, 0, 96)).once();
    verify(mocks.renderer.setupSectorTransform(mocks.contextInstance, -4.292341855040817, 40, 96)).once();

    verify(mocks.renderer.renderSectorBorder(mocks.contextInstance, -4, 0, 96, optionsExpect)).once();
    verify(mocks.renderer.renderSectorName(mocks.contextInstance, sector__4_0Instance, -4, 0, 96, optionsExpect)).once();
    verify(mocks.renderer.renderRoutes(mocks.contextInstance, sector__4_0Instance, -4, 0, 96, optionsExpect)).once();
    verify(mocks.renderer.renderBorders(mocks.contextInstance, sector__4_0Instance, -4, 0, 96, optionsExpect)).once();

    verify(mocks.renderer.renderSectorBorder(mocks.contextInstance, -4, 1, 96, optionsExpect)).once();
    verify(mocks.renderer.renderSectorName(mocks.contextInstance, anything(), -4, 1, 96, optionsExpect)).never();
    verify(mocks.renderer.renderRoutes(mocks.contextInstance, anything(), -4, 1, 96, optionsExpect)).never();
    verify(mocks.renderer.renderBorders(mocks.contextInstance, anything(), -4, 1, 96, optionsExpect)).never();

    verify(mocks.renderer.setBackground(mocks.contextInstance, 4,4, 96, optionsExpect)).once();


    //console.log(allCalls(mocks.renderer.renderSectorBorder));
});


test(`Basic hex render test`, () => {
    const mocks = setupMocks();

    const tile = new TileRender(() => instance(mocks.renderer));

    //const sector__4_0 = mock(Sector);
    //const sector__4_0Instance = instance(sector__4_0);
    //when(mocks.universe.getSector(-4, 0)).thenReturn(sector__4_0Instance);
    //const world__120_1 = mock(World);
    const world__120_1Instance = {} as World;
    when(mocks.universe.lookupWorld(-120,1)).thenReturn(world__120_1Instance);

    const result = tile.renderTile(instance(mocks.universe), {
        options,
        style: 'poster',
        dpr: 1.5,
        x: -27,
        y: 0,
        scale: 64,
    });


    expect(result).toBe(mocks.buffer);
    verify(mocks.renderer.createCanvas(384,384,undefined)).once();

    const baseX = 0.7076581449591828;
    for(let x = -1; x < 4; ++x) {
        for(let y = 0; y < 5; ++y) {
            verify(mocks.renderer.setupHexTransform(mocks.contextInstance, x + baseX, y, (x + 2) % 2, 96)).once();

            verify(mocks.renderer.renderHexBorder(mocks.contextInstance, <any>(x==3 && y==1 ? world__120_1Instance: null), x-123, y, 96, optionsExpect)).once();
        }
    }
    for(const world of [ world__120_1Instance, anything() ]) {
        verify(mocks.renderer.renderWorld(mocks.contextInstance, world, 96, optionsExpect)).once();
        verify(mocks.renderer.renderZone(mocks.contextInstance, world, 96, optionsExpect)).once();
        verify(mocks.renderer.renderHexName(mocks.contextInstance, world, 96, optionsExpect)).once();
        verify(mocks.renderer.renderWorldUpp(mocks.contextInstance, world, 96, optionsExpect)).once();
        verify(mocks.renderer.renderIcons(mocks.contextInstance, world, 96, optionsExpect)).once();
        verify(mocks.renderer.renderWorldName(mocks.contextInstance, world, 96, optionsExpect)).once();

    }

});