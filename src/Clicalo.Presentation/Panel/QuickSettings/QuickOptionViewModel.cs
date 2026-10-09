using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel.QuickSettings;

/// <summary>
/// One option of a segmented group of Quick settings (AJR-001: Vista, Tamaño, Lado de la pestaña, Tema): the chosen
/// one has the accent fill and the accessible selected state (ACC-003).
/// </summary>
public sealed class QuickOptionViewModel : ObservableObject
{
    private readonly Action _select;
    private string _label = string.Empty;
    private string _accessibleName = string.Empty;
    private bool _isSelected;

    internal QuickOptionViewModel(object value, string icon, Action select)
    {
        Value = value;
        Icon = icon;
        _select = select;
    }

    /// <summary>The setting value it stands for (a <c>PanelDensity</c>, <c>PanelSize</c>, <c>DockSide</c> or <c>ThemeChoice</c>).</summary>
    public object Value { get; }

    /// <summary>Its Material Symbols icon, or empty for a text-only option.</summary>
    public string Icon { get; }

    /// <summary>What it shows: a word, a letter (S, M, L) or nothing for an icon-only side.</summary>
    public string Label
    {
        get => _label;
        internal set => SetProperty(ref _label, value);
    }

    /// <summary>Its accessible name (the full word: «Pequeño», «Izquierda»…).</summary>
    public string AccessibleName
    {
        get => _accessibleName;
        internal set => SetProperty(ref _accessibleName, value);
    }

    /// <summary>Whether it is the current value.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    /// <summary>A tap, or the UI Automation SelectionItem.Select.</summary>
    public void Select() => _select();
}
