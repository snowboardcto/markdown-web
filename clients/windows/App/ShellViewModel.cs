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
/// View-model backing the shell toolbar. The Back/Forward/Reload handlers record the last action
/// (Story 3.1 contract — kept green) AND, when constructed with a <see cref="NavigationController"/>
/// (Story 3.5), delegate to real history navigation. The 3.1 parameterless ctor remains inert.
/// </summary>
public sealed class ShellViewModel
{
    private readonly NavigationController? _controller;

    /// <summary>Story 3.1 ctor — inert, records <see cref="LastAction"/> only.</summary>
    public ShellViewModel()
    {
    }

    /// <summary>Story 3.5 ctor — delegates Back/Forward/Reload to the real history controller.</summary>
    public ShellViewModel(NavigationController controller)
    {
        _controller = controller;
    }

    /// <summary>Last toolbar action invoked. Defaults to <see cref="ShellAction.None"/>.</summary>
    public ShellAction LastAction { get; private set; } = ShellAction.None;

    /// <summary>Records Back and (if wired) drives the controller's Back navigation.</summary>
    public void OnBack()
    {
        LastAction = ShellAction.Back;
        if (_controller is not null)
        {
            _ = _controller.GoBackAsync();
        }
    }

    /// <summary>Records Forward and (if wired) drives the controller's Forward navigation.</summary>
    public void OnForward()
    {
        LastAction = ShellAction.Forward;
        if (_controller is not null)
        {
            _ = _controller.GoForwardAsync();
        }
    }

    /// <summary>Records Reload and (if wired) re-fetches the current page in place.</summary>
    public void OnReload()
    {
        LastAction = ShellAction.Reload;
        if (_controller is not null)
        {
            _ = _controller.ReloadAsync();
        }
    }
}
