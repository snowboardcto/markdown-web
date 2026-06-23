# Story 6.2: Open any http(s) URL

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want to type any http(s) URL into the address bar, not just .md URLs,
so that I can point the client at any site to read its markdown.

## Context note — SECOND Epic-6 story; RELAXES the ".md only" rule WITHOUT breaking the 3.2 tests

> This story REVISES — does not delete — the address-bar acceptance rule. Today the client accepts ONLY `.md` URLs and DECLINES everything else (Story 3.2 / UX-DR5 ".md only"). 6.2 widens acceptance to ANY `http(s)` URL so a non-`.md` URL is ACCEPTED and handed to **markdown discovery (Story 6.3)** instead of being declined. The `.md`-only fast path stays (a `.md` URL still routes straight through the existing endpoint mapping). Non-`http(s)` schemes (`ftp:`, `javascript:`, `file:`, `mailto:`) are STILL declined.
>
> **⚠️ UX-DR5 REVISION (flagged explicitly):** UX-DR5 ("address-bar loads `.md` URLs only; shows … `.md only` tag; non-`.md` declined") is SUPERSEDED by FR-20. PRD §4.7 FR-20 explicitly "supersed[es] the `.md`-only restriction of FR-9 / the original address-bar rule," and the open-questions log (PRD lines 309–310) records: "§4.7 FR-20 — Supersedes the original `.md`-only address-bar rule (FR-9 / UX-DR5 'non-`.md` declined'); UX-DR5 must be revised when Epic 6 is cut." Epics.md Story 6.2 (line 494) mandates: *"UX-DR5's `.md only` rule is revised to `.md-discoverable` (the `.md only` tag/behavior is updated accordingly) without breaking existing address-bar tests."* So this story (a) updates the `.md only` tag's copy/`AutomationProperties.Name` to reflect "any markdown-discoverable URL" and (b) relaxes the acceptance predicate — and it must do BOTH without breaking the Story 3.2 `AddressBarValidationTests` / `AddressBarWindowTests`.

**What exists today (verified — build ON this, do not recreate):**
- `clients/windows/App/AddressBarValidation.cs` — `public static bool IsLoadableMarkdownUrl(string?)` (the `.md`-only predicate, pure/total, exhaustively tested by `AddressBarValidationTests`) and `TryGetHostPath`. 6.2 ADDS a new predicate (`IsAcceptableUrl` / `IsLoadableHttpUrl`) rather than mutating `IsLoadableMarkdownUrl` (keeps the 3.2 matrix tests green and lets the `.md` fast path still recognize `.md` URLs).
- `clients/windows/App/MainWindow.xaml.cs` — `AddressInput_KeyDown` (lines 295–316) is the submission branch: if `IsLoadableMarkdownUrl(input)` AND parses absolute → `_controller.NavigateToAsync(pageUrl)`; ELSE → `_addressBar.SubmitAsync()` (the 3.2 decline-and-offer-system-browser UX). 6.2 inserts a THIRD branch: a non-`.md` but valid `http(s)` URL → proceed to discovery (6.3), not decline.
- `clients/windows/App/AddressBarViewModel.cs` — `enum AddressBarState { Idle, Loading, Loaded, NotMarkdown, Broken }`; `SubmitAsync` declines non-`.md` input to `NotMarkdown` + sets `DeclinedUrl` (offer system browser) for absolute `http(s)`; `OpenDeclinedInBrowser()`. 6.2 must keep the decline path for non-`http(s)` schemes but STOP declining valid `http(s)` non-`.md` URLs.
- `clients/windows/App/MainWindow.xaml` — the address bar `Border x:Name="AddressBar"` with `LockIndicator`, `AddressInput`, and `MdOnlyTag` (text EXACTLY `.md only`, `AutomationProperties.Name="Loads .md pages only"`). The `MdOnlyTag` copy is what UX-DR5's revision updates.
- `clients/windows/App.Tests/AddressBarValidationTests.cs`, `AddressBarViewModelTests.cs`, `AddressBarWindowTests.cs` — the 3.2 conventions: a `[Theory]` true/false matrix for the predicate; `[Fact]` VM state-machine + decline/launcher with fakes; `[StaFact]` construct-not-`Show` for the bar. `AddressBarWindowTests.MdOnlyTag_Text_IsExactlyDotMdOnly` asserts the tag text — if 6.2 changes the tag copy, this test must be reconciled (see AC5).

### ⚠️ ENVIRONMENT CONSTRAINT — read before writing any code or test

