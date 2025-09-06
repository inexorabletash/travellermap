import fs, {Dirent} from "node:fs";
import logger from "./logger.js";
import path from "node:path";

export class CaseInsensitiveFileResolver {
    protected knownDirectories: Record<string, Promise<Dirent[]>> = {};

    async resolve(name: string) {
        try {
            const stat = await fs.promises.stat(name);

            if(!stat.isFile()) {
                logger.warn(`File: ${name} - not a file`);
                return undefined;
            }
        } catch(e) {
            logger.info(`File: ${name} not found - attempting case insensitive resolve`);
            // ignore
        }

        const dirName = path.dirname(name);
        const dirContents = await this.getDirectoryContents(dirName);
        const fileName = path.basename(name).toLowerCase();
        const match = dirContents.findIndex(entry => entry.isFile() && entry.name.toLowerCase() === fileName);
        if(match < 0) {
            logger.warn(`File: ${name} - not found`);
            return undefined;
        }
        const result = path.join(dirName, dirContents[match].name);
        logger.info(`File: ${name} resolved as ${result}`);
        return result;
    }

    protected async getDirectoryContents(dir: string): Promise<Dirent[]> {
        if(!this.knownDirectories[dir]) {
            this.knownDirectories[dir] = fs.promises.readdir(dir, {encoding: 'utf8', withFileTypes: true})
        }
        return this.knownDirectories[dir];
    }
}