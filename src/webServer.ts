import express, { Request, Response } from "express";
import {WorkerPool} from "./workerPool.js";
import {isMainThread, parentPort, threadId} from "node:worker_threads";
import fs from "node:fs";
import {requestLogger} from "./requestLogger.js";
import logger from "./logger.js";

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
    protected workerThreds: number;
    protected workers: WorkerPool | undefined;
    protected registeredFunctions: Record<string, ProcessFunction> = {};

    constructor(startupFunction: (() => (Promise<void>|void))|undefined, workerThreads: number) {
        this.startup = startupFunction;
        this.workerThreds = workerThreads;

        if(isMainThread) {
            this.app = express();
            this.app.use(requestLogger as any);
        }


        if(this.workerThreds && isMainThread) {
            this.workers = new WorkerPool(workerThreads);
        }
    }

    isWorker() : boolean {
        return !isMainThread;
    }

    isMaster() : boolean {
        return isMainThread && !!this.workerThreds;
    }

    isStandalone(): boolean {
        return isMainThread && !this.workerThreds;
    }

    registerJson(path: string, verb: 'get'|'post'|'put'|'delete'|'patch', process: ProcessFunction) {
        if(this.isWorker()) {
            this.registeredFunctions[path] = process;
        } else if(this.isMaster()) {
            this.app?.[verb](path, async (req: Request, res: Response) => {
                const msgReq = this.toWireRequest(path, req)
                const result = await this.workers?.invoke(msgReq);
                this.wireResponseJson(result, res);
            });
        } else {
            this.app?.[verb](path, async (req: Request, res: Response) => {
                const msgReq = this.toWireRequest(path, req)
                const result = await process(msgReq);
                this.wireResponseJson(result, res);
            })

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
            parentPort?.on('message',async (message: WireRequest) => {
                const fn = this.registeredFunctions[message.matchPath];
                if(!fn) {
                    logger.error(`No processor for ${message.matchPath}`);
                } else {
                    try {
                        const result = await fn(message)
                        parentPort?.postMessage(result);
                    } catch(e:any) {
                        logger.warn(e.stack);
                        parentPort?.postMessage(new Error(`Worker Error:\n\t${e.stack}`));
                    }
                }
            }).on('close', () => {
                logger.debug(`Closed: ${threadId}`);
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
            headers: Object.fromEntries(Object.entries(req.headers)) as Record<string, number | string | readonly string[]>,
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
