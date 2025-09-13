import express, {Express} from "express";
import {WorkerPool} from "../build/workerPool.js";
import {anyFunction, anyNumber, anything, capture, instance, mock, verify, when} from "ts-mockito";
import * as http from "node:http";
import {WebServer, WebServerFactories} from "../build/webServer.js";
import {MessagePort} from "worker_threads";


let mockExpressClass : Express;
let mockExpress : Express;
let mockServerClass: http.Server;
let mockServer = http.Server;
let workerPoolClass: WorkerPool;
let workerPool: WorkerPool;

const PORT=1234;

let initCalled = 0;
let warmCalled = 0;

let Factories!: WebServerFactories;

class GetThis {
    getThis() { return this; }
}

function init() {
    ++initCalled;
}
function warm() {
    ++warmCalled;
}

beforeEach(() => {
    mockExpressClass = mock<Express>();
    mockServerClass = mock<http.Server>();
    workerPoolClass = mock(WorkerPool);
    mockExpress = instance(mockExpressClass);
    mockServer = <any>instance(mockServerClass);
    workerPool = instance(workerPoolClass);

    when(mockExpressClass.listen(PORT, anyFunction())).thenCall((port,initFn) => {
        initFn();
        return mockServer;
    });


    Factories = {
        expressFactory: () => mockExpress,
        workerPoolFactory: count => {
            return workerPool;
        },
        isMainThread: () => true,
        threadId: () => 0,
        parentPort: () => null,
    }

});

let acceptOptions;
let acceptsValue = 'accept/something';
const VERBS: string[] = ['get','put','post','delete'];
const RAWREQUEST = {
    accepts: (options: any) => {
        acceptOptions = options;
        return acceptsValue;
    },
    path: '/something',
    query: {
        param1: 'xxx'
    },
    params: {
        param2: 'yyy'
    },
    body: {
        some: 'value'
    },
    headers: {
        'Content-Type': 'application/json'
    }
}
describe('Standalone Tests', () => {
    const WORKERS = 0;

    test('init', async () => {
        const server = new WebServer(() => init(), WORKERS, Factories);
        await server.start(PORT, async () => warm());

        expect(warmCalled).toEqual(1);
        expect(initCalled).toEqual(1);
    });

    test('JSON', async () => {
        const server = new WebServer(() => init(), WORKERS, Factories);
        VERBS.forEach(verb => {
            server.registerJson(`/${verb}`, <any>verb, async (req) => ({ [verb]: req}));
        })

        await server.start(1234, async () => {});

        await Promise.all(VERBS.map(async verb => {
            let mockResponseClass: express.Response;
            mockResponseClass = <any>mock<Express.Response>();
            when((<any>mockResponseClass.statusCode)(anyNumber())).thenReturn(0);

            verify((<any>mockExpressClass)[verb](`/${verb}`, anyFunction())).once();
            const [_, fn] = capture((<any>mockExpressClass)[verb]).last();
            const resp = instance(mockResponseClass);
            await (<any>fn)(RAWREQUEST, resp);
            const [v] = capture(mockResponseClass.json).first();
            expect(resp.statusCode).toEqual(200);
            const expected = {
                ...RAWREQUEST,
                matchPath: `/${verb}`,
                contentType: undefined,
            };
            delete (<any>expected)['accepts'];
            expect(v).toEqual({[verb]: expected});
        }));

    });

    test('Raw', async () => {
        const server = new WebServer(() => init(), WORKERS, Factories);
        VERBS.forEach(verb => {
            server.registerRaw(`/${verb}`, <any>verb, async (req) => ({ body: verb, statusCode: 123, contentType: 'some/type'}));
        })

        await server.start(1234, async () => {});

        await Promise.all(VERBS.map(async verb => {
            let mockResponseClass: express.Response;
            mockResponseClass = <any>mock<Express.Response>();
            when((<any>mockResponseClass.statusCode)(anyNumber())).thenReturn(0);
            when((<any>mockResponseClass.statusCode)(anyNumber())).thenReturn(0);

            verify((<any>mockExpressClass)[verb](`/${verb}`, anyFunction())).once();
            const [_, fn] = capture((<any>mockExpressClass)[verb]).last();
            const resp = instance(mockResponseClass);
            await (<any>fn)(RAWREQUEST, resp);

            const [body] = capture(mockResponseClass.send).first();
            const [contentType] = capture(mockResponseClass.contentType).first();
            expect(resp.statusCode).toEqual(123);
            expect(body).toEqual(verb);
            expect(contentType).toEqual('some/type');
        }));

    });

    test('static', async () => {
        const server = new WebServer(() => init(), WORKERS, Factories);
        const staticFn = () => {};
        (<any>express).static = (dir: string) => { expect(dir).toEqual('/dir'); return staticFn; }
        server.staticRoute('/static', '/dir');
        verify(mockExpressClass.use('/static', staticFn)).once();
    });

});

