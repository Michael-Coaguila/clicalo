namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>How the texts of Text actions and text steps are written (LOG-003, DAT-007).</summary>
internal enum TextExport
{
    /// <summary>Encrypted with DPAPI: the document, its backups and a shared profile by default.</summary>
    Encrypt,

    /// <summary>In clear: a shared profile when the user chooses to include the texts.</summary>
    Plain,

    /// <summary>Left out, marked as excluded: a shared profile by default (imported as unavailable).</summary>
    Exclude,
}
