// Story 2.7 — TDD (RED phase) unit tests for the PURE content-negotiation
// handler. CI-always-green tier (AC8a): NO Azure emulator, NO network, NO
// `func`/`swa` process, NO Azure binding glue. The handler is a pure function
// over (slug, Accept header, vault-read shim) and returns { status, headers,
// body }, so the full Accept/q-value/security/byte-fidelity matrix is exercised
// deterministically here.
//
// The module under test does NOT exist yet — this is the RED phase. Importing
// `../negotiate/negotiate.mjs` MUST fail until Task 3 implements it. Run:
//   cd api && node --test
//
// Handler contract under test (per story Task 3):
//   negotiate({ slug, acceptHeader, readMd }) -> { status, headers, body }
//   - `readMd` is the CLOSED slug -> content lookup (a slug->Buffer map's get,
//     or a function returning a Buffer for a known slug and null/undefined for
//     an unknown key). The request slug is a LOOKUP KEY, never a filesystem
//     path (AC5 closed-map discipline). Tests inject a fake map so no real FS /
//     emulator is needed.
//   - `headers` is a plain object (case may vary; tests look up
//     case-insensitively).
//   - `body` on the markdown branch is the raw source Buffer (AC6).

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

// The not-yet-existing module. In the RED phase this import throws
// (ERR_MODULE_NOT_FOUND), failing every test in this file — that IS the red bar.
import { negotiate } from '../negotiate/negotiate.mjs';

// Drive expected slugs from the SAME shared source of truth the web route uses,
// so the test never re-implements slugging (anti-drift, AC4).
import { pathToSlug } from '../../web/src/lib/slug.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CONTENT_ROOT = path.resolve(__dirname, '../../content');

// --- Real fixtures from content/ (anti-tautology, 2.2): assert served bytes
// against the ACTUAL file on disk, never a re-implementation. ---
const FIXTURE_FILES = [
  'x.md',
  'gear-guide.md',
  'sub/page.md',
  'sub/index.md',
  'My Notes.md',
];

/** Raw source bytes of a content file. */
function sourceBytes(relPath) {
  return readFileSync(path.join(CONTENT_ROOT, relPath)); // no 'utf8' -> Buffer
}

/**
 * Build the CLOSED slug -> Buffer map exactly as the build would (shared
 * `pathToSlug`), then return a `readMd(slug)` shim that returns the Buffer for a
 * known key and `null` for anything else. This is the dependency injection the
 * story calls for: the handler resolves a slug ONLY through this closed map, so
 * an unknown/hostile key is a clean miss by construction.
 */
function makeReadMd(relFiles = FIXTURE_FILES) {
  const map = new Map();
  for (const rel of relFiles) {
    map.set(pathToSlug(rel), sourceBytes(rel));
  }
  return {
    map,
    readMd: (slug) => (map.has(slug) ? map.get(slug) : null),
  };
}

/** Case-insensitive header lookup (header casing is an impl detail). */
function header(headers, name) {
  if (!headers) return undefined;
  const target = name.toLowerCase();
  for (const key of Object.keys(headers)) {
    if (key.toLowerCase() === target) return headers[key];
  }
  return undefined;
}

const MARKDOWN_CT = 'text/markdown; charset=utf-8';

// ---------------------------------------------------------------------------
// AC1 — Accept: text/markdown -> raw .md branch (200 + CT + Vary + raw bytes)
// ---------------------------------------------------------------------------
test('AC1: Accept: text/markdown -> 200 text/markdown; charset=utf-8 + Vary: Accept', () => {
  const { readMd } = makeReadMd();
  const res = negotiate({ slug: 'x', acceptHeader: 'text/markdown', readMd });

  assert.equal(res.status, 200);
  assert.equal(
    header(res.headers, 'Content-Type'),
    MARKDOWN_CT,
    'markdown branch must set Content-Type: text/markdown; charset=utf-8',
  );
  assert.equal(header(res.headers, 'Vary'), 'Accept');
  assert.ok(Buffer.isBuffer(res.body), 'markdown body must be a Buffer');
});

