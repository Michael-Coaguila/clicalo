using System.Globalization;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// UIA009, the behavioral rule: after a UI Automation call on a surface, the foreground application (InputProbe) is
/// still in front and was never deactivated, the surface is not active and <c>ActivationGuard</c> counted nothing
/// (REG-01, S3).
/// </summary>
public static class ForegroundInvariant
{
    /// <summary>Rule id.</summary>
    public const string RuleId = "UIA009";

    /// <summary>The violations after <paramref name="action"/>.</summary>
    /// <param name="action">What the test did, for the messages.</param>
    /// <param name="foregroundWindow">The window that must still own the foreground (InputProbe).</param>
    /// <param name="surfaceWindow">The surface acted on.</param>
    /// <param name="surfaceIsActive">WPF's <c>Window.IsActive</c> of the surface after the action.</param>
    /// <param name="probeEvents">What InputProbe recorded since before the action.</param>
    /// <param name="guardViolations"><c>reg01.violations</c> of the surface's guard.</param>
    /// <param name="reportedViolations">Violations the arbiter received.</param>
    public static IReadOnlyList<UiaViolation> Check(
        string action,
        nint foregroundWindow,
        nint surfaceWindow,
        bool surfaceIsActive,
        IEnumerable<ProbeEvent> probeEvents,
        long guardViolations,
        int reportedViolations
    )
    {
        ArgumentNullException.ThrowIfNull(probeEvents);
        var violations = new List<UiaViolation>();
        var current = ForegroundWindows.Current;
        if (current != foregroundWindow)
        {
            violations.Add(
                new UiaViolation(
                    RuleId,
                    action,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"the foreground moved from 0x{foregroundWindow:X} to 0x{current:X}{(current == surfaceWindow ? " (the surface)" : string.Empty)}: {ForegroundWindows.Describe()}."
                    )
                )
            );
        }

        if (surfaceIsActive)
        {
            violations.Add(new UiaViolation(RuleId, action, "the surface became active."));
        }

        foreach (var probeEvent in probeEvents)
        {
            var problem = probeEvent switch
            {
                ActivateEvent activate => $"InputProbe received WM_ACTIVATE ({activate.State}).",
                FocusEvent { IsGained: false } =>
                    "InputProbe lost the keyboard focus (WM_KILLFOCUS).",
                AppActivateEvent { IsActive: false } =>
                    "InputProbe was deactivated (WM_ACTIVATEAPP).",
                _ => null,
            };
            if (problem is not null)
            {
                violations.Add(new UiaViolation(RuleId, action, problem));
            }
        }

        if (guardViolations != 0 || reportedViolations != 0)
        {
            violations.Add(
                new UiaViolation(
                    RuleId,
                    action,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"reg01.violations = {guardViolations}, reported to the arbiter: {reportedViolations}."
                    )
                )
            );
        }

        return violations;
    }
}
