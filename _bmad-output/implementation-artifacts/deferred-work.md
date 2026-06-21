# Deferred Work

Items deferred during reviews — real but not actionable in the originating step.

## Deferred from: code review of 2-2-apply-the-github-style-default-theme (2026-06-21)

- Long unbreakable autolink URL in a paragraph is untested. The fixture's only autolink is 26 chars, so `body { overflow-wrap: break-word }` (the defense against a long URL forcing horizontal document scroll) is never exercised. Add a 140+ char bare URL to `content/x.md` and assert no horizontal document scroll.
- Nested-blockquote test asserts `toHaveCount(1)` for `blockquote blockquote`, coupling it to the current 2-level fixture; a 3-level fixture would make the count 2+ and break the assertion (the CSS descendant combinator already handles N levels correctly). Use `>= 1` or `.first()`.
- The AC2 "no dark palette" test blocklists only 5 specific `github-dark` hexes; a different low-contrast color would slip past it. Acceptable while the AC3 per-token contrast loop (palette-agnostic) remains the real backstop.
- `:not(pre) > code` chip breadth is only exercised for inline code inside `<p>`. The selector is structurally correct for code inside headings/links/table-cells/list-items; add fixture rows to lock that breadth.
- Stacked h1/h2 hairlines and inline code in non-`<p>` contexts are cosmetically fine but unasserted. No behavior risk.
