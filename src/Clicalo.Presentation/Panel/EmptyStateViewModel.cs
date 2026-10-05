using Clicalo.Domain.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The empty profile (CUA-010): a card with a dashed border, the <c>inbox</c> icon, [emptyProfT], [emptyProfS] with the
/// profile and the 44 px accent button «+ [addShortcut]», which opens the editor with a new shortcut.
/// </summary>
public sealed class EmptyStateViewModel : ObservableObject
{
    private readonly IPanelBodyIntents _intents;
    private ProfileId _profile = ProfileId.General;
    private bool _isVisible;
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private string _buttonName = string.Empty;

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

    /// <summary>«+ Añadir atajo» (a tap or UI Automation Invoke).</summary>
    public void Add() => _intents.AddShortcut(_profile);

    internal void Apply(
        bool visible,
        ProfileId profile,
        string title,
        string subtitle,
        string buttonName
    )
    {
        _profile = profile;
        Title = title;
        Subtitle = subtitle;
        ButtonName = buttonName;
        IsVisible = visible;
    }
}