**Linux dev box, NO .NET SDK; WPF builds/runs ONLY on Windows. Verification is EXCLUSIVELY `build-windows.yml` on `windows-latest`.** The new acceptance predicate is PURE → `[Fact]`/`[Theory]` (no window, no network). The address-bar tag/decline behavior is exercised by `[Fact]` on the VM (with fakes) plus `[StaFact]` construct-not-`Show` on the window. NEVER `.Show()`; no socket, no live pump, no pixels, no timing.

## Acceptance Criteria

> Source: [_bmad-output/planning-artifacts/epics.md#Story 6.2] (lines 482–494): **Given** the address bar **When** I enter an http(s) URL that does not end in .md **Then** the client accepts it and proceeds to markdown discovery (Story 6.3) instead of declining **And** a non-http(s) scheme (ftp:, javascript:, file:) is still declined **And** UX-DR5's ".md only" rule is revised to ".md-discoverable" (the ".md only" tag/behavior is updated accordingly) without breaking existing address-bar tests. (FR-20.) PRD §4.7 FR-20 "Open any URL": accepts any http(s) URL, superseding the .md-only restriction; non-http(s) still declined; testable — entering `https://example.com/docs/intro` triggers discovery, not an immediate decline; `ftp:`/`javascript:` still declined.

1. **[New acceptance predicate — accept any absolute http(s) URL, pure + TOTAL]** **Given** a string entered in the address bar, **When** it is validated for acceptance, **Then** a pure App-side predicate (e.g. `AddressBarValidation.IsAcceptableUrl(string?)`) returns **true** iff the input, after trimming surrounding whitespace, is an **absolute** `http`/`https` URL (scheme compared `OrdinalIgnoreCase`), and **false** for every other input (relative, non-`http(s)` scheme, null/empty/whitespace, unparseable). It NEVER throws for any `string?`. The existing `IsLoadableMarkdownUrl` is RETAINED unchanged (it stays the `.md` fast-path discriminator) — 6.2 ADDS the broader predicate, it does not mutate the `.md` one.

   **`IsAcceptableUrl` true/false matrix (exhaustive + total):**

   | Input | Result | Why |
   |---|---|---|
   | `https://themarkdownweb.com/guides/x.md` | **true** | absolute https (a `.md` URL is also acceptable) |
   | `https://example.com/docs/intro` | **true** | absolute https, no `.md` — accepted (→ discovery) |
   | `http://host/page` | **true** | http allowed |
   | `https://h/a/b/c?ref=1#sec` | **true** | query/fragment fine |
   | `  https://h/x  ` | **true** | surrounding whitespace trimmed |
   | `HTTPS://H/X` | **true** | scheme compare case-insensitive |
   | `null` / `""` / `"   "` / `"\t\n"` | **false** | null/empty/whitespace guard (no throw) |
   | `mailto:x@y.z` | **false** | non-http scheme |
   | `ftp://h/x.md` | **false** | non-http scheme |
   | `file:///c:/x.md` | **false** | non-http scheme (no local-file load) |
   | `javascript:alert(1)` | **false** | non-http scheme (no script execution) |
   | `not a url` | **false** | unparseable as absolute Uri |
   | `host/x` | **false** | relative (no scheme) |
   | `/docs/intro` | **false** | relative (no scheme/authority) |

   *(AC1 — FR-20 "accepts any http(s) URL … non-http(s) declined"; mirror the `AddressBarValidation` purity/totality discipline of 3.2.)*

2. **[Submission routes by kind — `.md` fast path / non-`.md` http(s) → discovery / non-http(s) → decline]** **Given** the reader submits the address bar (Enter), **When** the input is classified, **Then** there are exactly THREE outcomes:
   - **(a) loadable `.md` URL** (`IsLoadableMarkdownUrl` true) → navigate via the EXISTING `NavigationController.NavigateToAsync` path (unchanged from 3.5);
   - **(b) acceptable http(s) but NOT `.md`** (`IsAcceptableUrl` true AND `IsLoadableMarkdownUrl` false) → **PROCEED TO DISCOVERY** (Story 6.3) rather than decline. At THIS story, 6.3's discovery service does not yet exist, so 6.2 establishes the **seam/branch**: it must NOT route this case to the `NotMarkdown` decline; it routes to a discovery entry point (a method/hook 6.3 fills — e.g. `BeginDiscoveryAsync(uri)` or a documented TODO seam that 6.3 implements). The acceptance test for 6.2 is that this case is NOT declined (no `NotMarkdown`, no system-browser offer) and reaches the discovery seam;
   - **(c) NOT acceptable** (`IsAcceptableUrl` false — non-`http(s)` scheme or unparseable) → the EXISTING 3.2 decline UX (`AddressBarViewModel.SubmitAsync` → `NotMarkdown`; `DeclinedUrl` populated ONLY for absolute http(s), which by construction is empty here, so no system-browser offer for `ftp:`/`javascript:`/`file:`/non-URL — consistent with 3.2).

   This is wired in `MainWindow.xaml.cs` `AddressInput_KeyDown` (the three-way branch) and/or in a pure dispatcher so the routing decision itself is `[Fact]`-testable without a window. *(AC2 — FR-20 testable "entering `https://example.com/docs/intro` triggers discovery, not an immediate decline"; reuse the 3.5 navigate path + the 3.2 decline path.)*

3. **[Non-http(s) schemes still declined]** **Given** a non-`http(s)` scheme (`ftp:`, `javascript:`, `file:`, `mailto:`) or an unparseable string, **When** submitted, **Then** the client DECLINES it (no navigation, no discovery) and surfaces the existing `NotMarkdown` decline state. For a non-`http(s)` scheme, `DeclinedUrl` stays `null` (no system-browser offer — the 3.2 contract: the offer is only for absolute `http(s)`), so the client neither loads nor offers to launch a `javascript:`/`file:` URL. *(AC3 — FR-20 "a non-http(s) scheme (ftp:, javascript:) is still declined"; reuse 3.2 `AddressBarViewModel` decline semantics unchanged.)*

4. **[UX-DR5 revision — `.md only` → markdown-discoverable copy + a11y name]** **Given** UX-DR5's `.md only` rule is superseded by FR-20, **When** the address bar displays, **Then** the `MdOnlyTag` element's user-facing copy and its `AutomationProperties.Name` are UPDATED to reflect "any markdown-discoverable URL" rather than "loads `.md` only" — e.g. the tag reads something like `.md-discoverable` (or `markdown` / a short equivalent) and the accessible name reads e.g. "Reads markdown-discoverable URLs" (DECIDE-AND-DOCUMENT the exact copy + a11y string, grounded in the epics.md `.md-discoverable` wording). The element stays present, keyboard-context-correct, and `AutomationProperties.Name` stays non-empty (a11y floor preserved). The lock indicator and the input's accessible name are unchanged. *(AC4 — epics.md 6.2 "UX-DR5's `.md only` rule is revised to `.md-discoverable`"; PRD open-question 309–310 "UX-DR5 must be revised"; UX-DR9/NFR-6 a11y floor.)*

5. **[Existing 3.2 address-bar tests stay green / reconciled minimally]** **Given** the revision touches the predicate set and the tag copy, **When** the WPF suite runs, **Then** the existing `AddressBarValidationTests` (the `.md`-only matrix over the UNCHANGED `IsLoadableMarkdownUrl`) stay green verbatim (because 6.2 adds a new predicate rather than mutating the old one), the `AddressBarViewModelTests` decline/launcher/state-machine tests stay green for the non-`http(s)` cases (the 3.2 decline contract is unchanged for those), and the ONE `[StaFact]` that asserts the tag text — `AddressBarWindowTests.MdOnlyTag_Text_IsExactlyDotMdOnly` (asserts exactly `.md only`) — is reconciled with the MINIMAL justified edit to the new copy (AC4). No broad rewrite; epics.md mandates the revision "without breaking existing address-bar tests," meaning: keep them passing, editing only the single tag-text assertion that the deliberate copy change invalidates. *(AC5 — epics.md 6.2 "without breaking existing address-bar tests"; the 3.2 test suite as the regression contract.)*

6. **[App owns acceptance/routing; Rendering pure; no embedded browser; no regression]** **Given** this is additive validation + routing, **When** boundaries + suites are inspected, **Then** all new logic lives in `App`; `Rendering`/`Agent` are untouched; `DependencyBoundaryTests` + `NoEmbeddedBrowserTests` stay green (no new forbidden dep — accepting a URL adds no webview); and the full WPF suite + the new 6.2 tests pass on `windows-latest`. The 6.1 launch-to-home + Home button stay green (6.2 does not touch them). *(AC6 — NFR-1/architecture FC-1; standing purity/boundary/no-regression guards; build-windows.yml CI gate, the only verification surface.)*

## Tasks / Subtasks

- [x] **Task 1 — New acceptance predicate in `AddressBarValidation` (AC: 1)**
  - [x] In `clients/windows/App/AddressBarValidation.cs`, added `public static bool IsAcceptableUrl(string? input)`: trim, `Uri.TryCreate(_, UriKind.Absolute, out uri)` must succeed, and `uri.Scheme` ∈ {`http`,`https`} (OrdinalIgnoreCase). Returns false for null/empty/whitespace, relative, non-http scheme, unparseable. Pure/total (never throws). `IsLoadableMarkdownUrl` untouched. [Source: AC1; AddressBarValidation purity discipline]

- [x] **Task 2 — Three-way submission routing in `MainWindow` (AC: 2, 3)**
  - [x] In `clients/windows/App/MainWindow.xaml.cs` `AddressInput_KeyDown`, replaced the current 2-way branch with 3-way: (a) `IsLoadableMarkdownUrl(input)` + parse → `_controller.NavigateToAsync(pageUrl)` (unchanged); (b) ELSE IF `IsAcceptableUrl(input)` + parse → `await BeginDiscoveryAsync(discoveryUrl)` (the 6.3/6.4 discovery seam, NOT decline); (c) ELSE → `_addressBar.SubmitAsync()` (3.2 decline). [Source: AC2/AC3]
  - [x] Non-`http(s)` decline path unchanged: `ftp:`/`javascript:`/`file:`/non-URL submit still reaches `AddressBarViewModel.SubmitAsync` → `NotMarkdown` with `DeclinedUrl == null` (no system-browser offer). [Source: AC3]

- [x] **Task 3 — UX-DR5 revision: update the tag copy + a11y name (AC: 4, 5)**
  - [x] In `clients/windows/App/MainWindow.xaml`, updated `MdOnlyTag` `Text` → `.md-discoverable`, `AutomationProperties.Name` → `"Reads markdown-discoverable URLs"`. Element stays present with non-empty a11y name. [Source: AC4; DECIDE-AND-DOCUMENT: copy ".md-discoverable" per epics.md wording]

- [x] **Task 4 — Tests (AC: 1, 2, 3, 4, 5, 6)**
  - [x] **`[Theory]` AC1 — `IsAcceptableUrl` matrix** added to `AddressBarValidationTests.cs`: all rows of the true/false matrix; never-throws check; subset relationship (`IsLoadableMarkdownUrl` ⊆ `IsAcceptableUrl`). Existing `.md`-matrix `[Theory]` over `IsLoadableMarkdownUrl` intact. [Source: AC1]
  - [x] **`[StaFact]` AC4 — revised tag** reconciled in `AddressBarWindowTests.MdOnlyTag_Text_IsExactlyDotMdOnly`: asserts `.md-discoverable` (was `.md only`). `AddressBar_SubElements_HaveNonEmptyAutomationNames` stays green. [Source: AC4/AC5]
  - [x] Regression: all `.md`-matrix tests, VM decline tests, and 6.1's tests stay green. Only the single tag-text assertion was reconciled. [Source: AC5]

- [x] **Task 5 — CI / boundary hygiene + final verification (AC: 6)**
  - [x] No new `PackageReference`; `NoEmbeddedBrowserTests` + `DependencyBoundaryTests` green; `Rendering`/`Agent` untouched. [Source: AC6]
  - [x] **DoD:** AC1 predicate matrix; AC2 three-way routing; AC3 non-http(s) still declined; AC4 UX-DR5 tag revised; AC5 3.2 tests reconciled; AC6 green CI. [Source: AC1–6]

## Dev Agent Record

### Decisions

1. **UX-DR5 copy (DECIDE-AND-DOCUMENT):** Tag text: `.md-discoverable`; `AutomationProperties.Name`: `"Reads markdown-discoverable URLs"`. Grounded in epics.md line 494 wording. Minimal justified edit: only `MdOnlyTag_Text_IsExactlyDotMdOnly` assertion updated.

2. **Discovery seam in 6.2 (DECIDE-AND-DOCUMENT):** Used `await BeginDiscoveryAsync(discoveryUrl)` — a real method (not a placeholder), co-implemented as part of Epic 6 since all four stories are implemented together. The three-way branch is NOT declined for `http(s)` non-.md URLs.

3. **No pure SubmitRoute classifier extracted:** The three-way routing stays in `AddressInput_KeyDown` code-behind (consistent with the existing 3.5 branch). Covered by `[StaFact]` smoke + predicate `[Theory]`.

### File List

- `clients/windows/App/AddressBarValidation.cs` — UPDATED (added `IsAcceptableUrl`)
- `clients/windows/App/MainWindow.xaml` — UPDATED (MdOnlyTag text + a11y name)
- `clients/windows/App/MainWindow.xaml.cs` — UPDATED (3-way routing + BeginDiscoveryAsync seam)
- `clients/windows/App.Tests/AddressBarValidationTests.cs` — UPDATED (IsAcceptableUrl theory tests)
- `clients/windows/App.Tests/AddressBarWindowTests.cs` — UPDATED (MdOnlyTag assertion updated)

## Dev Notes

### ⚠️ UX-DR5 REVISION (the load-bearing scope item — flagged for the coordinator)
FR-20 SUPERSEDES the `.md`-only rule (PRD §4.7 FR-20; open-question PRD 309–310; epics.md 6.2 line 494). 6.2 (1) relaxes acceptance to any `http(s)` URL and (2) updates the `.md only` tag → `.md-discoverable` copy + a11y name. The mandate is to do this **without breaking existing address-bar tests** — achieved by ADDING `IsAcceptableUrl` (not mutating `IsLoadableMarkdownUrl`) and reconciling exactly ONE tag-text `[StaFact]` assertion.

### Decide-and-document points
- The exact revised tag copy + `AutomationProperties.Name` (grounded in epics.md `.md-discoverable`). [Source: AC4]
- Whether the discovery seam in 6.2 is a real `BeginDiscoveryAsync` hook (preferred — 6.3 fills it) or a documented placeholder branch; the contract is: a non-`.md` http(s) URL is NOT declined. [Source: AC2]
- Whether to extract a pure `SubmitRoute` classifier (recommended for windows-only `[Fact]` testability of the three-way decision). [Source: AC2]

### Critical constraints (do not violate)
- **Add, don't mutate:** keep `IsLoadableMarkdownUrl` + its matrix tests verbatim; add `IsAcceptableUrl`. [Source: AC1/AC5]
- **Non-http(s) still declined** with the 3.2 semantics (no system-browser offer for non-http(s)). [Source: AC3]
- **App owns acceptance/routing; Rendering pure; no webview.** [Source: AC6; DependencyBoundaryTests/NoEmbeddedBrowserTests]
- **Scope: relax acceptance + revise the tag + route non-`.md` http(s) to the discovery SEAM ONLY.** The discovery cascade ITSELF is Story 6.3; the render of discovered markdown + the no-markdown state is 6.4. Do not implement them here. [Source: epics.md Epic 6 sequence]
- **Windows-only verification** — `[Fact]`/`[Theory]` for predicates/routing; `[StaFact]` construct-not-`Show` for the tag. No socket/window-show/pump/pixel/timing. [Source: Environment Constraint]

### Source tree components to touch
- `clients/windows/App/AddressBarValidation.cs` — add `IsAcceptableUrl` (UPDATE; do not change `IsLoadableMarkdownUrl`).
- `clients/windows/App/MainWindow.xaml.cs` — three-way `AddressInput_KeyDown` routing + discovery seam (UPDATE).
- `clients/windows/App/MainWindow.xaml` — revised `MdOnlyTag` copy + a11y name (UPDATE).
- (Optional) a pure `SubmitRoute` classifier (NEW) for `[Fact]` testability.
- `clients/windows/App.Tests/AddressBarValidationTests.cs` (ADD `IsAcceptableUrl` theory), `AddressBarWindowTests.cs` (reconcile tag-text assertion), `AddressBarViewModelTests.cs` (extend non-http(s) decline if needed).
- Do NOT touch: `Rendering/*`, `Agent/*`, `NavigationController.cs`/`PageEndpointResolver.cs` contracts, `build-windows.yml`, `TheMarkdownWeb.sln`.

### Cross-story dependencies
- **6.2 → 6.3:** the non-`.md` http(s) branch routes to the discovery seam 6.3 implements (`MarkdownDiscoveryService`). 6.2 owns the BRANCH; 6.3 owns the cascade. Coordinate the seam signature (a URL in, a discovery result out) so 6.3 plugs in cleanly.
- **6.2 → 6.4:** 6.4 wires the discovery result into the render pipeline + the no-markdown/bot-blocked states; 6.2's branch is where that flow starts.
- **6.1 ← 6.2:** 6.1's launch/Home target is an app-host `.md`/page URL and uses the existing path — unaffected by the relaxed acceptance.

### Testing standards summary
- xUnit; `[Theory]` for the `IsAcceptableUrl` matrix + the retained `.md` matrix; `[Fact]` for the three-way routing classifier and the non-http(s) decline; `[StaFact]` construct-not-`Show` for the revised tag. Assert against the REAL `AddressBarValidation`/`AddressBarViewModel`/`MainWindow`, not re-declared stubs. No real network, no shown window, no pump/pixel/timing.
