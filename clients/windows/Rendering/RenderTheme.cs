namespace TheMarkdownWeb.Rendering;

/// <summary>
/// The render-theme / personality-override seam (Story 3.6, AC3). At Epic 3 the ONLY member is
/// <see cref="Basic"/> — the faithful GitHub-light "no-personality" default render. It is also the
/// default value of <see cref="FlowDocumentRenderOptions.Theme"/>, so the bedrock render IS the
/// faithful basic render with no opt-in.
///
/// EPIC-4 SEAM (open/closed): Epic 4 adds AI personalities that override the render. It extends this
/// enum with a NEW member (or a richer personality hook on the options) and the renderer branches to
/// a NEW theme-application path for it. The <see cref="Basic"/> branch is the bedrock and is CLOSED
/// for modification — new themes are new branches, so Epic 4 adds without reworking Story 3.6. The
/// personality ENGINE lives in the <c>Agent</c> project / Epic 4, never in <c>Rendering</c> (the
/// architecture boundary that <c>RenderingPurityTests</c> enforces).
/// </summary>
public enum RenderTheme
{
    /// <summary>The faithful GitHub-light basic theme — the no-personality default render.</summary>
    Basic,
}
