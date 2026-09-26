namespace Clicalo.Windowing.IntegrationTests.Automation.Lab;

/// <summary>Which tile event a UI Automation call raised.</summary>
public enum LabInvocationKind
{
    /// <summary><see cref="UI.Wpf.Automation.ShortcutTile.Invoked"/>.</summary>
    Invoked,

    /// <summary><see cref="UI.Wpf.Automation.ShortcutTile.Toggled"/>.</summary>
    Toggled,

    /// <summary><see cref="UI.Wpf.Automation.ShortcutTile.ExpandRequested"/>.</summary>
    ExpandRequested,

    /// <summary><see cref="UI.Wpf.Automation.ShortcutTile.CollapseRequested"/>.</summary>
    CollapseRequested,
}
