namespace TheMarkdownWeb.App;

/// <summary>
/// The toolbar action most recently invoked from the shell. Defaults to <see cref="None"/>.
/// </summary>
public enum ShellAction
{
    None,
    Back,
    Forward,
    Reload,
}

/// <summary>
/// Minimal view-model backing the shell toolbar (Story 3-1). The Back/Forward/Reload handlers are
/// inert at this story — there is NO history stack, NO fetch, and NO reload target yet (real
/// navigation lands in Story 3-2 / 3-5). They only record the last action so the wiring is
/// observable/testable without a visible window.
/// </summary>
public sealed class ShellViewModel
{
    /// <summary>Last toolbar action invoked. Defaults to <see cref="ShellAction.None"/>.</summary>
    public ShellAction LastAction { get; private set; } = ShellAction.None;

    /// <summary>Inert Back handler — records the action only.</summary>
    public void OnBack() => LastAction = ShellAction.Back;

    /// <summary>Inert Forward handler — records the action only.</summary>
    public void OnForward() => LastAction = ShellAction.Forward;

    /// <summary>Inert Reload handler — records the action only.</summary>
    public void OnReload() => LastAction = ShellAction.Reload;
}
