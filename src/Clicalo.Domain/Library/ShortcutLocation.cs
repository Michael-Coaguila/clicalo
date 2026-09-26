namespace Clicalo.Domain.Library;

/// <summary>Where a shortcut lives.</summary>
/// <param name="List">Its list.</param>
/// <param name="Index">Its zero-based position in the list.</param>
public readonly record struct ShortcutLocation(ListRef List, int Index);
