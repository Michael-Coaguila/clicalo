using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel.QuickSettings;

/// <summary>
/// One switch row of Quick settings (AJR-001 item 7: Modo prueba (30 s), [autoDim], [stickyMods] and [voiceNums]):
/// the whole row toggles, and UI Automation sees the Toggle pattern named with the label.
/// </summary>
public sealed class QuickSwitchViewModel : ObservableObject
{
    private readonly Action _toggle;
    private string _label = string.Empty;
    private bool _isOn;

    internal QuickSwitchViewModel(string icon, Action toggle)
    {
        Icon = icon;
        _toggle = toggle;
    }

    /// <summary>Its Material Symbols icon, in muted.</summary>
    public string Icon { get; }

    /// <summary>Its label, also its accessible name.</summary>
    public string Label
    {
        get => _label;
        internal set => SetProperty(ref _label, value);
    }

    /// <summary>Whether it is on.</summary>
    public bool IsOn
    {
        get => _isOn;
        internal set => SetProperty(ref _isOn, value);
    }

    /// <summary>A tap anywhere on the row, or the UI Automation Toggle.</summary>
    public void Toggle() => _toggle();
}
