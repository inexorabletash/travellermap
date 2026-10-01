// Runs the browser test pages (APITest, ContentTest, ImageTest) against a running site
// in headless Chrome and reports the results. No dependencies beyond Node 22+ (built-in
// WebSocket) and an installed Chrome/Edge.
//
//   npm run test:browser                      # http://localhost:50103
//   npm run test:browser -- --base http://localhost:8080 --no-search
//
// --no-search: allow /api/search failures (no SQL Server search index configured).
// --informational <Page>: report the page's failures but don't fail the run (repeatable).
// --save-images <dir>: for failed ImageTest checks, save the reference and the actual
//   rendering as <dir>/<name>.ref.png and <dir>/<name>.actual.png.
// Set CHROME to the browser executable if it isn't found automatically.

import {spawn} from 'node:child_process';
import {copyFileSync, existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import path from 'node:path';

const args = process.argv.slice(2);
const opt = name => {
  const i = args.indexOf(name);
  return i === -1 ? undefined : args[i + 1];
};
const BASE = (opt('--base') ?? 'http://localhost:50103').replace(/\/$/, '');
const PAGES = ['test/APITest.html', 'test/ContentTest.html', 'test/ImageTest.html'];

// Failures that are expected and don't fail the run.
const ALLOWED = [/ref_bad_example/];  // ImageTest's self-check that diffs are detected
if (args.includes('--no-search'))
  ALLOWED.push(/api\/search/);

const INFORMATIONAL = args.flatMap((a, i) => a === '--informational' ? [args[i + 1]] : []);
const SAVE_IMAGES = opt('--save-images');

const CHROME = process.env.CHROME ?? [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser',
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
].find(p => existsSync(p));
if (!CHROME) {
  console.error('Chrome not found; set the CHROME environment variable.');
  process.exit(2);
}

const sleep = ms => new Promise(r => setTimeout(r, ms));
const PORT = 9300 + Math.floor(Math.random() * 600);
const profile = mkdtempSync(path.join(tmpdir(), 'tm-browser-tests-'));
const chrome = spawn(CHROME, ['--headless=new', `--remote-debugging-port=${PORT}`,
  `--user-data-dir=${profile}`, '--no-first-run', 'about:blank'], {stdio: 'ignore'});

let exitCode = 0;
try {
  let target;
  for (let i = 0; i < 100 && !target; ++i) {
    try {
      target = await (await fetch(`http://127.0.0.1:${PORT}/json/new?about:blank`, {method: 'PUT'})).json();
    } catch {
      await sleep(200);
    }
  }
  if (!target) throw new Error('Could not connect to Chrome');

  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise(r => ws.addEventListener('open', r));
  let nextId = 0;
  const pending = new Map();
  ws.addEventListener('message', ev => {
    const m = JSON.parse(ev.data);
    if (m.id && pending.has(m.id)) {
      pending.get(m.id)(m.result);
      pending.delete(m.id);
    }
  });
  const send = (method, params = {}) => new Promise(r => {
    pending.set(++nextId, r);
    ws.send(JSON.stringify({id: nextId, method, params}));
  });
  const evaluate = async expr =>
    (await send('Runtime.evaluate', {expression: expr, returnByValue: true})).result?.value;

  // The pages use three reporting styles; normalize to {done, total, failures}.
  const STATE = `(() => {
    const text = s => document.querySelector(s)?.textContent;
    const clean = e => e.textContent.trim().replace(/\\s+/g, ' ').slice(0, 300);
    if (document.querySelector('#summary')) {  // APITest
      return {done: ['pass', 'fail'].includes(document.querySelector('#summary').className),
              total: +text('#tests_total'),
              failures: [...document.querySelectorAll('.test.fail')].map(clean)};
    }
    if (document.querySelector('#status_tests')) {  // ContentTest
      const total = +text('#status_tests');
      const done = total > 0 && (+text('#status_passed')) + (+text('#status_failed')) === total;
      return {done, total, failures: [...document.querySelectorAll('tr.fail')]
          .filter(r => r.classList.contains('source')).map(clean)};
    }
    // ImageTest: a description row then a result row, classed pass/fail when done.
    const rows = [...document.querySelectorAll('#results tr')].slice(1);
    const results = rows.filter((_, i) => i % 2 === 1);
    const failedRows = rows.filter((r, i) => i % 2 === 0 && rows[i + 1]?.className === 'fail');
    return {done: results.length > 0 && results.every(r => r.className),
            total: results.length,
            failures: failedRows.map(r => {
              const next = r.nextElementSibling?.textContent ?? '';
              return clean(r) + (next.includes('Failed to load') ? ' — ' + next.trim() : '');
            }),
            images: failedRows.map(r => ({ref: r.cells[0].textContent, url: r.cells[1].textContent}))};
  })()`;

  for (const page of PAGES) {
    const url = `${BASE}/${page}`;
    await send('Page.navigate', {url});
    let state;
    for (let t = 0; t < 600; ++t) {  // up to 5 minutes per page
      await sleep(500);
      state = await evaluate(STATE);
      if (state?.done) break;
    }
    if (!state?.done) {
      console.log(`TIMEOUT ${url}`);
      exitCode = 1;
      continue;
    }
    const unexpected = state.failures.filter(f => !ALLOWED.some(re => re.test(f)));
    const allowed = state.failures.length - unexpected.length;
    const informational = INFORMATIONAL.some(p => page.includes(p));
    const status = !unexpected.length ? 'PASS' : informational ? 'WARN' : 'FAIL';
    console.log(`${status} ${page}: ${state.total - state.failures.length}/${state.total} passed` +
        (allowed ? ` (${allowed} expected failure${allowed > 1 ? 's' : ''})` : '') +
        (informational && unexpected.length ? ' (informational; not failing the run)' : ''));
    for (const f of unexpected)
      console.log('    ' + f);
    if (unexpected.length && !informational) exitCode = 1;

    if (SAVE_IMAGES && state.images?.length) {
      mkdirSync(SAVE_IMAGES, {recursive: true});
      const toSave = state.images.filter(({ref}) => !ALLOWED.some(re => re.test(ref)));
      for (const {ref, url} of toSave) {
        const name = path.basename(ref, '.png');
        copyFileSync(path.join('test', ref), path.join(SAVE_IMAGES, `${name}.ref.png`));
        const r = await fetch(new URL(url, BASE));
        writeFileSync(path.join(SAVE_IMAGES, `${name}.actual.png`), Buffer.from(await r.arrayBuffer()));
      }
      if (toSave.length)
        console.log(`    Saved ${toSave.length} reference/actual image pair(s) to ${SAVE_IMAGES}`);
    }
  }
  ws.close();
} catch (ex) {
  console.error(ex);
  exitCode = 1;
} finally {
  chrome.kill();
  await sleep(500);
  try {
    rmSync(profile, {recursive: true, force: true});
  } catch {
    // Chrome may still hold files briefly; the temp profile is harmless.
  }
}
process.exit(exitCode);
