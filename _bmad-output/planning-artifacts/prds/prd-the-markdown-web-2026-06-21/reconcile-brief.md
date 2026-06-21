# Brief → PRD Reconciliation: Gaps

Reconciling the source brief (`briefs/brief-the-markdown-web-2026-06-21/brief.md` + `addendum.md`) against the PRD (`prds/prd-the-markdown-web-2026-06-21/prd.md` + `addendum.md`). These are material things present in the brief that the PRD drops, weakens, or buries. No rewrites proposed.

---

## Gap 1 — "Zero Shared Pixels for humans" — the product's sharpest coinage — is gone

**Brief (Solution, line 31):** the native client is defined as "the heart of the product: **'Zero Shared Pixels for humans.'**" This is the brief's single most concentrated articulation of the thesis — no two readers see the same rendered output; the only thing shared is the source `.md`.

**PRD:** §1 Vision and §4.3 (the native client feature) describe per-reader rendering functionally ("One source file, a personal experience per reader," "reflowing, re-leveling, translating, re-emphasizing") but never carry the coined phrase or its absolutist claim. The Glossary (§3) defines "Per-reader rendering" in mechanical terms (layout, reading level, language, accessibility, emphasis) and loses the *zero-shared-pixels* idea entirely.

**Why it matters:** "Zero Shared Pixels" is a memorable, falsifiable design north-star and a category-defining phrase. The FR decomposition (FR-10's testable consequence — "two readers... see materially different renderings") quietly relaxes the bar from *zero shared pixels* (radical) to *materially different* (modest). That is a real weakening of the vision's ambition, not just a missing slogan.

---

## Gap 2 — The one-line wedge / industry-conversation hook is lost

**Brief (What Makes This Different, line 44):** "The one-line wedge into the whole industry conversation: *everyone says markdown is for models and HTML is for humans — what if markdown were for humans too, rendered by their agent?*"

**PRD:** §1 Vision paraphrases the lingua-franca idea ("if markdown is the lingua franca for machines, it can be the lingua franca for humans too") but drops the *contrarian framing* — the "everyone says X / what if Y" reversal that positions the product against the prevailing 2025–26 consensus (markdown-for-models). The PRD has no positioning/wedge section at all; differentiation survives only implicitly in §2.2 Non-Users and §5 Non-Goals.

**Why it matters:** This is the messaging spearhead and the thing that makes the product legible in the current discourse. SM-2 ("'Gets it' on sight... can articulate the vision unprompted") is the success metric most dependent on exactly this framing, yet the PRD gives the builder no captured language to design that "gets it" moment around.

---

## Gap 3 — The manifesto-*is*-the-proof-of-concept claim is softened to "seed content"

**Brief (Executive Summary line 18; Success Criteria line 54):** repeatedly and emphatically, "the manifesto, **rendered, *is* the proof of concept**" and "the manifesto, rendered, *is* the proof of concept." The brief frames the manifesto not as test data but as the *artifact that demonstrates the thesis* — its beauty when rendered is itself the v0.1 success bar.

**PRD:** the manifesto is demoted to "Seed content: the manifesto and planning docs" (§6.1) and appears in UJ-1 as one of several files naethyn points at. The load-bearing claim — *the rendered manifesto is the proof* — is nowhere stated. SM-2 measures "a beautiful publication" generically rather than "the manifesto specifically lands as proof."

**Why it matters:** This collapses a vision beat (the manifesto as the self-demonstrating centerpiece) into ordinary fixture data. It removes a clear, opinionated acceptance target ("does the rendered manifesto itself sell the idea?") and the dogfooding-as-proof narrative that justifies customer-zero sequencing.

---

## Gap 4 — Defensibility / "the moat is non-technical" is exiled to the addendum and never shapes requirements

**Brief addendum (lines 6–9):** an explicit, sharp strategic caveat — "**Plumbing is commoditizing**" (content negotiation, md/HTML dual-serve are being given away), "**the native client is attackable**" by Comet/Dia/Atlas/A2UI, and "**therefore the moat is non-technical**: category ownership, the publish↔read loop, an opinionated beautiful client, community, the personal-vault wedge."

**PRD:** this survives only as two compressed lines in the *PRD addendum* (lines 9, 14: "do not over-invest in building novel plumbing... the differentiator is per-reader human-facing rendering"). The main PRD body never states the moat thesis. Consequently the requirements give no special weight to the things the brief names as the *actual* defensibility — "an opinionated beautiful client" is only FR-6 ("high-quality default theme"), and "category ownership" / "the publish↔read loop" / "community" / "personal-vault wedge" have no FRs or success metrics at all.

**Why it matters:** The brief's central strategic insight is that the technical layers are commodity and the moat lives elsewhere. A PRD that buries this risks the builder over-indexing on plumbing FRs (content negotiation, hosting) and under-investing in the non-technical moat the brief explicitly flags as the win condition.

---

## Gap 5 — Audience subtlety: "will tolerate an early native client" and the snowboardcto.com model are dropped

**Brief (Who This Serves, line 48):** the primary audience "intuitively grasp the agent-rendering thesis and **will tolerate an early native client**." This is a precise tolerance assumption — the early adopters are chosen *because* they'll forgive rough edges in the hardest-to-build piece.

**Brief (Scope, line 69; Roadmap):** the publishing mechanism is anchored to a concrete reference model — "**auto-publish on push (the snowboardcto.com model)**."

**PRD:** §2 Target User describes the audience's markdown accumulation and intuition but omits the *tolerance* point — the explicit license to ship a rough native client to a forgiving cohort. And FR-17 ("Publish on push") plus the addendum describe the mechanism (GitHub Actions → Azure) but drop the snowboardcto.com reference point that grounds the expected author workflow/feel.

**Why it matters:** The tolerance subtlety is what makes the "large MVP / both clients" decision (§6 NOTE FOR PM) defensible — it's the audience justification for accepting native-client roughness, and without it the MVP-size risk reads as unmitigated. The snowboardcto.com reference is a concrete, validated UX/workflow exemplar the builder loses when it's abstracted to "GitHub Actions CI/CD."

---

## Lower-confidence / minor

- **"That content is meaningful but homeless"** (brief, Problem, line 22) — the emotional framing of the core pain (markdown is meaningful but has no good home) is functionally captured in JTBD-1 ("Keep my markdown somewhere good") but loses the evocative "homeless content" phrasing that motivates the wedge.
- **Author incentive as a live tension** — the brief lists "why authors publish `.md` and **surrender presentation control**" as an open question (line 83); the PRD keeps it (§8.6) but never surfaces the *surrender of control* as the genuinely counterintuitive ask it is in the brief's framing.
