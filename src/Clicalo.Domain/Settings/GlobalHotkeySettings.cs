namespace Clicalo.Domain.Settings;

/// <summary>
/// The optional global shortcut that shows or hides the panel (BUR-005, user decision D10, ADR-0028): off by default,
/// with a combination chosen from the closed list <see cref="GlobalHotkeys"/>.
/// </summary>
/// <param name="Enabled">Whether it is registered; <see langword="false"/> by default.</param>
/// <param name="Combo">The <see cref="GlobalHotkey.Id"/> of the chosen combination, kept while it is off.</param>
public sealed record GlobalHotkeySettings(bool Enabled, string Combo);
