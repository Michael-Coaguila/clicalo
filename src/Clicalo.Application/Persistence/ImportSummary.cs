namespace Clicalo.Application.Persistence;

/// <summary>What an import plan does, for its notice and its undo label (COP-002).</summary>
/// <param name="ProfilesAdded">Profiles added.</param>
/// <param name="ShortcutsAdded">Shortcuts added, renamed ones included.</param>
/// <param name="ShortcutsRenamed">Imported shortcuts whose id was taken by a different one: added with a new id, never lost.</param>
/// <param name="ShortcutsKept">Imported shortcuts identical to one already there: the existing one wins.</param>
/// <param name="BindingsDropped">Processes already bound to another profile (I5): the existing binding wins.</param>
public sealed record ImportSummary(
    int ProfilesAdded,
    int ShortcutsAdded,
    int ShortcutsRenamed,
    int ShortcutsKept,
    int BindingsDropped
);
