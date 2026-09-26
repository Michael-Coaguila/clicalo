namespace Clicalo.Domain.Library;

/// <summary>A shortcut and where it lives, as listed by <see cref="ShortcutLibrary.EnumerateShortcuts"/>.</summary>
/// <param name="Shortcut">The shortcut.</param>
/// <param name="Location">Its list and position.</param>
public sealed record LocatedShortcut(Shortcut Shortcut, ShortcutLocation Location);