// ---------------------------------------------------------------------------
// AC6 — byte fidelity: served bytes === source bytes EXACTLY (Buffer.equals)
// ---------------------------------------------------------------------------
test('AC6: markdown body is byte-equal to the source file (Buffer.equals, not string compare)', () => {
  const { readMd } = makeReadMd();
  for (const rel of FIXTURE_FILES) {
    const slug = pathToSlug(rel);
    const res = negotiate({ slug, acceptHeader: 'text/markdown', readMd });
    assert.equal(res.status, 200, `expected 200 for slug ${slug}`);
    assert.ok(
      Buffer.isBuffer(res.body),
      `body for ${slug} must be a Buffer for byte-level assert`,
    );
    assert.ok(
      res.body.equals(sourceBytes(rel)),
      `served bytes for ${slug} must equal content/${rel} on disk`,
    );
  }
});

test('AC6: handler does not inject a UTF-8 BOM (EF BB BF) ahead of the source', () => {
  const { readMd } = makeReadMd();
  const res = negotiate({ slug: 'x', acceptHeader: 'text/markdown', readMd });
  const src = sourceBytes('x.md');
  const srcHasBom =
    src.length >= 3 && src[0] === 0xef && src[1] === 0xbb && src[2] === 0xbf;
  const bodyHasBom =
    res.body.length >= 3 &&
    res.body[0] === 0xef &&
    res.body[1] === 0xbb &&
    res.body[2] === 0xbf;
  assert.equal(bodyHasBom, srcHasBom, 'BOM presence must mirror the source exactly');
});

test('AC6: Content-Length, if present, equals the source byte length', () => {
  const { readMd } = makeReadMd();
  const res = negotiate({ slug: 'x', acceptHeader: 'text/markdown', readMd });
  const cl = header(res.headers, 'Content-Length');
  if (cl !== undefined) {
    assert.equal(Number(cl), sourceBytes('x.md').length);
  }
});

// ---------------------------------------------------------------------------
// AC2 — HTML is the DEFAULT; markdown is opt-in ONLY. The handler signals the
// HTML branch by NOT serving markdown: status !== 200-markdown and the body is
// not the raw .md. We assert via a markers-agnostic predicate: a markdown
// result has Content-Type text/markdown + 200 + the raw bytes; anything else is
// the HTML/default branch. The handler MUST NOT throw on any of these.
// ---------------------------------------------------------------------------

/** True iff the result is the markdown branch (200 + text/markdown CT). */
function isMarkdownBranch(res) {
  return (
    res &&
    res.status === 200 &&
    header(res.headers, 'Content-Type') === MARKDOWN_CT
  );
}

const HTML_DEFAULT_ACCEPTS = [
  ['absent (undefined)', undefined],
  ['empty string', ''],
  ['text/html', 'text/html'],
  ['*/*', '*/*'],
  ['text/*', 'text/*'],
  [
    'browser blob',
    'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8',
  ],
  ['text/markdown;q=0 explicit reject', 'text/markdown;q=0, text/html'],
  ['wildcard with markdown q=0', '*/*, text/markdown;q=0'],
  ['equal-q tie vs explicit html stays html', 'text/html;q=0.9, text/markdown;q=0.9'],
  ['html higher than markdown', 'text/html, text/markdown;q=0'],
];

for (const [label, acceptHeader] of HTML_DEFAULT_ACCEPTS) {
  test(`AC2: Accept ${label} -> HTML branch (markdown NOT served)`, () => {
    const { readMd } = makeReadMd();
    let res;
    assert.doesNotThrow(() => {
      res = negotiate({ slug: 'x', acceptHeader, readMd });
    }, 'handler must never throw on a non-markdown / absent Accept');
    assert.ok(!isMarkdownBranch(res), `${label} must NOT win the markdown branch`);
  });
}

