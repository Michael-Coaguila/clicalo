namespace Clicalo.Domain.Migration.V1;

/// <summary>What the report tells about something without a direct equivalent (MIG-004, MIG-006, MIG-007, MIG-008).</summary>
public enum MigrationNoteKind
{
    /// <summary>A <c>#RRGGBB</c> colour mapped to a category.</summary>
    HexColor,

    /// <summary>A separator (not converted, PQ-09).</summary>
    Separator,

    /// <summary><c>pinned_profile</c> (PQ-06).</summary>
    PinnedProfile,

    /// <summary><c>buttons_per_page</c>.</summary>
    ButtonsPerPage,

    /// <summary><c>window_size</c>.</summary>
    WindowSize,

    /// <summary><c>edit_size</c>.</summary>
    EditSize,

    /// <summary>A process bound to several profiles: the first keeps it, the others become manual.</summary>
    DuplicateProcess,

    /// <summary>One of the 8 shortcuts that never worked in v1, migrated with the meaning of its name (PQ-40).</summary>
    NeverWorkedInV1,

    /// <summary>A token without catalog equivalent («Revisar», MIG-005).</summary>
    UnresolvedToken,

    /// <summary><c>win+l</c> became the «Lock computer» system action (MIG-007).</summary>
    LockBecameSystemAction,

    /// <summary><c>ctrl+shift+esc</c> kept with the special combination warning (MIG-007).</summary>
    SpecialCombination,

    /// <summary>An address that is not http or https («Revisar»).</summary>
    NonWebAddress,

    /// <summary>A command with interpreter arguments, kept but never run («Revisar»).</summary>
    InterpreterCommand,

    /// <summary>The window position was outside every monitor and was moved to the primary one.</summary>
    PositionMoved,

    /// <summary>The button height mapped to a size; 40 is below the touch minimum (MIG-006).</summary>
    SizeChanged,

    /// <summary>The opacity was rounded to the 0.05 grid (MIG-006).</summary>
    OpacityRounded,

    /// <summary>A repeated combination created by the import, added to «It's fine» (MIG-008).</summary>
    DuplicateIgnored,

    /// <summary>General did not exist and was created empty.</summary>
    GeneralCreated,

    /// <summary>General had a process in v1 (EC-MIG-04).</summary>
    GeneralHadProcess,

    /// <summary>A button of an unknown type.</summary>
    UnknownButton,

    /// <summary>A top-level key the schema does not know; it stays in the byte-for-byte copy.</summary>
    UnknownKey,

    /// <summary>
    /// <c>active_profile</c> named a profile that does not exist; General is shown instead (EC-MIG-03).
    /// </summary>
    ActiveProfileMissing,

    /// <summary>
    /// v1 kept the last profile for apps without one; Clícalo goes back to General in Auto (MIG-006).
    /// </summary>
    ReturnsToGeneral,

    /// <summary>A button without keys, address or command: imported incomplete, marked «Revisar».</summary>
    MissingAction,
}
