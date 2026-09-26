namespace Clicalo.Platform.Windows.Tray;

/// <summary>One entry of the tray menu (blueprint §8.1: Control Center, Release all, Pause, Exit).</summary>
/// <param name="Id">Command identifier returned by <see cref="TrayMenuHost.ShowMenuAsync"/>; positive.</param>
/// <param name="Text">Localized text, rendered by the caller (never a literal).</param>
/// <param name="IsEnabled">False to show it greyed out.</param>
public sealed record TrayMenuItem(int Id, string Text, bool IsEnabled = true);
