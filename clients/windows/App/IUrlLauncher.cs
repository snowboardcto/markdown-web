using System;
using System.Diagnostics;

namespace TheMarkdownWeb.App;

/// <summary>
/// Seam for opening a URL in the OS default browser (Story 3-2 AC3). Injectable so the decline test can
/// assert WHICH URL would be launched via a fake, without spawning a process. This is explicitly NOT an
/// in-app/embedded browser — it hands the URL to the operating system (guards NFR-1 / architecture FC-1).
/// </summary>
public interface IUrlLauncher
{
    /// <summary>Opens <paramref name="url"/> in the system default browser.</summary>
    void Open(Uri url);
}

/// <summary>
/// Default <see cref="IUrlLauncher"/>: shell-executes the URL so the OS opens it in the user's default
/// browser (<c>Process.Start</c> with <c>UseShellExecute = true</c>). An un-launchable URL is swallowed
/// so it never crashes the app (Story 3-2 AC3). No embedded browser engine is involved.
/// </summary>
public sealed class SystemBrowserLauncher : IUrlLauncher
{
    /// <inheritdoc />
    public void Open(Uri url)
    {
        if (url is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });
        }
        catch
        {
            // An un-launchable URL (no handler, blocked, etc.) must never crash the app.
        }
    }
}
