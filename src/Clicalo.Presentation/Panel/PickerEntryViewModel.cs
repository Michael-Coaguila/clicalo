using Clicalo.Domain.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// One tile of the profile grid (SEL-003): icon and name; accentWash and a 2 px accent border for the profile in view;
/// a 9 px dot for the profile of the active app. A tap chooses it (SEL-004).
/// </summary>
public sealed class PickerEntryViewModel : ObservableObject
{
    private readonly Action<ProfileId> _choose;
    private string _name;
    private string _icon;
    private bool _isCurrent;
    private bool _isActiveApp;

    internal PickerEntryViewModel(PickerEntry entry, Action<ProfileId> choose)
    {
        Id = entry.Id;
        _name = entry.Name;
        _icon = entry.Icon.Name;
        _choose = choose;
    }

    /// <summary>The profile.</summary>
    public ProfileId Id { get; }

    /// <summary>Its name (user data, shown as is).</summary>
    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    /// <summary>Its Material Symbols icon.</summary>
    public string Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    /// <summary>Whether it is the profile in view.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        private set => SetProperty(ref _isCurrent, value);
    }

    /// <summary>Whether it is the profile of the active app (its dot).</summary>
    public bool IsActiveApp
    {
        get => _isActiveApp;
        private set => SetProperty(ref _isActiveApp, value);
    }

    /// <summary>A tap or UI Automation SelectionItem.Select.</summary>
    public void Choose() => _choose(Id);

    internal void Apply(PickerEntry entry, bool current, bool activeApp)
    {
        Name = entry.Name;
        Icon = entry.Icon.Name;
        IsCurrent = current;
        IsActiveApp = activeApp;
    }
}
