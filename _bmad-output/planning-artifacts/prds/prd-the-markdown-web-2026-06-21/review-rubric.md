# PRD Quality Review — The Markdown Web

## Overall verdict

This is a strong early-stage, solo-founder PRD with a genuine thesis ("markdown as the lingua franca for humans, rendered by each reader's agent") that the features actually serve rather than decorate. Scope honesty is excellent: a hard constraint (no-Chromium), a deliberately large MVP, deferred-not-cut sharing, counter-metrics, and an honest `[NOTE FOR PM]` at the real tension all earn their place. What's at risk is **done-ness clarity** — roughly a third of the FRs carry no testable consequences, and the load-bearing per-reader-rendering FRs lean on qualitative outcomes that downstream story creation will struggle to bound. Cross-reference hygiene is good but not airtight (one Assumptions-Index entry without an inline tag; a couple of FR↔UJ back-references that resolve only via the parent feature). None of this blocks a build for a solo founder; the testability gaps are the thing worth fixing before epics.

## Decision-readiness — strong

Decisions are stated as decisions, not buried. The no-Chromium rule is a *hard constraint* attributed to naethyn (§4.3 NFRs, §8.1), not a soft preference. The "both clients, sequenced HTML-first" call is made explicitly (§6) with the founder's own words quoted. The `[NOTE FOR PM]` at §6 names the real tension — a non-Chromium native client running a local agent is "a substantial build" — instead of smoothing it. Open Questions (§8) are genuinely open (form factor, agent integration, author incentive, business model) and routed to the right owner (architecture vs PM vs later). Trade-offs are surfaced: the addendum explicitly says *not* to over-invest in content-negotiation plumbing because the differentiator is human-facing rendering. No findings.

## Substance over theater — strong

No persona theater: there are three UJs, each with a named protagonist (naethyn, Dana, Theo) driving a distinct decision — born-compatibility, dogfood, per-reader rendering. The Vision (§1) is product-specific and could not swap into another PRD. NFRs are real and bounded by constraint ("No Chromium dependency — rules out Electron, Chromium webviews, Chrome extensions"), not boilerplate. Differentiation is honestly hedged in the addendum ("plumbing is commoditizing; moat is non-technical"), which is the opposite of innovation theater.

### Findings
- **low** "Looks awesome" as a Vision-adjacent quality bar (§4.2 FR-6) — the phrase "such that content 'looks awesome'" is the one spot that reads aspirational rather than specified. It is partly rescued by the SM-2 link. *Fix:* keep the SM-2 reference but name one or two concrete bars (e.g., "web-font typography, max-width measure, responsive media") so "awesome" has a floor.

## Strategic coherence — strong

The PRD has a clear thesis and the feature arc follows it: content layer → born-compatible HTML path → per-reader native rendering → content negotiation that ties the two → sharing (deferred). Prioritization follows the thesis, not ease — the founder explicitly keeps the hard native client *in* MVP rather than shipping only the easy static slice. Success Metrics validate the thesis (SM-3 measures per-reader rendering "end to end, not mocked"), not vanity activity; there is no DAU/MAU tell. Counter-metrics exist and are pointed (SM-C1 beauty/speed vs breadth; SM-C2 born-compatibility vs personalization). No findings.

## Done-ness clarity — thin

This is the weakest dimension and the one downstream epics will lean on hardest. Several FRs carry no "Consequences (testable)" block at all, and the most strategically important capability (per-reader rendering) is specified through outcomes that are real but hard to bound.

### Findings
- **high** FRs missing testable consequences (§4.1 FR-4, §4.2 FR-8, §4.3 FR-13, §4.5 FR-15/FR-16, §4.6 FR-18) — six FRs have no Consequences block. FR-4 ("browsable space"), FR-8 ("Navigation"), and FR-18 ("custom domain over HTTPS") are in-MVP and an engineer would have to invent "done." FR-15/16 are deferred so lighter treatment is defensible, but FR-4/FR-8/FR-18 are not. *Fix:* add one verifiable consequence each — e.g., FR-18: "themarkdownweb.com serves over TLS with a valid cert and no mixed-content warnings"; FR-8: "every rendered inter-page link is reachable by click from the vault index without typing a URL."
- **medium** Per-reader rendering bounded only by "materially different" (§4.3 FR-10, SM-3) — "materially different renderings" and "materially differently for two readers" is the core proof of the product, yet "materially" is undefined. Two readers seeing a different font size would technically pass. *Fix:* enumerate the rendering axes that count as material (reading level, language, format/audio, section reordering) and require at least one structural (not cosmetic) difference for SM-3 to count.
- **medium** FR-11 outcomes lack acceptance bounds (§4.3) — "audio, large-text/reflowed, translated" are listed as possible outcomes but none has a pass condition (e.g., what makes the audio rendering acceptable? round-trip fidelity of translation?). *Fix:* pick the one or two outcomes that must work for MVP and give each a consequence; mark the rest as illustrative.
- **low** FR-9 has no Consequences block (§4.3) — the entry-point FR for the native client states the capability but not its done-state. *Fix:* add "opening a published `.md` URL in the native client yields agent-produced output, not raw text or an error."

