import pino from 'pino';
import {threadId} from "node:worker_threads";
import fs from "node:fs";
import path from "node:path";


const FILENAME = import.meta.filename;
const pkg = JSON.parse(fs.readFileSync(path.join(FILENAME,'..','package.json'), 'utf8'));

let transport;
if(!process.env.JSON_LOGGING) {
    transport = {
        target: 'pino-pretty',
        options: {
            translateTime: 'UTC:yyyy-mm-dd HH:MM:ss.l',
            singleLine: true,
        }
    };
} else {
    transport = {
        target: 'pino/file',
    }
}
export default pino({
    level: process.env.PINO_LOG_LEVEL ?? 'info',
    formatters: {
        level: (label) => {
            return {level: label.toUpperCase()};
        },
        bindings: (bindings) => ({
            pid: bindings.pid,
            host: bindings.hostname,
            threadId: threadId,
            node_version: process.version,
            version: pkg.version,
        }),
    },
    transport,
});

