namespace Clicalo.Platform.Windows.Tray;

/// <summary>One entry of the tray menu (blueprint §8.1: Control Center, Release all, Pause, Exit).</summary>
/// <param name="Id">Command identifier returned by <see cref="TrayMenuHost.ShowMenuAsync"/>; positive.</param>
/// <param name="Text">Localized text, rendered by the caller (never a literal).</param>
/// <param name="IsEnabled">False to show it greyed out.</param>
/// <param name="StartsGroup">True to draw a separator before it, unless it is the first entry.</param>
/// <param name="IsDefault">True for what a click on the icon does: Windows draws it in bold.</param>
public sealed record TrayMenuItem(
    int Id,
    string Text,
    bool IsEnabled = true,
    bool StartsGroup = false,
    bool IsDefault = false
);
