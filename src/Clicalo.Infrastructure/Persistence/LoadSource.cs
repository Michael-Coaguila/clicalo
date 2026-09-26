namespace Clicalo.Infrastructure.Persistence;

/// <summary>Which file of the load chain gave the document (blueprint §6.5).</summary>
internal enum LoadSource
{
    /// <summary>Nothing usable on disk: the caller tries the backups, then a default.</summary>
    None,

    /// <summary>The file itself.</summary>
    Main,

    /// <summary>
    /// <c>.tmp</c>: a complete newer version that a crash kept from replacing the file (hash verified), or the new
    /// version when <c>ReplaceFileW</c> stopped half-way.
    /// </summary>
    Temporary,

    /// <summary><c>.prev</c>: the version before the last replace.</summary>
    Previous,

    /// <summary>The emergency copy of <c>pending\</c>, newer than the file (hash verified).</summary>
    Pending,

    /// <summary>A greater major than this version reads: nothing is decoded and nothing is written.</summary>
    FutureMajor,
}
