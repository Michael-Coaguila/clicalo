using Clicalo.Domain.Messages;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>One entry of the tray menu before it is formatted in the interface language.</summary>
/// <param name="Command">What it does.</param>
/// <param name="Text">Its text, a key of <c>data/i18n</c>.</param>
/// <param name="IsEnabled">False to show it greyed out.</param>
/// <param name="StartsGroup">True when a separator goes before it.</param>
/// <param name="IsDefault">True for what a click on the icon does (BUR-003).</param>
public sealed record TrayMenuEntry(
    TrayCommand Command,
    Message Text,
    bool IsEnabled = true,
    bool StartsGroup = false,
    bool IsDefault = false
);
