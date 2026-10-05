using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// When InputProbe, forced to give the foreground to a surface, got it back: the restore measured on the probe's
/// clock, for the negative tests of <c>ActivationGuard</c> (blueprint §3.5: the foreground is back on InputProbe within
/// <c>Timings.Windowing.ViolationRestoreBudget</c>).
/// </summary>
/// <remarks>
/// <para>
/// The probe is back when it handles an activation message (<c>WM_ACTIVATEAPP(TRUE)</c>, <c>WM_NCACTIVATE(TRUE)</c> or
/// a <c>WM_ACTIVATE</c> that activates it) while <c>GetForegroundWindow</c> names its window, after the test asked for
/// the forced activation. The probe only gets one after it has lost the activation, so the first one after the request
/// is the restore; one handled before the request (a late message of the activation that put the probe in front) never
/// counts, or the measured time could be shorter than the real one.
/// </para>
/// <para>
/// Spike S1 in CI showed why neither end can be a fixed message. Windows can hand the foreground to the panel and back
/// without ever deactivating the probe's thread (s0 37164843497, run 9: <c>WM_ACTIVATEAPP(TRUE)</c>,
/// <c>WM_ACTIVATE</c> and <c>WM_SETFOCUS</c>, no <c>WA_INACTIVE</c> before them), so the loss is counted from the moment
/// the test asks for the forced activation, which is never later than the real loss: the measured time can only be
/// longer than the real one. And when the restore lands while the probe is still inside its own
/// <c>SetForegroundWindow</c> on the panel, Windows activates the probe again (<c>WM_NCACTIVATE(TRUE)</c> with the probe
/// in front) and only then delivers the rest of the deactivation that call started (<c>WM_ACTIVATEAPP(FALSE)</c>,
/// <c>WM_KILLFOCUS</c>): the probe is the foreground window, but its thread ends with no active and no focus window and
/// never gets a <c>WM_ACTIVATE</c> (s0 37171041134, three of 600 iterations). That is the forcing app's own call
/// finishing after the restore; the panel never had the focus (its <c>WM_ACTIVATE</c> is kept from WPF).
/// </para>
/// </remarks>
public static class ProbeReactivation
{
    /// <summary>
    /// True when the events after the cursor hold an activation of <paramref name="probeWindow"/> in front handled after
    /// <paramref name="requestedAt"/> (<c>QueryPerformanceCounter</c> ticks).
    /// </summary>
    public static bool Reactivated(
        IReadOnlyList<ProbeEvent> events,
        nint probeWindow,
        long requestedAt
    ) => events.Any(received => IsBack(received, probeWindow, requestedAt));

    /// <summary>
    /// From <paramref name="requestedAt"/> (<c>QueryPerformanceCounter</c> ticks, taken by the test just before asking
    /// for the forced activation) to the probe's first activation in front in <paramref name="events"/>.
    /// </summary>
    public static TimeSpan RestoredAfter(
        IReadOnlyList<ProbeEvent> events,
        nint probeWindow,
        long requestedAt,
        double frequency
    )
    {
        var back = events.First(received => IsBack(received, probeWindow, requestedAt));
        return TimeSpan.FromSeconds((back.Timestamp - requestedAt) / frequency);
    }

    private static bool IsBack(ProbeEvent received, nint probeWindow, long requestedAt) =>
        received.Timestamp > requestedAt
        && received.ForegroundWindow == probeWindow
        && received switch
        {
            ActivateEvent activate => activate.State != ActivationState.Inactive,
            AppActivateEvent application => application.IsActive,
            WindowMessageEvent message => string.Equals(
                message.MessageName,
                "WM_NCACTIVATE",
                StringComparison.Ordinal
            )
                && message.WParam != 0,
            _ => false,
        };
}
