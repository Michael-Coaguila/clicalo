namespace Clicalo.Application.Ports;

/// <summary>How the document was obtained at start-up (blueprint §6.5, DAT-003, SIS-004).</summary>
public enum DocumentLoadOutcome
{
    /// <summary>No document yet: a new installation.</summary>
    FirstRun,

    /// <summary>Read and valid.</summary>
    Loaded,

    /// <summary>Valid but its hash did not match: accepted, logged as <c>doc.edited_externally</c>.</summary>
    EditedExternally,

    /// <summary>Repaired (duplicate id, dangling reference); a <c>pre-repair</c> backup was taken.</summary>
    Repaired,

    /// <summary>A future major: read-only, never written; the <c>pre-update</c> backup is offered.</summary>
    FutureMajorReadOnly,

    /// <summary>Unreadable: moved to quarantine and <c>clicalo.json.prev</c> used.</summary>
    RecoveredFromPrevious,

    /// <summary>Unreadable, and so was <c>.prev</c>: the newest valid backup used.</summary>
    RecoveredFromBackup,

    /// <summary>Nothing usable: a default document in memory, not written until the user accepts.</summary>
    DefaultInMemory,
}
