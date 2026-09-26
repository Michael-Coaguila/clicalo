namespace Clicalo.Domain.Library;

/// <summary>The invariants of <see cref="ShortcutLibrary"/> (blueprint §6.2, DAT-004, DAT-005).</summary>
public enum LibraryInvariant
{
    /// <summary>I1: every profile and shortcut id is present and unique in the whole library.</summary>
    UniqueIds,

    /// <summary>I2: a shortcut lives in one list.</summary>
    SingleList,

    /// <summary>I3: General exists (Always visible exists by construction).</summary>
    FixedListsExist,

    /// <summary>I4: General has no process.</summary>
    GeneralUnbound,

    /// <summary>I5: a process is not empty and belongs to one profile, without distinguishing case.</summary>
    ProcessOwnedOnce,

    /// <summary>I6: macro steps have a valid type and waits are in range.</summary>
    MacroStepsValid,
}
