using Clicalo.Platform.Core.Guardian;

namespace Clicalo.Platform.Windows.SentinelHost;

/// <summary>
/// The real <c>Clicalo.Sentinel.exe</c>, started with exactly the handles of its <see cref="SentinelStartInfo"/>
/// (<c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c>).
/// </summary>
internal sealed class SentinelProcess : ISentinelChild
{
    private readonly GuardianProcess _process;

    private SentinelProcess(GuardianProcess process) => _process = process;

    /// <inheritdoc />
    public int Id => _process.Id;

    /// <inheritdoc />
    public bool HasExited => _process.HasExited;

    /// <summary>Starts Sentinel; throws <see cref="System.ComponentModel.Win32Exception"/> when it cannot.</summary>
    /// <param name="path">Full path of <c>Clicalo.Sentinel.exe</c>.</param>
    /// <param name="info">The start-up contract.</param>
    public static SentinelProcess Start(string path, SentinelStartInfo info) =>
        new(GuardianProcess.Start(path, info.ToArguments(), info.InheritedHandles.AsSpan()));

    /// <inheritdoc />
    public void Dispose() => _process.Dispose();
}
