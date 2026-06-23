# Epic 6 — The Markdown Lens — AC Trace Report

Date: 2026-06-23 · Branch: claude/enhanced-automated-epic-6-lens · Verified: windows-latest CI run on e7d9301 (build clean, 485 tests pass)

All Epic 6 stories are native .NET/WPF and verified on `windows-latest` CI (build-windows.yml), not locally (no dotnet on the dev host). The consolidated code review (PASS WITH ITEMS) verified AC coverage; all 9 review items (2 HIGH + 4 MEDIUM + 3 LOW) were fixed and re-verified green.

## Story 6.1 — Default home (FR-19): 6/6 ACs covered
| AC | Coverage |
|----|----------|
| Single canonical home Uri | `HomeNavigatorTests` (HomeUrl absolute/https/host/IsAppHost) |
| Launch loads home (not blank) | `HomeNavigatorTests.NavigateHomeAsync_FetchesHomeUrl_AndRendersContent` + `Loaded` hook |
| Home button returns home | `HomeButtonWindowTests` (exists, a11y name "Home", focusable, tab order) |
| App-owns-nav / Rendering pure / no webview | `NoEmbeddedBrowserTests`, boundary guards (pass) |
| No nav-count regression | nav StackPanel stays 3 — Home in its own column (`HomeButtonWindowTests`, 3 guards) |
| windows CI gate | run e7d9301 green |

## Story 6.2 — Open any URL (FR-20): 6/6 ACs covered
| AC | Coverage |
|----|----------|
| `IsAcceptableUrl` http(s) matrix + total | `AddressBarValidationTests` (true/false/never-throws + subset property) |
| 3-way routing (.md / non-.md→discovery / decline) | `AddressBarViewModelTests` |
| non-http(s) declined | `AddressBarValidationTests` / `AddressBarViewModelTests` |
| UX-DR5 revision (.md only → .md-discoverable) | `AddressBarWindowTests.MdOnlyTag_Text_IsExactly...` |
| 3.2 tests reconciled, not broken | existing AddressBar tests green |
| no regression | full suite green |

## Story 6.3 — Markdown discovery service (FR-21): 8/8 ACs covered
| AC | Coverage |
|----|----------|
| Ordered cascade (Accept+alternate → .md sibling → llms.txt index) | `MarkdownDiscoveryServiceTests` (Step1/1b/2/3, first-hit-wins, Step1Hit→no 2/3) |
| Bounded probe budget + timeout | `...IssuesAtMostMaxProbesGETs`, 4-GET test, per-probe timeout test |
| pure/total never-throws | null/network-error tests |
| Content-Type + doctype/BOM/comment guards (zero false positives) | `MarkdownCandidateValidatorTests` (HTML/BOM/comment/text-plain rejected; markdown accepted) |
| honest UA + 403→Blocked (short-circuit) | Step1/1b/2/3 403 short-circuit tests |
| bounded redirects ≤5 | `MaxAutomaticRedirections=5` + overflow-totality test |
| App-only / no webview / regex head-parse | `NoEmbeddedBrowserTests`, purity guards |
| deterministic fake-handler tests + gated live probe | full service suite + `MarkdownDiscoveryLiveProbeTests` (8 SKIP in CI) |

## Story 6.4 — Render + no-markdown state (FR-21 integration, FR-22): 7/7 ACs covered
| AC | Coverage |
|----|----------|
| PageMarkdown → gateway → Markdig render (per-reader) | `DiscoveryRenderFlowTests` (personalization prefix reaches sink; Basic pass-through byte-identical) |
| "no markdown available" state (no HTML fallback) | `DiscoveryStateWindowTests` (exact a11y name) |
| bot-blocked distinct message | `DiscoveryStateWindowTests` (distinct a11y names: Blocked≠NoMarkdown≠Broken) |
| llms index as resources, not page body | `DiscoveryRenderFlowTests` + state tests (cap-20) |
| total/last-wins dispatch | `DiscoveryOutcomeDispatcherTests` + generation-token last-wins test |
| no Epic 3/4/5 regression | full suite green |
| App-only + no webview + CI gate | guards + run e7d9301 green |

**Coverage gaps: none.** Total native tests: 485 (incl. 8 skipped live probes). Web suite unaffected.
</content>