const MALFORMED_ACCEPTS = [
  ['bare semicolon', ';'],
  ['missing subtype', 'text/'],
  ['garbage token', '@@@'],
  ['non-ascii garbage', 'ÿþ text/markdown'],
  ['empty media-range', 'text/html,,text/plain'],
];

for (const [label, acceptHeader] of MALFORMED_ACCEPTS) {
  test(`AC2: malformed Accept (${label}) -> fail safe to HTML, never throw/500`, () => {
    const { readMd } = makeReadMd();
    let res;
    assert.doesNotThrow(() => {
      res = negotiate({ slug: 'x', acceptHeader, readMd });
    }, 'malformed Accept must fail safe, never throw');
    assert.notEqual(res.status, 400);
    assert.notEqual(res.status, 500);
    assert.ok(!isMarkdownBranch(res), 'malformed Accept must not win markdown');
  });
}

const MARKDOWN_WIN_ACCEPTS = [
  ['plain', 'text/markdown'],
  ['charset param tolerated', 'text/markdown;charset=utf-8'],
  ['charset param spaced + cased', 'text/markdown; charset=UTF-8'],
  ['mixed case type', 'Text/Markdown'],
  ['higher q than html', 'text/html;q=0.8, text/markdown;q=0.9'],
  ['html present, markdown q=0.9', 'text/html, text/markdown;q=0.9'],
  ['tie vs wildcard resolves to markdown (more specific)', '*/*, text/markdown'],
];

for (const [label, acceptHeader] of MARKDOWN_WIN_ACCEPTS) {
  test(`AC1/AC2: Accept ${label} -> markdown branch wins (opt-in)`, () => {
    const { readMd } = makeReadMd();
    const res = negotiate({ slug: 'x', acceptHeader, readMd });
    assert.ok(
      isMarkdownBranch(res),
      `${label} should positively select markdown (200 + text/markdown)`,
    );
  });
}

// ---------------------------------------------------------------------------
// AC3 — Vary: Accept on BOTH branches, exact value `Accept`
// ---------------------------------------------------------------------------
test('AC3: Vary: Accept present (exact value) on the markdown branch', () => {
  const { readMd } = makeReadMd();
  const res = negotiate({ slug: 'x', acceptHeader: 'text/markdown', readMd });
  assert.equal(header(res.headers, 'Vary'), 'Accept');
});

test('AC3: Vary: Accept present (exact value) on the HTML/default branch', () => {
  const { readMd } = makeReadMd();
  const res = negotiate({ slug: 'x', acceptHeader: 'text/html', readMd });
  assert.equal(
    header(res.headers, 'Vary'),
    'Accept',
    'HTML branch must ALSO carry Vary: Accept (a cache must never cross-serve)',
  );
});

test('AC3: Vary value is exactly "Accept", never "*"', () => {
  const { readMd } = makeReadMd();
  for (const acceptHeader of ['text/markdown', 'text/html', '*/*']) {
    const res = negotiate({ slug: 'x', acceptHeader, readMd });
    assert.notEqual(header(res.headers, 'Vary'), '*');
  }
});

// ---------------------------------------------------------------------------
// AC4 — slug mapping matches the shared pathToSlug (no drift from the web route)
// ---------------------------------------------------------------------------
test('AC4: representative pages resolve via the shared pathToSlug map', () => {
  const cases = [
    ['x.md', '/x'.slice(1)],
    ['gear-guide.md', 'gear-guide'],
    ['sub/page.md', 'sub/page'],
    ['My Notes.md', 'my-notes'],
    ['sub/index.md', 'sub'],
  ];
  const { readMd } = makeReadMd();
  for (const [rel, expectedSlug] of cases) {
    // Confirm the shared logic produces the expected slug (no drift)...
    assert.equal(pathToSlug(rel), expectedSlug, `pathToSlug(${rel})`);
    // ...and the handler serves that page's bytes for that slug.
    const res = negotiate({ slug: expectedSlug, acceptHeader: 'text/markdown', readMd });
    assert.equal(res.status, 200, `slug ${expectedSlug} should resolve`);
    assert.ok(
      res.body.equals(sourceBytes(rel)),
      `slug ${expectedSlug} must serve content/${rel}`,
    );
  }
});

