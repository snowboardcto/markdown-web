using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App;

/// <summary>
/// The toolbar selection state (Story 4.2 AC3). Drives <see cref="PersonalizationGateway"/> via
/// <c>() =&gt; Current</c> (replacing the 4-1 constant <c>() =&gt; Persona.Basic</c>). <see cref="Current"/>
/// defaults to <see cref="Persona.Basic"/> (no regression: first run = the faithful basic render);
/// <see cref="Select"/> updates it and raises <see cref="SelectionChanged"/> + <c>PropertyChanged</c>.
/// <see cref="Options"/> is the ComboBox <c>ItemsSource</c> (= <see cref="PersonaRegistry.Seed"/>).
/// </summary>
public sealed class PersonalitySelectionViewModel : IPersonalitySelection, INotifyPropertyChanged
{
    private Persona _current = Persona.Basic;

    /// <inheritdoc />
    public event EventHandler? SelectionChanged;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The currently selected persona (default <see cref="Persona.Basic"/>).</summary>
    public Persona Current
    {
        get => _current;
        private set
        {
            if (!ReferenceEquals(_current, value))
            {
                _current = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The selector's item source — the ordered seed registry.</summary>
    public IReadOnlyList<Persona> Options => PersonaRegistry.Seed;

    /// <summary>
    /// Selects <paramref name="persona"/>. A no-op (no event) when it equals the current selection;
    /// otherwise sets <see cref="Current"/> and raises <see cref="SelectionChanged"/> exactly once.
    /// </summary>
    public void Select(Persona persona)
    {
        if (persona is null || persona == _current)
        {
            return;
        }

        Current = persona;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
