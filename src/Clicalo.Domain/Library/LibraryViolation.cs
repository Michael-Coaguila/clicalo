namespace Clicalo.Domain.Library;

/// <summary>A broken invariant of a <see cref="ShortcutLibrary"/>, found when it is validated.</summary>
/// <param name="Invariant">Which one.</param>
/// <param name="Detail">Ids involved; never user text (LOG-001).</param>
public sealed record LibraryViolation(LibraryInvariant Invariant, string Detail);
