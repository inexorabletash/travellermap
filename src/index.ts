#!/usr/bin/env -S node --enable-source-maps

import {WebServer, WireRequest} from "./webServer.js";
import path from "node:path";
import {TileRender} from "./render/tileRender.js";
import {Universe} from "./universe.js";
import {FilePoller} from "./filePoller.js";
import logger from './logger.js';
import express from "express";
import {WorkerPool} from "./workerPool.js";
import {isMainThread, parentPort, threadId} from "node:worker_threads";
import {addListeners} from "./controller.js";
import {PosterRenderer} from "./render/posterRenderer.js";
import {program} from 'commander';


process.on('SIGINT', () => {
    console.warn('Received SIGINT - shutting down');
    process.exit(0);
});

process.on('SIGTERM', () => {
    console.warn('Received SIGINT - shutting down');
    process.exit(0);
});


WorkerPool.initOptions(() => {
    program
        .option('--workers <number>', 'number of workers', process.env['WORKERS'] ?? '8')
        .option('--port <number', 'listen port', '8000')
        .option('--sector <string>', 'sector directory', path.join(process.cwd(), 'static', 'res', 'Sectors'))
        .option('--override <string>', 'override directory', path.join(process.cwd(), 'static', 'res', 'overrides'))
    ;
    program.parse();
    return program.opts();
});

const opts = WorkerPool.opts;
Universe.baseDir = opts['sector'];
Universe.OVERRIDE_DIR = opts['override'];
const workers = Number.parseInt(opts['workers']);
const port = Number.parseInt(opts['port']);

const server = new WebServer(() => logger.info(`Server started on port ${port} with ${workers} workers`), workers,
    {
        expressFactory: () => express(),
        workerPoolFactory: workers => new WorkerPool(workers),
        isMainThread: () => isMainThread,
        parentPort: () => parentPort,
        threadId: () => threadId,
    });
const tileRenderer = new TileRender(() => new PosterRenderer());

addListeners(server, tileRenderer);

await server.start(port, async () => {
    if(server.isMaster()) {
        new FilePoller(Universe.OVERRIDE_DIR,
            //fs.watch(Universe.OVERRIDE_DIR, {encoding: 'utf8', recursive: true, },
            (change, file) => {
                const bn = path.basename(file);
                if(file && path.extname(file) === '.yml' && !bn.startsWith('.') && !bn.startsWith('#')) {
                    server.restartWorkers();
                }
            });
    } else {
        await Universe.getUniverse(undefined);
    }
});