describe('Server tests', () => {
    const WORKERS = 8;
    test('JSON', async () => {
        when(workerPoolClass.invoke(anything())).thenCall(data => ({
            ...data,
            invoked: true,
        }))

        const server = new WebServer(() => init(), WORKERS, Factories);
        VERBS.forEach(verb => {
            server.registerJson(`/${verb}`, <any>verb, async (req) => ({ [verb]: req}));
        })

        await server.start(1234, async () => {});

        await Promise.all(VERBS.map(async verb => {
            let mockResponseClass: express.Response;
            mockResponseClass = <any>mock<Express.Response>();
            when((<any>mockResponseClass.statusCode)(anyNumber())).thenReturn(0);

            verify((<any>mockExpressClass)[verb](`/${verb}`, anyFunction())).once();
            const [_, fn] = capture((<any>mockExpressClass)[verb]).last();
            const resp = instance(mockResponseClass);
            await (<any>fn)(RAWREQUEST, resp);
            const [v] = capture(mockResponseClass.json).first();
            expect(resp.statusCode).toEqual(200);
            const expected = {
                ...RAWREQUEST,
                contentType: undefined,
                matchPath: `/${verb}`,
                invoked: true,
            };
            delete (<any>expected)['accepts'];
            expect(v).toEqual(expected);
        }));

    });

    test('Raw', async () => {
        const server = new WebServer(() => init(), WORKERS, Factories);
        VERBS.forEach(verb => {
            server.registerRaw(`/${verb}`, <any>verb, async (req) => ({ body: verb, statusCode: 123, contentType: 'some/type'}));
        })
        when(workerPoolClass.invoke(anything())).thenCall(data => ({
            ...data,
            body: { invoked: true },
            contentType: 'someother/type',
            statusCode: 456
        }))

        await server.start(1234, async () => {});

        await Promise.all(VERBS.map(async verb => {
            let mockResponseClass: express.Response;
            mockResponseClass = <any>mock<Express.Response>();
            when((<any>mockResponseClass.statusCode)(anyNumber())).thenReturn(0);
            when((<any>mockResponseClass.statusCode)(anyNumber())).thenReturn(0);

            verify((<any>mockExpressClass)[verb](`/${verb}`, anyFunction())).once();
            const [_, fn] = capture((<any>mockExpressClass)[verb]).last();
            const resp = instance(mockResponseClass);
            await (<any>fn)(RAWREQUEST, resp);

            const [body] = capture(mockResponseClass.send).first();
            const [contentType] = capture(mockResponseClass.contentType).first();
            expect(resp.statusCode).toEqual(456);
            expect(body).toEqual({invoked: true});
            expect(contentType).toEqual('someother/type');
        }));

    });

    test('static', async () => {
        const server = new WebServer(() => init(), WORKERS, Factories);
        const staticFn = () => {};
        (<any>express).static = (dir: string) => { expect(dir).toEqual('/dir'); return staticFn; }
        server.staticRoute('/static', '/dir');
        verify(mockExpressClass.use('/static', staticFn)).once();
    });

})


describe('Worker Tests', () => {
    const WORKERS = 8;

    test('JSON', async () => {
        Factories.isMainThread = () => false;
        Factories.threadId = () => 99;
        Factories.parentPort = () => null;
        const server = new WebServer(() => init(), WORKERS, Factories);

        VERBS.forEach(verb => {
            const fn = async (req: any) => ({ body: verb, statusCode: 123, contentType: 'some/type'});
            server.registerRaw(`/${verb}`, <any>verb, fn);
            expect((<any>server).registeredFunctions[`/${verb}`]).toEqual(fn);
        })

        VERBS.forEach(verb => {
            verify((<any>mockExpressClass)[verb](`/${verb}`, anyFunction())).never();
        });

    });

    test('Raw', async () => {
        Factories.isMainThread = () => false;
        Factories.threadId = () => 99;
        Factories.parentPort = () => null;
        const server = new WebServer(() => init(), WORKERS, Factories);

        VERBS.forEach(verb => {
            const fn = async (req: any) => ({ body: verb, statusCode: 123, contentType: 'some/type'});
            server.registerRaw(`/${verb}`, <any>verb, fn);
            expect((<any>server).registeredFunctions[`/${verb}`]).toEqual(fn);
        })

        VERBS.forEach(verb => {
            verify((<any>mockExpressClass)[verb](`/${verb}`, anyFunction())).never();
        });

    });

    test('RequestHandling', async () => {
        const parentPortMock = mock<MessagePort>();
        const ppi = instance(parentPortMock);
        let messageFn!: (data: any) => void;
        when(parentPortMock.on('message',anyFunction())).thenCall((_, fn) => { messageFn = fn; return ppi; });
        when(parentPortMock.on('close',anyFunction())).thenCall(() => ppi);

        Factories.isMainThread = () => false;
        Factories.threadId = () => 99;
        Factories.parentPort = () => instance(parentPortMock);
        const server = new WebServer(() => init(), WORKERS, Factories);
        await server.start(1234, async () => {});

        expect(messageFn).toBeDefined();

        (<any>server).registeredFunctions[`xxx`] = (data:any) => ({processed: data});
        await messageFn?.({ ...RAWREQUEST, matchPath: 'xxx'});

        verify(parentPortMock.postMessage(anything())).once();
        const [msg] = capture(parentPortMock.postMessage).first();
        expect(msg).toEqual({processed:{ ...RAWREQUEST, matchPath: 'xxx'}});
    });


    test('static', async () => {
        Factories.isMainThread = () => false;
        Factories.threadId = () => 99;
        Factories.parentPort = () => null;
        const server = new WebServer(() => init(), WORKERS, Factories);
        server.staticRoute('/static', '/dir');
        verify(mockExpressClass.use('/static', anything())).never();
    });

});
