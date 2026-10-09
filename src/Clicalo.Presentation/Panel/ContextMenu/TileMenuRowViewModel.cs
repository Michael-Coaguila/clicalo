using Clicalo.Domain.Frequents;

namespace Clicalo.Presentation.Panel.ContextMenu;

/// <summary>A 44 px row of the context menu of a tile (CUA-014): its icon, its text and what a tap on it does.</summary>
public sealed class TileMenuRowViewModel
{
    private readonly Action _activate;

    internal TileMenuRowViewModel(TileMenuItem item, string icon, string label, Action activate)
    {
        Item = item;
        Icon = icon;
        Label = label;
        _activate = activate;
    }

    /// <summary>Which row it is.</summary>
    public TileMenuItem Item { get; }

    /// <summary>The Material Symbols icon: <c>keep</c>, <c>keep_off</c>, <c>visibility_off</c>, <c>edit</c> or <c>close</c>.</summary>
    public string Icon { get; }

    /// <summary>The text, also its accessible name.</summary>
    public string Label { get; }

    /// <summary>A tap on the row, or its UI Automation Invoke.</summary>
    public void Activate() => _activate();
}
