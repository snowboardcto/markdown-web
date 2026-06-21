// Story 2.7 — AC8b: LOCAL-FIDELITY endpoint integration test (GATED / skipped
// in CI). This tier hits the REAL running function over HTTP via node `fetch`,
// so it requires an Azure emulator (Azure Functions Core Tools `func start`
// and/or the SWA CLI `swa start` composing the static host + linked API +
// staticwebapp.config.json). It is the local fidelity check, NOT the CI
// backstop — the pure handler test (`negotiate.test.mjs`) is what CI runs.
//
// It is SKIPPED unless BOTH:
//   - the env flag RUN_SWA_E2E is set (opt-in), and
//   - BASE_URL points at the running emulator.
// so a plain `node --test` (RED phase / CI) never needs an emulator.
//
// Local run (documented, reproducible):
//   1. cd api && npm install   # (once api deps land in Task 3)
//   2. Run the Task-1 content-copy/manifest step so the Function can read .md.
//   3. Start ONE emulator:
//        swa start ../web/dist --api-location .        # full SWA fidelity
//        # or, function alone:
//        func start
//   4. In another shell:
//        cd api && RUN_SWA_E2E=1 BASE_URL=http://localhost:4280 node --test tests/negotiate.e2e.test.mjs
//
// If the emulator is impractical in CI, this file STAYS skipped there with this
// documented reason; the pure handler test is the always-green backstop.

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CONTENT_ROOT = path.resolve(__dirname, '../../content');

const ENABLED = process.env.RUN_SWA_E2E === '1' || process.env.RUN_SWA_E2E === 'true';
const BASE_URL = process.env.BASE_URL || 'http://localhost:4280';

// `node:test` honors `{ skip }` — these never run (and never need an emulator)
// unless RUN_SWA_E2E is explicitly set.
const gated = { skip: ENABLED ? false : 'gated: set RUN_SWA_E2E=1 + BASE_URL to run against func/swa emulator' };

test('AC8b: GET /x with Accept: text/markdown -> 200 raw .md + Vary: Accept', gated, async () => {
  const res = await fetch(`${BASE_URL}/x`, { headers: { Accept: 'text/markdown' } });
  assert.equal(res.status, 200);
  assert.equal(res.headers.get('content-type'), 'text/markdown; charset=utf-8');
  assert.equal(res.headers.get('vary'), 'Accept');
  const body = Buffer.from(await res.arrayBuffer());
  assert.ok(body.equals(readFileSync(path.join(CONTENT_ROOT, 'x.md'))));
});

test('AC8b: GET /x with Accept: text/html -> HTML page (200, text/html, Vary: Accept)', gated, async () => {
  const res = await fetch(`${BASE_URL}/x`, { headers: { Accept: 'text/html' } });
  assert.equal(res.status, 200);
  assert.match(res.headers.get('content-type') || '', /text\/html/);
  assert.equal(res.headers.get('vary'), 'Accept');
});

test('AC8b: GET /does-not-exist with Accept: text/markdown -> 404', gated, async () => {
  const res = await fetch(`${BASE_URL}/does-not-exist`, { headers: { Accept: 'text/markdown' } });
  assert.equal(res.status, 404);
});
