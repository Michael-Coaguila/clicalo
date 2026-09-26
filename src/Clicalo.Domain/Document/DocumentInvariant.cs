namespace Clicalo.Domain.Document;

/// <summary>The invariants of <see cref="UserDocument"/> (blueprint §6.2, DAT-004, DAT-005).</summary>
public enum DocumentInvariant
{
    /// <summary>I1: ids are unique in the whole document.</summary>
    UniqueIds,

    /// <summary>I2: a shortcut lives in one list.</summary>
    SingleList,

    /// <summary>I3: General and Always visible exist.</summary>
    FixedListsExist,

    /// <summary>I4: General has no process.</summary>
    GeneralUnbound,

    /// <summary>I5: a process belongs to one profile.</summary>
    ProcessOwnedOnce,

    /// <summary>I6: macro steps are valid and waits in range.</summary>
    MacroStepsValid,

    /// <summary>The last profile, when set, exists (PER-008).</summary>
    LastProfileExists,

    /// <summary>Every setting is inside its range (<see cref="Settings.SettingsSchema"/>).</summary>
    SettingsInRange,

    /// <summary>Revision and usage epoch are not negative.</summary>
    CountersNotNegative,
}
