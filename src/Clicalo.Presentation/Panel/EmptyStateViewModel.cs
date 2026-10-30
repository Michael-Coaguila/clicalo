using Clicalo.Domain.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The empty states of the grid (CUA-010): a card with a dashed border and a 44 px accent button. For a profile
/// without shortcuts it shows the <c>inbox</c> icon, [emptyProfT], [emptyProfS] with the profile and «+ [addShortcut]»,
/// which opens the editor with a new shortcut. For Frequents without anything used yet it shows the <c>star</c> icon,
/// [freqEmptyT], [freqEmptyS] and «Volver a {perfil}», which goes back to the profile of the selector.
/// </summary>
public sealed class EmptyStateViewModel : ObservableObject
{
    private readonly IPanelBodyIntents _intents;
    private ProfileId _profile = ProfileId.General;
    private bool _isVisible;
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private string _buttonName = string.Empty;
    private bool _isFrequents;

    internal EmptyStateViewModel(IPanelBodyIntents intents) => _intents = intents;

    /// <summary>Whether the card shows.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>[emptyProfT].</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>[emptyProfS] with the profile.</summary>
    public string Subtitle
    {
        get => _subtitle;
        private set => SetProperty(ref _subtitle, value);
    }

    /// <summary>[addShortcut].</summary>
    public string ButtonName
    {
        get => _buttonName;
        private set => SetProperty(ref _buttonName, value);
    }

    /// <summary>Whether the card is the one of an empty Frequents: its button goes back to the profile.</summary>
    public bool IsFrequents
    {
        get => _isFrequents;
        private set
        {
            if (SetProperty(ref _isFrequents, value))
            {
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(ButtonIcon));
            }
        }
    }

    /// <summary>The Material Symbols icon of the card: <c>inbox</c>, or <c>star</c> for Frequents.</summary>
    public string Icon => _isFrequents ? "star" : "inbox";

    /// <summary>The icon of the button: <c>add</c>, or <c>undo</c> for «Volver a {perfil}».</summary>
    public string ButtonIcon => _isFrequents ? "undo" : "add";

    /// <summary>
    /// The button of the card (a tap or UI Automation Invoke): «+ Añadir atajo» for an empty profile, «Volver a
    /// {perfil}» for an empty Frequents.
    /// </summary>
    public void Add()
    {
        if (_isFrequents)
        {
            _intents.ReturnFromFrequents();
        }
        else
        {
            _intents.AddShortcut(_profile);
        }
    }

    internal void Apply(
        bool visible,
        ProfileId profile,
        string title,
        string subtitle,
        string buttonName,
        bool frequents = false
    )
    {
        _profile = profile;
        IsFrequents = frequents;
        Title = title;
        Subtitle = subtitle;
        ButtonName = buttonName;
        IsVisible = visible;
    }
}
