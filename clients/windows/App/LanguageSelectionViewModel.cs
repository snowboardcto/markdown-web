using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TheMarkdownWeb.App;

/// <summary>
/// The target-language selection state (Story 4.4 AC2 — Q-Lang-Source). Drives
/// <see cref="PersonalizationGateway"/> via <c>() =&gt; Current</c> (the gateway sources it into
/// <c>ReaderContext.PreferredLanguage</c> per resolve). <see cref="Current"/> defaults to <c>null</c> (the
/// "choose a language" state — a language-less Translate is a safe pass-through-of-original); the chosen
/// string is passed VERBATIM (never normalized/validated) so any language label or code works.
/// </summary>
public sealed class LanguageSelectionViewModel : INotifyPropertyChanged
{
    private string? _current;

    /// <summary>Raised when the selected language changes (the App re-renders in place on this).</summary>
    public event EventHandler? SelectionChanged;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The currently selected target language, or <c>null</c>/blank for "none chosen".</summary>
    public string? Current
    {
        get => _current;
        private set
        {
            if (!string.Equals(_current, value, StringComparison.Ordinal))
            {
                _current = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Selects <paramref name="language"/> (blank → treated as none). A no-op (no event) when it equals
    /// the current selection; otherwise sets <see cref="Current"/> and raises <see cref="SelectionChanged"/>.
    /// </summary>
    public void Select(string? language)
    {
        string? normalized = string.IsNullOrWhiteSpace(language) ? null : language.Trim();
        if (string.Equals(normalized, _current, StringComparison.Ordinal))
        {
            return;
        }

        Current = normalized;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
