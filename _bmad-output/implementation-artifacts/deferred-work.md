# Deferred Work

Items deferred during reviews — real but not actionable in the originating step.

## Deferred from: code review of 2-2-apply-the-github-style-default-theme (2026-06-21)

- Long unbreakable autolink URL in a paragraph is untested. The fixture's only autolink is 26 chars, so `body { overflow-wrap: break-word }` (the defense against a long URL forcing horizontal document scroll) is never exercised. Add a 140+ char bare URL to `content/x.md` and assert no horizontal document scroll.
- Nested-blockquote test asserts `toHaveCount(1)` for `blockquote blockquote`, coupling it to the current 2-level fixture; a 3-level fixture would make the count 2+ and break the assertion (the CSS descendant combinator already handles N levels correctly). Use `>= 1` or `.first()`.
- The AC2 "no dark palette" test blocklists only 5 specific `github-dark` hexes; a different low-contrast color would slip past it. Acceptable while the AC3 per-token contrast loop (palette-agnostic) remains the real backstop.
- `:not(pre) > code` chip breadth is only exercised for inline code inside `<p>`. The selector is structurally correct for code inside headings/links/table-cells/list-items; add fixture rows to lock that breadth.
- Stacked h1/h2 hairlines and inline code in non-`<p>` contexts are cosmetically fine but unasserted. No behavior risk.

## Deferred from: code review of 2-3-inter-file-linking-and-navigation (2026-06-21)

- F5 (LOW): The rehype link-rewrite scheme regex `^[a-z][a-z0-9+.-]*:` mis-classifies a relative `.md` filename whose first segment contains a colon (e.g. `weird:name.md`) as a URL scheme and leaves it unrewritten. Pathological for a POSIX vault; accept the heuristic and revisit only if a real filename hits it.
- F6 (LOW): AC4's "real 404 status, not soft-200" is verified only under `astro preview`. Production Azure SWA 404-status behavior is relied-upon-by-default but unverified by any test (no `staticwebapp.config.json` ships, per spec intent). Add a post-deploy smoke check asserting an unmatched route returns HTTP 404 with the custom page in production.

## Deferred from: code review of 2-4-media-embedding (2026-06-21)

- (MEDIUM) AC2 edge-table rows 5/17/18 (filename with spaces / case-only mismatch / non-ASCII) have neither fixture nor test. The "byte-for-byte / case + spaces preserved" claim is verified only for the lowercase same-dir case; the re-encode-per-segment logic is plausibly correct but unproven on the hard rows. Add `media/Powder Day.JPG`, a case-only `media/POWDER.JPG`, and a non-ASCII `media/résumé.png` fixture + round-trip assertions.
- (LOW) AC2 edge-table rows 11/15/16/19 (`../escape.png`, `?query` strip, `#fragment`, trailing-slash directory) are handled in `resolveMediaRef` but untested on the media path (tested 2.3 link-side analogues exist). Add media fixtures to lock the branches.
- (LOW) Case-only filename collision on a case-insensitive dev FS (`Logo.png` + `logo.png`) silently clobbers in the copy step (`copy-vault-media.mjs`). Linux CI/Azure (case-sensitive) is the source of truth so the deploy artifact is fine; no detection/warn for the dev-FS case. Add a `console.warn` on a case-insensitive-collision detection.
- (DECISION, kept #7) `rehype-raw` is enabled with NO sanitizer (`web/astro.config.mjs`). For the current TRUSTED single-author vault this is accepted as-is (Astro already serialized raw HTML before, so the effective surface is ~unchanged) and documented in the 2-4 Dev Agent Record. DEFERRED: if/when the vault ever becomes multi-author or accepts user-contributed content, add a `rehype-sanitize` allowlist AFTER `rehypeRaw` permitting at least `img`/`video`/`audio`/`source`/`track` (+ `src`/`poster`/`controls`/`width`/`height`/`type`/`alt`/`title`) and the GFM element/attribute set, so raw `<script>`/`<img onerror>`/`<iframe>`/`javascript:` cannot be stored-XSS'd into every rendered page. NOT done now to avoid risking the 2-4 media tests (a too-tight allowlist would strip the media elements the suite asserts).
