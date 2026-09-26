namespace Clicalo.Infrastructure.Persistence;

/// <summary>What an I/O error means for the write protocol (blueprint §6.5, S11).</summary>
internal enum IoFailureKind
{
    /// <summary>Another process holds the file: an antivirus, the indexer, a sync client or a monitor (transient).</summary>
    Locked,

    /// <summary><c>ACCESS_DENIED</c>, often transient (a file pending deletion, a scan in progress).</summary>
    Denied,

    /// <summary>The file vanished between two steps (transient: the next attempt takes the other branch).</summary>
    Missing,

    /// <summary>The disk is full (persistent).</summary>
    DiskFull,

    /// <summary>Any other error (persistent).</summary>
    Other,
}
