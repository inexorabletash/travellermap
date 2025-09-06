import fs from "node:fs";
import path from "node:path";
import {StatWatcher} from "fs";
import logger from './logger.js';

/**
 * Implement recursive fs.watch using polling
 */
export class FilePoller {
    private watches: Map<string,StatWatcher> = new Map();

    constructor(private root: string, private notify: (change: string, file: string) => void) {
        this.addWatch(root, true);
    }


    addWatch(watchPath: string, starting: boolean) {
        if(this.watches.has(watchPath)) {
            return;
        }
        logger.info(`ADDWATCH: ${watchPath}`);
        if(!starting) {
            this.notify('create', watchPath);
        }
        this.watches.set(watchPath, fs.watchFile(watchPath, { bigint: true} , (curr, old) => {
            if(curr.mtimeMs === BigInt(0) && curr.nlink === BigInt(0)) {
                this.notify('remove', watchPath);
                this.removeWatch(watchPath);
            } else if(curr.isFile()) {
                this.notify('change', watchPath);
            } else if(curr.isDirectory()) {
                this.notify('change', watchPath);
                this.changeDir(watchPath, false);
            }
        }));
        this.changeDir(watchPath, starting);
    }

    changeDir(watchPath: string, starting: boolean) {
        fs.readdir(watchPath, (err: NodeJS.ErrnoException | null, files: string[]) => {
            for(const file of (files ?? [])) {
                const newPath = path.join(watchPath, file);
                this.addWatch(newPath, starting);
            }
        });
    }

    removeWatch(watchPath: string) {
        if(!this.watches.has(watchPath)) {
            return;
        }
        logger.info(`DELWATCH: ${watchPath}`);
        fs.unwatchFile(watchPath);
        this.watches.delete(watchPath);
    }
}