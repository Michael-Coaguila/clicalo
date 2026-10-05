using System.Collections.ObjectModel;
using Clicalo.Domain.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The profile grid (SEL-003): a tile per profile in the stored order, «Crear para {app}» when the active app has no
/// profile and a template exists, «+ Más» toward Templates, and the legend of the dot.
/// </summary>
public sealed class PickerGridViewModel : ObservableObject
{
    private readonly IPanelBodyIntents _intents;
    private bool _isVisible;
    private bool _hasSuggestion;
    private string _suggestionName = string.Empty;
    private string _moreName = string.Empty;
    private string _legend = string.Empty;

    internal PickerGridViewModel(IPanelBodyIntents intents) => _intents = intents;

    /// <summary>A tile per profile, in the stored order.</summary>
    public ObservableCollection<PickerEntryViewModel> Entries { get; } = [];

    /// <summary>Whether the grid shows.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>Whether «Crear para {app}» shows.</summary>
    public bool HasSuggestion
    {
        get => _hasSuggestion;
        private set => SetProperty(ref _hasSuggestion, value);
    }

    /// <summary>«Crear para {app}».</summary>
    public string SuggestionName
    {
        get => _suggestionName;
        private set => SetProperty(ref _suggestionName, value);
    }

    /// <summary>[morePf].</summary>
    public string MoreName
    {
        get => _moreName;
        private set => SetProperty(ref _moreName, value);
    }

    /// <summary>[activeLegend].</summary>
    public string Legend
    {
        get => _legend;
        private set => SetProperty(ref _legend, value);
    }

    /// <summary>«Crear para {app}» (a tap or UI Automation Invoke).</summary>
    public void CreateSuggested() => _intents.CreateProfileForActiveApp();

    /// <summary>«+ Más» (a tap or UI Automation Invoke): Templates.</summary>
    public void More() => _intents.OpenTemplates();

    internal void Apply(
        IReadOnlyList<PickerEntry> entries,
        bool visible,
        ProfileId? current,
        ProfileId? activeApp,
        string? suggestionName,
        string moreName,
        string legend,
        string currentWord,
        string activeAppWord
    )
    {
        var same =
            Entries.Count == entries.Count
            && Entries.Select(static e => e.Id).SequenceEqual(entries.Select(static e => e.Id));
        if (!same)
        {
            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(new PickerEntryViewModel(entry, _intents.ChooseProfile));
            }
        }

        for (var i = 0; i < entries.Count; i++)
        {
            Entries[i]
                .Apply(
                    entries[i],
                    entries[i].Id == current,
                    entries[i].Id == activeApp,
                    currentWord,
                    activeAppWord
                );
        }

        HasSuggestion = suggestionName is not null;
        SuggestionName = suggestionName ?? string.Empty;
        MoreName = moreName;
        Legend = legend;
        IsVisible = visible;
    }
}
