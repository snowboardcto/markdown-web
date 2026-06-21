# Story 2-4 — Media Embedding: AC Trace Report

- **Story:** 2-4-media-embedding (Epic 2)
- **Date:** 2026-06-21
- **Status at trace:** review → done
- **Verification command:** `cd web && npx playwright test`
- **Result:** **94 passed / 0 failed** (66 prior specs + 28 new `2-4-media` specs)
- **`npx astro check`:** 0 errors / 0 warnings / 1 pre-existing hint (a `waitForNavigation` deprecation in the 2.3 test file, unrelated to 2.4)

## Implementation under trace

- `web/src/lib/rehype-md-media.mjs` — media `src`/`poster` rewrite rehype plugin + astro:assets bypass
- `web/src/lib/copy-vault-media.mjs` — `astro:build:done` integration: binary-copies `content/`'s non-`.md` assets into `dist/` + the dist-HTML-scan missing-asset warn
- `web/src/lib/page-path.mjs` — shared `contentDir` / `resolveSourcePath` / `pageDirSlugFromSource` (links) / `pageDirFromSource` (media, verbatim dir)
- `web/astro.config.mjs` — wires `rehypeRaw` (first) + `rehypeMdLinks` + `rehypeMdMedia` and `integrations: [copyVaultMedia()]`
- Fixtures: `content/x.md`, `content/sub/page.md` + `content/sub/diagram.png`, `content/My Notes Dir/{page.md,pic.png}`, `content/media/powder.jpg`

## AC → Test matrix (7 ACs)

| AC | Acceptance bar | Covering tests in `web/tests/2-4-media.spec.ts` | Status |
|----|----------------|--------------------------------------------------|--------|
| **AC1** | Relative image embed renders inline as plain `<img src="/media/powder.jpg">` (build-time, JS-free), asset served 200 w/ `image/*` content-type, non-zero rendered size | `AC1` describe: emits `/media/powder.jpg` not literal; GET 200 + `image/*` + JPEG SOI magic bytes; `naturalWidth > 0`; plain static `<img>` no `astro-island`; LITERAL markdown `![]()` → served plain `<img>` not `/_astro/*` | ✅ COVERED |
| **AC2** | Relative resolution against the page's own dir (same-dir / `..` / `./` / nested sibling), filenames NOT slugged, verbatim-dir (not slugged route dir), F1 `%2F`/leading-`%2F`/malformed-`%` smuggle left UNREWRITTEN | `AC2` describe: same-dir + `./` collapse from `/x`; `..` from `/sub/page` → `/media/powder.jpg`; sibling `diagram.png` → `/sub/diagram.png` served 200; byte-exact filename served; F1 rows 12/13/14 unrewritten. **review fix #1** describe: mixed-case/spaced dir `My Notes Dir` → `/My%20Notes%20Dir/pic.png` served 200, slugged path 404s | ✅ COVERED (core); see Gaps for deferred edge rows |
| **AC3** | External `https` / protocol-relative `//` / `data:` / root-absolute `/` pass through unchanged, not copied; `<a>` links remain 2.3's concern | `AC3` describe: external https unchanged; protocol-relative `//cdn` unchanged; `data:` URI unchanged; root-absolute `/already/served.png` unchanged | ✅ COVERED |
| **AC4** | `alt` (incl. empty `alt=""`), `title`, `width`, `height` survive the rewrite | `AC4` describe: alt preserved alongside src rewrite; empty-alt → `alt=""` + src rewritten; title/width/height preserved | ✅ COVERED |
| **AC5** | Relative `<video src>`/`<video poster>`/`<audio src>`/`<source src>` rewritten + served the same as images | `AC5` describe: `<source src>` → `/media/clip.mp4`; `<video poster>` → `/media/powder.jpg` (independent attr); `<audio src>` → `/media/clip.mp3`; poster image (real fixture) served 200 `image/*` | ✅ COVERED (mp4/mp3 byte-serving deferred per Dev Notes; rewrite + copy-parity proven via real poster) |
| **AC6** | Missing referenced asset rewrites to would-be served path → 404 (broken image), never a build crash | `AC6` describe: missing embed rewrites to `/media/missing.jpg`; GET → 404; build did not crash (`/x` renders, `<h1>` present) | ✅ COVERED |
| **AC7** | No regression: all prior specs green, JS-free/crawlable/semantic shell + theme + 2.3 linking/404 intact, build exits 0 one page per `.md` | FULL suite green: **94 passed** (66 prior — `ac1`/`ac2`/`ac3`/`ac5`/`ac6`/`2-2-theme`/`2-3-linking-nav` — + 28 new); `astro check` clean; build exit 0 | ✅ COVERED |

## No-regression guard (existing specs)

All 66 prior specs across `ac1-gfm-core`, `ac2-js-disabled`, `ac3-crawlable-shell`, `ac5-slugging-edge`, `ac6-gfm-extensions`, `2-2-theme`, `2-3-linking-nav` pass unchanged alongside the 28 new `2-4-media` specs. The "Media (AC: 2.4)" section in `content/x.md` adds no second `<h1>`/`<table>`; the `page-path.mjs` extraction is behavior-preserving for 2.3 (link rewrites + smuggle guards + `report.pdf` pass-through identical).

## Gaps

All gaps are **acceptable** — coverage gaps on robustness/edge branches, not core-AC failures, and all are recorded in `deferred-work.md` under "code review of 2-4-media-embedding":

1. **AC2 rows 5/17/18 (MEDIUM)** — filename with spaces / case-only mismatch / non-ASCII have neither fixture nor test. The byte-for-byte / re-encode-per-segment logic is exercised only for lowercase same-dir + the mixed-case-dir review-fix case; the hard rows (`Powder Day.JPG`, `POWDER.JPG`, `résumé.png`) are unproven.
2. **AC2 rows 11/15/16/19 (LOW)** — `../escape.png`, `?query` strip, `#fragment`, trailing-slash directory are handled in `resolveMediaRef` but untested on the media path (tested 2.3 link-side analogues exist).
3. **Copy step (LOW)** — case-only filename collision on a case-insensitive dev FS silently clobbers; Linux CI / Azure SWA (case-sensitive) is the source of truth so the deploy artifact is unaffected.
4. **`rehype-raw` no sanitizer (DECISION, kept)** — accepted for the trusted single-author vault; `rehype-sanitize` allowlist deferred to if/when the vault becomes multi-author.

These are the deferred-work rows referenced as acceptable gaps. The 7 core ACs are all covered and green.

## Notes

- The `URIError: URI malformed` / `ERR_INVALID_FILE_URL_PATH` lines in the Playwright WebServer log are a documented `astro preview` harness artifact (Vite middleware decoding the unrewritten `%2F` smuggle srcs). The suite never GETs those URLs; Azure SWA serves statically and would 404 them. Dismissed in the code review as a harness quirk, not an app defect — all 94 specs pass.

## Conclusion

All 7 ACs are mapped to covering tests and green (94/94). The one CRITICAL and two HIGH review findings are resolved (verified in the Dev Agent Record and by the +2 mixed-case-dir specs). Remaining gaps are documented deferrals, not AC failures. **Story 2-4 meets its Definition of Done.**
