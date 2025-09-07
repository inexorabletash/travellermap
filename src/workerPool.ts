import { Worker } from 'worker_threads';
import logger from "./logger.js";

const FILENAME = import.meta.filename;

type WorkerNotify = {
    resolve?: (v: any) => void;
    reject?: (v: Error) => void;
}

export class WorkerPool {
    protected readyWorkers: Worker[] = [];
    protected allWorkers: Map<Worker,WorkerNotify> = new Map();
    protected workerWait!: Promise<void>;
    protected workerReady_!: () => void;

    constructor(count = 8) {
        this.createWorkerWait();
        for(let i = 0 ; i < count; ++i) {
            this.startOne();
        }
    }

    protected startOne() {
        //const runPath = path.join(path.dirname(FILENAME),'index.js');
        const runPath = process.argv[1];

        const worker = new Worker(runPath, {
            eval: false,
            workerData: {
            },
        });
        const workerThreadId = worker.threadId;

        worker
            .on('exit', (code) => {
                logger.warn(`Worker ${workerThreadId} shutdown - ${code}`);
                const notify = this.allWorkers.get(worker);
                this.removeWorker(worker);
                this.startOne();
                notify?.reject?.(new Error(`Worker shutdown with code ${code}`));
            })
            .on('message', result => {
                if(result instanceof Error) {
                    logger.warn(result,`Error processing request`);
                    this.allWorkers.get(worker)?.reject?.(result);
                } else {
                    this.allWorkers.get(worker)?.resolve?.(result);
                }
            }).on('error', result => {
                logger.error(result,`Error on worker`);
                this.allWorkers.get(worker)?.reject?.(result as Error);
            })
        ;

        this.readyWorkers.push(worker);
        this.allWorkers.set(worker, {});
    }

    async invoke(query: any): Promise<any> {
        let worker: Worker|undefined = undefined;
        try {
            worker = await this.worker();
            logger.debug(`Obtained worker ${worker.threadId} for ${query.path} (${JSON.stringify(query.query)}) `);
            if(!worker) {
                throw new Error(`Did not get a worker for ${query.path}`);
            }
            const result = new Promise((resolve, reject) => {
                this.allWorkers.set(<any>worker, { reject, resolve });
            });
            worker.postMessage(query);
            return await result;
        } finally {
            logger.debug(`Releasing worker ${worker?.threadId}`);
            if(worker) {
                if (this.allWorkers.get(worker)) {
                    this.allWorkers.set(worker, {});
                    if (this.readyWorkers.length === 0) {
                        this.workerReady();
                    }
                    this.readyWorkers.push(worker);
                }
            }
        }
    }

    public async restart() {
        const shutdownWorkers = this.allWorkers;
        const promises = [...shutdownWorkers.keys()].map(worker => worker.terminate());
        await Promise.all(promises);
    }

    protected removeWorker(worker: Worker) {
        this.allWorkers.delete(worker);
        this.readyWorkers = this.readyWorkers.filter(w => w !== worker);
    }

    protected async worker(): Promise<Worker> {
        while(this.readyWorkers.length === 0) {
            logger.debug(`Waiting for worker ...`);
            await this.workerWait;
        }
        const worker = <any>this.readyWorkers.shift();
        return worker;
    }

    protected workerReady() {
        this.workerReady_();
        this.createWorkerWait();

    }

    protected createWorkerWait() {
        this.workerWait = new Promise<void>(resolve => this.workerReady_ = resolve);
    }
}