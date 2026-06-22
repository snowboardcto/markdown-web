using System;
using System.Windows;

namespace TheMarkdownWeb.App;

/// <summary>
/// Seam for writing text to the system clipboard (Story 5.1 AC5). Injectable so the CI tests can
/// assert WHICH text would be written via a fake, without touching the real OS clipboard.
/// Mirrors the <see cref="IUrlLauncher"/> seam pattern established in Story 3.2.
/// </summary>
public interface IClipboard
{
    /// <summary>Writes <paramref name="text"/> to the clipboard. Implementations must never throw into the caller.</summary>
    void SetText(string text);
}

/// <summary>
/// Default <see cref="IClipboard"/>: writes to the WPF <see cref="System.Windows.Clipboard"/> (STA-thread).
/// The WPF Clipboard can transiently fail (<c>COMException</c> / clipboard locked by another process) —
/// the exception is caught and swallowed so a transient clipboard failure never crashes the app.
/// </summary>
public sealed class SystemClipboard : IClipboard
{
    /// <inheritdoc />
    public void SetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // The WPF clipboard can transiently fail (COMException, clipboard locked, etc.).
            // A failed clipboard write must never crash the app (AC5 total behavior).
        }
    }
}
