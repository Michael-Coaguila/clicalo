namespace Clicalo.Platform.Windows.SentinelHost;

/// <summary>
/// A running Sentinel as its supervisor sees it: <see cref="SentinelProcess"/> is the real one; the tests give their
/// own, so the supervisor is tested without starting a guardian.
/// </summary>
internal interface ISentinelChild : IDisposable
{
    /// <summary>The process id.</summary>
    int Id { get; }

    /// <summary>Whether it has ended.</summary>
    bool HasExited { get; }
}
