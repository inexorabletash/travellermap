#!/usr/bin/env -S node --enable-source-maps

import {program, Option} from "commander";
import path from "node:path";
import {Universe} from "./universe.js";
import {World} from "./universe/world.js";
import * as YAML from 'yaml';
import logger from "./logger.js";
import fs from "node:fs";


program
    .option('--sector <string>', 'sector directory', path.join(process.cwd(), 'static', 'res', 'Sectors'))
    .option('--override <string>', 'override directory', path.join(process.cwd(), 'static', 'res', 'overrides'))
    .option('--milieu <string>', 'override directory')
    .addOption(new Option('--output <filename>', 'output file').makeOptionMandatory(true))
    .option('--log <string>', 'log level', 'error')
    .arguments('<sector>')
;
program.parse();
const opts = program.opts();

logger.level = opts['log'];
Universe.baseDir = opts['sector'];
Universe.OVERRIDE_DIR = opts['override'];

const universe = await Universe.getUniverse(opts['milieu']);
const sector = universe.getSectorByName(program.args[0]);
const result = sector?.enrichWorlds();

const mapped = {
    sector: program.args[0],
    world: result,
};

fs.writeFileSync(opts['output'],YAML.stringify(mapped));
