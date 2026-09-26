namespace Clicalo.Platform.Core.Guardian;

/// <summary>Exit codes of Sentinel, recorded in the crash journal.</summary>
public enum SentinelExitCode
{
    /// <summary>The main process exited with <c>CleanShutdown</c>; nothing to release.</summary>
    CleanExit = 0,

    /// <summary>The main process died; the ledger was released and the app relaunched.</summary>
    ReleasedAndRelaunched = 1,

    /// <summary>The main process died; the ledger was released and the app not relaunched (marks or crash loop).</summary>
    ReleasedWithoutRelaunch = 2,

    /// <summary>The arguments did not follow <see cref="SentinelStartInfo"/>.</summary>
    InvalidArguments = 3,

    /// <summary>The inherited ledger had another magic or layout version.</summary>
    LedgerUnreadable = 4,
}
