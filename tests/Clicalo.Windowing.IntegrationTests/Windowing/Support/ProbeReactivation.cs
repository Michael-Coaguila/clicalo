using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// When InputProbe, forced to give the foreground to a surface, got the activation back: the restore measured by the
/// probe itself, for the negative tests of <c>ActivationGuard</c>.
/// </summary>
/// <remarks>
/// The probe only gets a <c>WM_ACTIVATE</c> that activates it after it has lost the activation, so the first one after
/// a cursor taken while it was in front is the restore. Its deactivation is no anchor: spike S1 in CI (s0 37164843497,
/// run 9) saw Windows hand the foreground to the panel and back without ever deactivating the probe's thread
/// (<c>WM_ACTIVATEAPP(TRUE)</c>, <c>WM_ACTIVATE</c> and <c>WM_SETFOCUS</c> arrived, no <c>WA_INACTIVE</c> before them),
/// and the old condition, a deactivation followed by an activation, waited for an event that never comes. The loss is
/// therefore counted from the moment the test asks the probe to force the activation, which is never later than the
/// real loss: the measured time can only be longer than the real one, never shorter.
/// </remarks>
public static class ProbeReactivation
{
    /// <summary>True when the events after the cursor hold a <c>WM_ACTIVATE</c> that activates the probe.</summary>
    public static bool Reactivated(IReadOnlyList<ProbeEvent> events) =>
        events.OfType<ActivateEvent>().Any(IsActivation);

    /// <summary>
    /// From <paramref name="requestedAt"/> (<c>QueryPerformanceCounter</c> ticks, taken by the test just before asking
    /// for the forced activation) to the probe's first activation in <paramref name="events"/>.
    /// </summary>
    public static TimeSpan RestoredAfter(
        IReadOnlyList<ProbeEvent> events,
        long requestedAt,
        double frequency
    )
    {
        var back = events.OfType<ActivateEvent>().First(IsActivation);
        return TimeSpan.FromSeconds((back.Timestamp - requestedAt) / frequency);
    }

    private static bool IsActivation(ActivateEvent activate) =>
        activate.State != ActivationState.Inactive;
}
