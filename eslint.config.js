import js from '@eslint/js';
import globals from 'globals';

export default [
  js.configs.recommended,
  {
    rules: {
      'no-unused-vars': 'off',
    },
  },
  {
    files: ['**/*.js'],
    ignores: ['sw.js'],
    languageOptions: {
      globals: {
        ...globals.browser,
        Handlebars: true,
      },
    },
  },
  {
    files: ['sw.js'],
    languageOptions: {
      globals: {
        ...globals.serviceworker,
      },
    },
  },
];
