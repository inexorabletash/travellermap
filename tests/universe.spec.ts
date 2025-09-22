import {Universe} from "../build/universe.js";
import path from "node:path";

beforeAll(() => {
    const FILENAME = import.meta.filename;
    const ROOT = path.join(path.dirname(FILENAME), '..', 'static', 'res', 'Sectors');
    const OVR_ROOT = path.join(path.dirname(FILENAME), '..', 'static', 'res', 'override');

    Universe.baseDir = ROOT;
    Universe.OVERRIDE_DIR = OVR_ROOT;
})

test('load default', async () => {
    const loaded = await Universe.getUniverse(undefined);
});
