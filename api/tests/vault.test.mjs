// Story 2.7 — CI-green integration test of the REAL runtime path (no emulator,
// no network): the bundled-vault `readMd` (Decision A, built by
// `scripts/build-content.mjs`) + the adapter's `sanitizeSlug` guard + the pure
// `negotiate(...)` handler, asserting served bytes against the ACTUAL
// `content/*.md` on disk (anti-tautology, 2.2). This proves the build step,
// the closed-map lookup (AC5), the shared slug mapping (AC4), and byte fidelity
// (AC6) end-to-end through the real modules.
//
// Requires the build step to have run (`cd api && npm run build`). If the bundle
// is absent the suite SKIPS with a clear reason rather than failing spuriously.

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import { negotiate } from '../negotiate/negotiate.mjs';
import { readMd, loadVaultMap } from '../negotiate/vault.mjs';
import { sanitizeSlug, handleNegotiate } from '../negotiate/adapter.mjs';
import { pathToSlug } from '../../web/src/lib/slug.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CONTENT_ROOT = path.resolve(__dirname, '../../content');
const MANIFEST = path.resolve(__dirname, '../negotiate/manifest.json');

const BUILT = existsSync(MANIFEST);
const gate = { skip: BUILT ? false : 'run `cd api && npm run build` to bundle content/ first' };

function sourceBytes(rel) {
  return readFileSync(path.join(CONTENT_ROOT, rel));
}

test('AC4/AC6: bundled readMd serves byte-equal source via the shared pathToSlug', gate, () => {
  for (const rel of ['x.md', 'gear-guide.md', 'sub/page.md', 'My Notes.md', 'sub/index.md']) {
    const slug = pathToSlug(rel);
    const res = negotiate({ slug, acceptHeader: 'text/markdown', readMd });
    assert.equal(res.status, 200, `slug ${slug} should resolve from the bundle`);
    assert.ok(res.body.equals(sourceBytes(rel)), `bundled bytes for ${slug} == content/${rel}`);
  }
});

test('AC5: missing slug via real readMd -> 404', gate, () => {
  const res = negotiate({ slug: 'does-not-exist', acceptHeader: 'text/markdown', readMd });
  assert.equal(res.status, 404);
});

test('AC5: sanitizeSlug rejects every hostile key (-> null, becomes uniform 404)', () => {
  const hostile = [
    '..', '../../etc/passwd', '%2F', '%2fetc%2fpasswd', '%2e%2e%2f',
    '%252F', '%252e%252e', '%00', 'x%00.md', '/etc/passwd', 'C:\\Windows',
    '\\\\unc\\share', 'sub%2Fpage',
  ];
  for (const h of hostile) {
    assert.equal(sanitizeSlug(h), null, `hostile slug ${JSON.stringify(h)} must be rejected`);
  }
});

test('AC4: sanitizeSlug passes through legitimate nested slugs', () => {
  assert.equal(sanitizeSlug('x'), 'x');
  assert.equal(sanitizeSlug('sub/page'), 'sub/page');
  assert.equal(sanitizeSlug('my-notes'), 'my-notes');
});

test('HIGH #3: sanitizeSlug lowercases/github-slugs the decoded key (no case drift)', () => {
  // Mixed-case URL must normalise to the SAME closed-map key the build emitted
  // (`pathToSlug` lower-cases), so `/X` is NOT a false 404.
  assert.equal(sanitizeSlug('X'), 'x');
  assert.equal(sanitizeSlug('Gear-Guide'), 'gear-guide');
  assert.equal(sanitizeSlug('My Notes'), 'my-notes');
  assert.equal(sanitizeSlug('Sub/Page'), 'sub/page');
});

test('HIGH #3: /X resolves to the SAME markdown bytes as /x via the real readMd', gate, () => {
  const lower = handleNegotiate({ acceptHeader: 'text/markdown', rawSlug: 'x', readMd });
  const upper = handleNegotiate({ acceptHeader: 'text/markdown', rawSlug: 'X', readMd });
  assert.equal(upper.status, 200, '/X must resolve (case-insensitive), not 404');
  assert.equal(lower.status, 200);
  assert.ok(upper.body.equals(lower.body), '/X and /x must serve identical bytes');
  assert.ok(upper.body.equals(sourceBytes('x.md')));
});

test('MEDIUM #5: HEAD omits the Buffer body but keeps status/headers/Content-Length', gate, () => {
  const get = handleNegotiate({ acceptHeader: 'text/markdown', rawSlug: 'x', readMd, method: 'GET' });
  const head = handleNegotiate({ acceptHeader: 'text/markdown', rawSlug: 'x', readMd, method: 'HEAD' });
  assert.equal(head.status, 200);
  assert.equal(head.headers['Content-Type'], 'text/markdown; charset=utf-8');
  assert.equal(head.headers['Vary'], 'Accept');
  // Content-Length is preserved (advertised) even though the body is omitted.
  assert.equal(head.headers['Content-Length'], get.headers['Content-Length']);
  assert.equal(head.body, undefined, 'HEAD must NOT carry a Buffer body');
  assert.ok(Buffer.isBuffer(get.body), 'GET still carries the body');
});

test('AC1/AC2/AC3: handleNegotiate end-to-end (markdown branch, HTML redirect, hostile 404)', gate, () => {
  // Markdown branch -> 200 + bytes.
  const md = handleNegotiate({ acceptHeader: 'text/markdown', rawSlug: 'x', readMd });
  assert.equal(md.status, 200);
  assert.equal(md.headers.Vary, 'Accept');
  assert.ok(md.body.equals(sourceBytes('x.md')));

  // HTML/default branch -> 307 redirect to the static page, Vary: Accept.
  const html = handleNegotiate({ acceptHeader: 'text/html', rawSlug: 'x', readMd });
  assert.equal(html.status, 307);
  assert.equal(html.headers.Location, '/x');
  assert.equal(html.headers.Vary, 'Accept');

  // Hostile slug -> uniform 404 (markdown branch).
  const bad = handleNegotiate({ acceptHeader: 'text/markdown', rawSlug: '..%2f..%2fetc%2fpasswd', readMd });
  assert.equal(bad.status, 404);
});

test('loadVaultMap is a closed map keyed exactly by the manifest slugs', gate, () => {
  const map = loadVaultMap();
  assert.ok(map.has('x'));
  assert.ok(map.has('sub/page'));
  assert.equal(map.has('sub%2Fpage'), false);
  assert.equal(map.has('..'), false);
});
