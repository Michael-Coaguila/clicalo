namespace Clicalo.UI.Wpf.Automation;

/// <summary>
/// The UI Automation pattern a <see cref="ShortcutTile"/> exposes, from its kind of action (ACC-001, blueprint §8.6).
/// Every tile also has a secondary action (its context menu), reachable without a gesture (CUA-014, UIA007).
/// </summary>
public enum ShortcutTilePattern
{
    /// <summary>Tap shortcuts, text, web, app, mouse and system actions: <c>Invoke</c>.</summary>
    Invoke,

    /// <summary>
    /// Toggle shortcuts, sticky keys (three states) and Hold, whose gesture-free equivalent is a latched toggle
    /// (§8.6): <c>Toggle</c>.
    /// </summary>
    Toggle,

    /// <summary>Tiles that open a menu or a side window (profile button, quick settings): <c>ExpandCollapse</c>.</summary>
    ExpandCollapse,
}
