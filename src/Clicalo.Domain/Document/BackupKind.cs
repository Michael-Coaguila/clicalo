namespace Clicalo.Domain.Document;

/// <summary>Kinds of backup and their retention (blueprint §6.8).</summary>
public enum BackupKind
{
    /// <summary>30 s after the last change of a significant slice; the last 12.</summary>
    Auto,

    /// <summary>From the button; no limit.</summary>
    Manual,

    /// <summary>Before installing an update; 10.</summary>
    PreUpdate,

    /// <summary>Before a schema migration; 10.</summary>
    PreMigrate,

    /// <summary>Before restoring a backup; 10.</summary>
    PreRestore,

    /// <summary>Before replacing the library with an import; 10.</summary>
    PreImportReplace,

    /// <summary>Before resetting Frequents; 10.</summary>
    PreResetFrequents,

    /// <summary>Before repairing a document on load; 10.</summary>
    PreRepair,
}
