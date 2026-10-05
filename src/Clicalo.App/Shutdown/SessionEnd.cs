using System.Diagnostics.CodeAnalysis;
using Clicalo.App.Lifecycle;

namespace Clicalo.App.Shutdown;

/// <summary>
/// The end of the Windows session (sign-out or shut down, <c>WM_QUERYENDSESSION</c>; blueprint §6.4, §7.6): Windows ends
/// the process right after this message is answered, so the exit sequence runs synchronously here, the only place
/// where waiting on a task is allowed (banned-api-exceptions.json: app-shutdown). The sequence never needs the UI
/// thread and is bounded by <c>Timings.App.ExitReleaseWait</c> and <c>ExitFlushTimeout</c>.
/// </summary>
internal static class SessionEnd
{
    /// <summary>Releases everything held and flushes the document and the usage.</summary>
    /// <param name="host">The running instance.</param>
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "WM_QUERYENDSESSION must be answered after the flush; the sequence is bounded and never needs this thread."
    )]
    public static void Flush(AppHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        host.EndSessionAsync().GetAwaiter().GetResult();
    }
}
