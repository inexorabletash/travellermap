import globals from "globals";
import js from "@eslint/js";

export default [
  js.configs.recommended,
  {
    rules: {
      "no-unused-vars": "off",
    },
  },
  {
    files: ["**/*.js"],
    ignores: ["sw.js"],
    languageOptions: {
      globals: {
        ...globals.browser,
        /** @type {typeof import('handlebars')} */
        Handlebars: Handlebars,
      },
    },
  },
  {
    files: ["sw.js"],
    languageOptions: {
      globals: {
        ...globals.serviceworker,
      },
    },
  },
];