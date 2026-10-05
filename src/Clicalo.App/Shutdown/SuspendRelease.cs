using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Coordinators;
using Clicalo.Domain.Timing;

namespace Clicalo.App.Shutdown;

/// <summary>
/// The suspend of the machine (<c>PBT_APMSUSPEND</c>; SEG-006, blueprint §7.6): the machine may sleep as soon as the
/// message is answered, so the SysEvents thread waits, bounded by <c>Timings.KeySafety.SuspendReleaseWait</c>, for the
/// engine to report that it holds nothing after <c>Terminal(Suspend)</c>. The wait never needs another thread of the
/// app than the engine's.
/// </summary>
internal static class SuspendRelease
{
    /// <summary>Waits for a quiet engine or for the limit, whichever comes first.</summary>
    /// <param name="relay">The engine's observer.</param>
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "WM_POWERBROADCAST must be answered after the release; the wait is bounded (app-suspend)."
    )]
    public static void Wait(EngineObserverRelay relay)
    {
        ArgumentNullException.ThrowIfNull(relay);
        using var limit = new CancellationTokenSource(
            Timings.KeySafety.SuspendReleaseWait,
            TimeProvider.System
        );
        try
        {
            relay.WhenNothingHeldAsync(limit.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // The limit: Sentinel and the preventive release of the next start still cover what is left (§15.2).
        }
    }
}
