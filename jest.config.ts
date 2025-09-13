import type { JestConfigWithTsJest } from "ts-jest";
import {createDefaultEsmPreset} from 'ts-jest';

/*
const config: JestConfigWithTsJest = {
  verbose: true,
  transform: {
    "^.+\\.ts?$": [
      "ts-jest",
      {
        useESM: true,
        tsconfig: 'tsconfig.spec.json'
      },
    ],
  },
  extensionsToTreatAsEsm: [".ts"],
  moduleNameMapper: {
    "^(\\.{1,2}/.*)\\.js$": "$1",
  },
  testRegex: '/tests/.*\.(test|spec)?\.(ts|tsx)$',
};
*/
const config = {
  verbose: true,
  displayName: 'ts-esm',
  ...createDefaultEsmPreset({tsconfig: 'tsconfig.spec.json'}),
};

export default config;
/*
module.exports = {
  transform: {'^.+\.ts?$': 'ts-jest'},
  testEnvironment: 'node',
  moduleFileExtensions: ['ts', 'tsx', 'js', 'jsx', 'json', 'node']
};
*/