## Scope honesty — strong

Omissions are explicit and load-bearing. §5 Non-Goals does real work (not an editor, not a CMS, not a universal AI browser, no accounts, not monetized) and each maps to a thesis decision. The `[NON-GOAL for MVP]` tag sits on Sharing (§4.5); `[NOTE FOR PM]` sits at the genuine MVP-size tension (§6). De-scoping is proposed honestly — sharing is "committed, sequenced — not cut" (§6.2), distinguishing deferral from abandonment. Open-items density (6 Open Questions + ~3 assumptions + 1 PM note) is appropriate for a foundation PRD, not a green-light-to-build, and the founder knows it. No findings.

## Downstream usability — adequate

This PRD is chain-top (it feeds UX/architecture/epics per §0), so traceability matters. Glossary is present and disciplined; IDs are contiguous and unique (FR-1…FR-18, UJ-1…3, SM-1…4 + SM-C1/C2). Cross-references mostly resolve. The gaps are minor but real for source-extraction.

### Findings
- **medium** FR→UJ back-references are uneven (§4.1–§4.6) — UJ-1 claims to realize FR-17 and FR-18, and UJ-3 claims FR-14, but those FRs don't name their UJ inline; the link resolves only through the parent feature's "Realizes UJ-1" header. Most FRs that *do* carry inline `Realizes UJ-n` make the inconsistency conspicuous. *Fix:* either add inline `Realizes UJ-n` to FR-14/FR-17/FR-18 or state once that feature-level "Realizes" cascades to its FRs.
- **low** "Feed" Glossary term vs FR-16 "Follow / Feed" (§3, §4.5) — minor: the Glossary defines "Feed" but not "Follow"; FR-16 introduces "follow" as a verb. Low impact since FR-16 is deferred. *Fix:* add "Follow" to the Glossary when Sharing is pulled forward.

## Shape fit — strong

The shape matches the product. This is a consumer-facing product with meaningful UX, so named-protagonist UJs are load-bearing — and they are present and doing work (UJ-2's no-JavaScript edge case is exactly the kind of detail that earns a UJ). Rigor is calibrated to solo/early-stage: no enterprise ceremony, no over-formalization, but the substance bar is met. The addendum correctly quarantines tech-how (Azure, HTTP Accept/Vary, rendering engines) out of the capability PRD. The PRD is neither over- nor under-formalized for its stakes. No findings.

## Mechanical notes

- **Glossary drift:** clean. "Vault," "`.md` page," "Living Link," "Per-reader rendering," "Local agent" are used consistently in case and number across FRs/UJs/SMs. One missing term: "Follow" (used in FR-16, not defined).
- **ID continuity:** FR-1 through FR-18 contiguous and unique; UJ-1–3 and SM-1–4 + SM-C1/C2 clean. No gaps or duplicates.
- **Assumptions Index roundtrip:** §9 lists three entries. The FR-13 inline `[ASSUMPTION]` and the §8.2 inline `[ASSUMPTION]` both round-trip. The §6 index entry ("MVP includes both clients…") has **no matching inline `[ASSUMPTION]` tag** in §6 — it's asserted as confirmed in prose. Minor: either drop it from the Assumptions Index (it's a confirmed decision, not an assumption) or tag the corresponding inline note.
- **UJ protagonist naming:** all three UJs have named protagonists carrying context inline (naethyn, Dana, Theo). No floating UJs.
- **Required sections:** all expected sections present for a chain-top early PRD — Vision, Target User/JTBD, Glossary, Features+FRs, Non-Goals, MVP Scope, Success Metrics + counter-metrics, Open Questions, Assumptions Index. Nothing missing.
- **Cross-ref nit:** §8 Open Questions are referenced as "§8.1," "§8.2" elsewhere (e.g., §9, FR-13) but §8 items are a plain numbered list, not sub-numbered headings. Harmless but the sub-section references don't resolve to literal anchors.