// ---------------------------------------------------------------------------
// AC5 — security: missing slug -> 404; hostile slugs -> 404 indistinguishable
// from missing (no path leak, no oracle). Uses the closed map only.
// ---------------------------------------------------------------------------
test('AC5: missing slug (markdown branch) -> 404', () => {
  const { readMd } = makeReadMd();
  const res = negotiate({ slug: 'does-not-exist', acceptHeader: 'text/markdown', readMd });
  assert.equal(res.status, 404);
});

test('AC5: missing slug still carries Vary: Accept and a non-crashing body', () => {
  const { readMd } = makeReadMd();
  let res;
  assert.doesNotThrow(() => {
    res = negotiate({ slug: 'does-not-exist', acceptHeader: 'text/markdown', readMd });
  });
  assert.equal(header(res.headers, 'Vary'), 'Accept');
});

const HOSTILE_SLUGS = [
  '..',
  '../../etc/passwd',
  '%2F',
  '%2fetc%2fpasswd',
  '%2e%2e%2f',
  '%252F', // double-encoded
  '%252e%252e',
  '%00',
  'x%00.md',
  '/etc/passwd',
  'C:\\Windows',
  '\\\\unc\\share',
  'sub%2Fpage', // encoded separator must not resolve to the nested page
];

test('AC5: hostile slugs all -> 404 indistinguishable from the missing-slug 404 (no oracle, no leak, no FS escape)', () => {
  const { readMd, map } = makeReadMd();
  const baseline = negotiate({
    slug: 'definitely-not-a-page',
    acceptHeader: 'text/markdown',
    readMd,
  });
  assert.equal(baseline.status, 404, 'baseline missing slug must be 404');
  const baselineBody =
    baseline.body == null
      ? ''
      : Buffer.isBuffer(baseline.body)
        ? baseline.body.toString('utf8')
        : String(baseline.body);

  const knownPageBytes = sourceBytes('x.md');

  for (const slug of HOSTILE_SLUGS) {
    let res;
    assert.doesNotThrow(() => {
      res = negotiate({ slug, acceptHeader: 'text/markdown', readMd });
    }, `hostile slug ${JSON.stringify(slug)} must not throw`);

    // Same status as the missing-slug baseline.
    assert.equal(res.status, 404, `hostile slug ${JSON.stringify(slug)} must be 404`);

    // Indistinguishable body (no path leak / no probe oracle).
    const body =
      res.body == null
        ? ''
        : Buffer.isBuffer(res.body)
          ? res.body.toString('utf8')
          : String(res.body);
    assert.equal(
      body,
      baselineBody,
      `hostile 404 body for ${JSON.stringify(slug)} must equal the missing-slug 404 body`,
    );

    // Must never leak a filesystem path / vault layout in the body.
    assert.ok(!/etc\/passwd/i.test(body), 'must not leak /etc/passwd');
    assert.ok(!/content[\\/]/i.test(body), 'must not leak the content/ path');

    // Must never serve another page's markdown bytes.
    if (Buffer.isBuffer(res.body)) {
      assert.ok(
        !res.body.equals(knownPageBytes),
        `hostile slug ${JSON.stringify(slug)} must not return x.md bytes`,
      );
    }
    // And the hostile key must not be a real map key by construction.
    assert.equal(map.has(slug), false, `hostile slug ${JSON.stringify(slug)} must not be a map key`);
  }
});
