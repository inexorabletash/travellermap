import pino from 'pino';
import {threadId} from "node:worker_threads";

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
        }),
    },
    transport,
});

