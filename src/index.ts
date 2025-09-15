#!/usr/bin/env -S node --enable-source-maps
import {WebServer, WireRequest} from "./webServer.js";
import path from "node:path";
import {TileRender} from "./render/tileRender.js";
import {Universe} from "./universe.js";
import fs from "node:fs";
import {Sector} from "./universe/sector.js";
import {World} from "./universe/world.js";
import {hexRadius} from "./render/border.js";
import {worldBfs} from "./universe/bfs.js";
import {inspect} from "node:util";
import {FilePoller} from "./filePoller.js";
import logger from './logger.js';
import express from "express";
import {WorkerPool} from "./workerPool.js";
import {isMainThread, parentPort, threadId} from "node:worker_threads";
import {addListeners} from "./controller.js";
import {PosterRenderer} from "./render/posterRenderer.js";

process.on('SIGINT', () => {
    console.warn('Received SIGINT - shutting down');
    process.exit(0);
});

process.on('SIGTERM', () => {
    console.warn('Received SIGINT - shutting down');
    process.exit(0);
});


const workers = Number.parseInt(process.env['WORKERS'] ?? '8');
const port = Number.parseInt(process.env['PORT'] ?? '8000');
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
