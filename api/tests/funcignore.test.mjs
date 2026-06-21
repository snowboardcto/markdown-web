// Story 2.7 — ANTI-REGRESSION backstop for Critical #2 (the `.funcignore` `*.md`
// strip) and the silent-empty-map footgun. CI-runnable, pure, no emulator/network.
//
// Two independent guards so the markdown representation can NEVER silently
// regress in CI again:
//
//  1. SIMULATE the `.funcignore` filter (the Azure Functions packager applies
//     `.gitignore`-style globs to decide what ships) and ASSERT the bundled
//     vault `negotiate/content/**/*.md` SURVIVES the filter — i.e. the deploy
//     package will actually contain the `.md` the Function reads at runtime.
//
//  2. ASSERT the runtime slug->content map is NON-EMPTY and a known fixture
//     (`x`) resolves to REAL bytes (byte-equal to content/x.md on disk) — so a
//     stripped/empty bundle is caught even if the funcignore semantics drift.
//
// Requires the build step to have run (`cd api && npm run build`). If the bundle
// is absent the map/byte guards SKIP with a clear reason; the funcignore-pattern
// guard always runs (it reads `.funcignore` text, not the build output).

import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { readMd, loadVaultMap } from '../negotiate/vault.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const API_ROOT = path.resolve(__dirname, '..');
const CONTENT_ROOT = path.resolve(__dirname, '../../content');
const FUNCIGNORE = path.join(API_ROOT, '.funcignore');
const BUNDLED_CONTENT = path.join(API_ROOT, 'negotiate', 'content');
const MANIFEST = path.join(API_ROOT, 'negotiate', 'manifest.json');

const BUILT = fs.existsSync(MANIFEST) && fs.existsSync(BUNDLED_CONTENT);
const gate = { skip: BUILT ? false : 'run `cd api && npm run build` to bundle content/ first' };

// --- Minimal `.gitignore`/`.funcignore` glob matcher (the subset the packager
// uses): blank lines and `#` comments are ignored; a leading `!` negates; a
// leading `/` anchors to the package root; an UNANCHORED pattern matches at any
// depth. Returns true iff `relPath` (POSIX, package-root-relative) is EXCLUDED. ---
function isExcludedByFuncignore(relPath, rules) {
  let excluded = false;
  for (const rule of rules) {
    let pattern = rule.trim();
    if (pattern === '' || pattern.startsWith('#')) continue;
    let negate = false;
    if (pattern.startsWith('!')) {
      negate = true;
      pattern = pattern.slice(1);
    }
    const anchored = pattern.startsWith('/');
    if (anchored) pattern = pattern.slice(1);
    const dirRule = pattern.endsWith('/');
    if (dirRule) pattern = pattern.slice(0, -1);

    // Translate the glob (`*` -> any-non-separator run) to a regex.
    const body = pattern
      .split('/')
      .map((seg) => seg.replace(/[.+^${}()|[\]\\]/g, '\\$&').replace(/\*/g, '[^/]*'))
      .join('/');

    let matched = false;
    if (anchored) {
      matched = dirRule
        ? new RegExp(`^${body}(/|$)`).test(relPath)
        : new RegExp(`^${body}$`).test(relPath);
    } else if (dirRule) {
      // Unanchored dir rule: the dir, at any depth, and everything under it.
      matched = new RegExp(`(^|/)${body}(/|$)`).test(relPath);
    } else {
      // Unanchored file rule: match the basename or any path segment.
      matched =
        new RegExp(`(^|/)${body}$`).test(relPath) || new RegExp(`(^|/)${body}(/|$)`).test(relPath);
    }
    if (matched) excluded = !negate;
  }
  return excluded;
}

test('CRITICAL #2: `.funcignore` does NOT strip the bundled vault .md from the package', gate, () => {
  const rules = fs.readFileSync(FUNCIGNORE, 'utf8').split(/\r?\n/);

  // Walk the bundled vault and assert EVERY .md survives the funcignore filter.
  const survivors = [];
  function walk(dir) {
    for (const dirent of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, dirent.name);
      const rel = path.relative(API_ROOT, full).split(path.sep).join('/');
      if (dirent.isDirectory()) walk(full);
      else if (dirent.isFile() && dirent.name.toLowerCase().endsWith('.md')) {
        assert.equal(
          isExcludedByFuncignore(rel, rules),
          false,
          `bundled vault file ${rel} MUST ship (not be excluded by .funcignore) — ` +
            'an unanchored `*.md` would strip it and 404 every markdown request in prod',
        );
        survivors.push(rel);
      }
    }
  }
  walk(BUNDLED_CONTENT);

  assert.ok(survivors.length > 0, 'expected at least one bundled vault .md to survive the funcignore filter');

  // Sanity: a root-level README.md (if present) is still excluded by `/*.md`.
  assert.equal(
    isExcludedByFuncignore('README.md', rules),
    true,
    'a package-root README.md should still be excluded by the anchored `/*.md`',
  );
});

test('CRITICAL #2: runtime slug->content map is NON-EMPTY (bundle present, not stripped)', gate, () => {
  const map = loadVaultMap();
  assert.ok(map.size > 0, 'the slug->content map MUST be non-empty — an empty map = every markdown request 404s');
});

test('CRITICAL #2: known fixture `x` resolves to REAL bytes (byte-equal to content/x.md)', gate, () => {
  const bytes = readMd('x');
  assert.ok(Buffer.isBuffer(bytes), 'readMd("x") must return a Buffer of real bundled bytes, not null');
  assert.ok(
    bytes.equals(fs.readFileSync(path.join(CONTENT_ROOT, 'x.md'))),
    'bundled bytes for `x` must equal content/x.md on disk',
  );
});
