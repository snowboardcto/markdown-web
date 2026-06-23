using System;
using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.App;

/// <summary>
/// Pure source of the canonical home <see cref="Uri"/> and a single navigate-home seam used by BOTH
/// the launch hook and the Home toolbar button (Story 6.1 AC1/AC2/AC3). Keeping the two callers on one
/// seam guarantees they can never drift to different targets.
///
/// Pure — no I/O, no statics-with-state — so it is <c>[Fact]</c>-testable with no window and no socket.
/// </summary>
public static class HomeNavigator
{
    /// <summary>
    /// The canonical home <see cref="Uri"/>: <c>https://themarkdownweb.com/</c>.
    /// <see cref="PageEndpointResolver.IsAppHost"/> returns <c>true</c> for this URI, so the existing
    /// <c>/api/negotiate/&lt;slug&gt;</c> mapping serves the home page without any new host constant.
    /// </summary>
    public static Uri HomeUrl { get; } = new Uri("https://themarkdownweb.com/");

    /// <summary>
    /// Navigates <paramref name="controller"/> to <see cref="HomeUrl"/> via
    /// <see cref="NavigationController.NavigateToAsync"/>. The SAME seam is called on launch (fire-and-
    /// forget from <c>MainWindow.Loaded</c>) and from the Home toolbar button handler. Total — never
    /// throws (the controller is total).
    /// </summary>
    public static Task NavigateHomeAsync(NavigationController controller, CancellationToken ct = default)
    {
        if (controller is null)
        {
            throw new ArgumentNullException(nameof(controller));
        }

        return controller.NavigateToAsync(HomeUrl, ct);
    }
}
