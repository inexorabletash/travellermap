import express, { Request, Response } from "express";
import bodyParser from "body-parser";
import multer from 'multer';
import {WorkerPool} from "./workerPool.js";
import {requestLogger} from "./requestLogger.js";
import logger from "./logger.js";
import {MessagePort} from "worker_threads";
import {inspect} from "node:util";

export type WebServerFactories = {
    expressFactory: () => express.Express;
    workerPoolFactory: (threads: number) => WorkerPool;
    isMainThread: () => boolean;
    threadId: () => number;
    parentPort: () => null|MessagePort;
};

export interface WireRequest {
    matchPath: string;
    path: string;
    query: Record<string,any>;
    params: Record<string,any>;
    body: any;
    headers: Record<string, number | string | readonly string[]>;
    contentType?: string;
}

export interface WireResponse {
    statusCode: number;
    contentType: string | undefined;
    body: any;
    headers: Record<string, number | string | readonly string[]> | undefined;
}

export type ProcessFunctionNormal = (req: WireRequest) => Promise<WireResponse>;
export type ProcessFunctionJson = (req: WireRequest) => Promise<any>;
export type ProcessFunction = ProcessFunctionNormal|ProcessFunctionJson;

export class WebServer {
    protected app: express.Express|undefined;
    protected startup: (() => (Promise<void>|void))|undefined;
    protected workerThreads: number;
    protected workers: WorkerPool | undefined;
    protected registeredFunctions: Record<string, ProcessFunction> = {};

    constructor(startupFunction: (() => (Promise<void>|void))|undefined, workerThreads: number, protected factories: WebServerFactories) {
        this.startup = startupFunction;
        this.workerThreads = workerThreads;

        if(factories.isMainThread()) {
            this.app = factories.expressFactory();
            this.app.use(requestLogger as any);
            this.app.use(bodyParser.json());
            this.app.use(bodyParser.urlencoded());
            this.app.use(multer().none());
        }

        if(this.workerThreads && factories.isMainThread()) {
            this.workers = factories.workerPoolFactory(workerThreads);
        }
    }

    isWorker() : boolean {
        return !this.factories.isMainThread();
    }

    isMaster() : boolean {
        return this.factories.isMainThread() && !!this.workerThreads;
    }

    isStandalone(): boolean {
        return this.factories.isMainThread() && !this.workerThreads;
    }

    registerJson(path: string, verb: 'get'|'post'|'put'|'delete'|'patch', process: ProcessFunction) {
        if(this.isWorker()) {
            this.registeredFunctions[path] = process;
        } else {
            this.app?.[verb](path, async (req: Request, res: Response) => {
                const msgReq = this.toWireRequest(path, req)
                let result;
                if (this.isMaster()) {
                    result = await this.workers?.invoke(msgReq);
                } else {
                    result = await process(msgReq);
                }
                this.wireResponseJson(result, res);
            });
        }
    }

    registerRaw(path: string, verb: 'get'|'post'|'put'|'delete'|'patch', process: ProcessFunction, acceptTypes: string[]|undefined = undefined) {
        if(this.isWorker()) {
            this.registeredFunctions[path] = process;
        } else {
            this.app?.[verb](path, async (req: Request, res: Response) => {
                let contentType;
                if ((acceptTypes?.length ?? 0) > 0) {
                    contentType = req.accepts(acceptTypes as string[]);
                    if (!contentType) {
                        contentType = acceptTypes?.[0];
                    }
                }
                const msgReq = this.toWireRequest(path, req, contentType)
                let result;
                if (this.isMaster()) {
                    result = await this.workers?.invoke(msgReq);
                } else {
                    result = await process(msgReq);
                }
                this.fromWireResponse(result, res);
            });
        }
    }

    staticRoute(route: string, directory: string) {
        if(this.app) {
            this.app.use(route, express.static(directory));
        }
    }

    restartRoute(route: string) {
        if(this.app) {
            this.app.use(route, async (req, res) => {
                await this.workers?.restart();
                this.wireResponseJson({}, res);
            });
        }
    }

    async restartWorkers() {
        await this.workers?.restart();
    }

    async start(port: number, warmFunction: () => Promise<void>): Promise<void> {
        await warmFunction();

        if(this.isWorker()) {
            this.factories.parentPort()?.on('message',async (message: WireRequest) => {
                const fn = this.registeredFunctions[message.matchPath];
                if(!fn) {
                    logger.error(`No processor for ${message.matchPath}`);
                    this.factories.parentPort()?.postMessage(new Error(`Unknown endpoint`));
                } else {
                    try {
                        const result = await fn(message)
                        this.factories.parentPort()?.postMessage(result);
                    } catch(e:any) {
                        logger.warn(e.stack);
                        this.factories.parentPort()?.postMessage(new Error(`Worker Error:\n\t${e.stack}`));
                    }
                }
            }).on('close', () => {
                logger.debug(`Closed: ${this.factories.threadId()}`);
            });
        } else {
            if(this.app) {
                this.app.listen(port, this.startup);
            }
        }
    }

    protected toWireRequest(matchPath: string, req: Request, contentType: string|undefined = undefined): WireRequest {
        return {
            contentType,
            matchPath: matchPath,
            path: req.path,
            query: req.query,
            params: req.params,
            body: req.body,
            headers: Object.fromEntries(Object.entries(req.headers ?? {})) as Record<string, number | string | readonly string[]>,
        }
    }

    protected fromWireResponse(result: WireResponse, res: Response) {
        res.statusCode = result.statusCode;
        Object.entries(result.headers ?? {}).forEach(
            e => {
                res.setHeader(e[0], e[1]);
            });
        if(result.contentType) {
            res.contentType(result.contentType);
        }
        res.send(result.body);
    }

    protected wireResponseJson(result: any, res: Response) {
        res.json(result);
        res.statusCode = 200;
    }

}
