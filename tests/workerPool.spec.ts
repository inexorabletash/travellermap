import {jest} from '@jest/globals';
import path from "node:path";

let WorkerPool: any;
let SCRIPT: string;

jest.unstable_mockModule('../build/logger.js', () => ({
    default: {
        info: jest.fn(),
        debug: jest.fn(),
        error: jest.fn(),
        warn: jest.fn(),
    },
}));

const wp = await import("../build/workerPool.js");
WorkerPool = wp.WorkerPool;

const FILENAME = import.meta.filename;

SCRIPT = path.join(path.dirname(FILENAME), 'workerPoolScript.js');

test('Worker Pool Create', () => {
    const workerPool = new WorkerPool(8, SCRIPT);

    expect(workerPool).toBeDefined();
    expect(workerPool.workerCount()).toBe(8);
});

test('Worker Pool Execute single task', async () => {
    const workerPool = new WorkerPool(8, SCRIPT);
    const PAYLOAD = {payload: 'Hello'};

    const result = await workerPool.invoke(PAYLOAD)
    expect(result).toEqual({message: PAYLOAD});
});

test('Worker Pool with empty payload', async () => {
    const workerPool = new WorkerPool(8, SCRIPT);
    const PAYLOAD = undefined;

    await expect(() => workerPool.invoke(PAYLOAD)).rejects.toThrow();
});



test('Worker Pool Execute single exception', async () => {
    const workerPool = new WorkerPool(8, SCRIPT);
    const PAYLOAD = {error: 'Hello', path:'path'};

    await expect(() => workerPool.invoke(PAYLOAD)).rejects.toThrow();
});

test('Worker Pool of 1 completes 3 requests', async () => {
    const workerPool = new WorkerPool(1, SCRIPT);

    const reqs = ['req-1', 'req-2', 'req-3'];
    const result = await Promise.all(reqs.map(payload => workerPool.invoke({payload, path:'dummy'})));
    const match = result.map(x => x?.message?.payload);

    expect(reqs).toEqual(match);
});

test('Worker Pool of 2 completes 3 requests with correct delays', async () => {
    const workerPool = new WorkerPool(2, SCRIPT);
    async function runForDataWithDelay(payload: string) {
        const start = Date.now();
        const req = {payload, delay:1000, path:'delay?'};
        const result = await workerPool.invoke(req);
        expect(result).toEqual({message: req});
        return Date.now() - start;
    }

    const result = await Promise.all(['req-1', 'req-2', 'req-3'].map(req => runForDataWithDelay(req)));
    console.log(result);
    expect(result[0]).toBeLessThan(1500);
    expect(result[1]).toBeLessThan(1500);
    expect(result[2]).toBeGreaterThan(2000);
});


test('Worker Pool works after restart', async () => {
    const workerPool = new WorkerPool(3, SCRIPT);

    const reqs = ['req-1', 'req-2', 'req-3', 'req-4', 'req-5', 'req-6', 'req-7', 'req-8'];
    const result = await Promise.all(reqs.map(payload => workerPool.invoke({payload, path:'dummy'})));
    const match = result.map(x => x?.message?.payload);

    expect(reqs).toEqual(match);
    workerPool.restart();

    // We expect anything submitted immediately after the restart to throw
    const PAYLOAD = {payload: 'Hello'};
    expect(() => workerPool.invoke(PAYLOAD)).rejects.toThrow();

    await new Promise((resolve, reject) => {
        setTimeout(resolve, 100);
    })

    const reqs2 = ['req-1a', 'req-2a', 'req-3a', 'req-4a', 'req-5a', 'req-6a', 'req-7a', 'req-8a'];
    const result2 = await Promise.all(reqs2.map(payload => workerPool.invoke({payload, path:'dummy'})));
    const match2 = result2.map(x => x?.message?.payload);

    expect(reqs2).toEqual(match2);

    expect(workerPool.workerCount()).toEqual(3);
    expect(workerPool.totalStarted()).toEqual(6);
});


// restart()
