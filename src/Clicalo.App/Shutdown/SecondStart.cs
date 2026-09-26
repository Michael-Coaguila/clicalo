using System.Diagnostics.CodeAnalysis;
using Clicalo.App.SingleInstance;

namespace Clicalo.App.Shutdown;

/// <summary>
/// A second start of Clícalo in the same session (SIS-003): it asks the running instance to show its panel and ends
/// at once, without a window, an engine or a key sent. Waiting for the answer is synchronous because <c>Main</c> is an
/// STA entry point; it has no synchronization context, so the wait cannot deadlock, and it is bounded by
/// <c>Timings.Ipc.IpcRequestTimeout</c>.
/// </summary>
internal static class SecondStart
{
    /// <summary>Asks the running instance of <paramref name="identity"/> to show its panel.</summary>
    /// <param name="identity">The names of the instance.</param>
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "The second process only waits for one bounded answer before it ends; Main cannot await."
    )]
    public static AppExitCode ShowRunningInstance(InstanceIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var ownImage =
            Environment.ProcessPath
            ?? throw new InvalidOperationException("The path of Clicalo.exe is unknown.");
        var outcome = ShowPipeClient
            .RequestShowAsync(identity, ownImage, TimeProvider.System, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return outcome switch
        {
            ShowOutcome.Shown => AppExitCode.Ok,
            ShowOutcome.Squatted => AppExitCode.InstanceSquatted,
            _ => AppExitCode.InstanceUnreachable,
        };
    }
}
