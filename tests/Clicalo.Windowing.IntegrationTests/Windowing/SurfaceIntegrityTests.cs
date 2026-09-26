using System.Globalization;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// Spike S1: on the visible panel, with InputProbe in front, <c>WS_EX_NOACTIVATE</c> or the topmost band is taken away
/// from outside the product, and <c>SurfaceIntegrityCheck.CheckNow</c> puts it back with <c>SWP_NOACTIVATE</c>,
/// without activating anything. 20 cycles, alternating the two drifts.
/// </summary>
/// <remarks>
/// Taking one surface out of the topmost band takes every surface out of it: they share the owner anchor, and
/// <c>SetWindowPos</c> makes the owners and owned windows of a window non-topmost with it (first local run of S1). So
/// the expected number of repairs is measured from outside, just before the check.
/// </remarks>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
public sealed class SurfaceIntegrityTests(SurfaceDesktopFixture desktop)
    : IClassFixture<SurfaceDesktopFixture>
{
    private const int Cycles = 20;

    [DesktopFact]
    public async Task A_removed_style_is_repaired_without_activating()
    {
        var panel = desktop.Panel;
        var window = panel.Handle;
        var surfaces = desktop
            .Lab.Registry.Surfaces.Select(surface => surface.SurfaceWindow.Handle)
            .ToList();
        var cursor = await desktop.PrepareAsync();
        var violations = desktop.Lab.Guard.Violations;

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            if (cycle % 2 == 1)
            {
                NativeSurface.SetExStyle(
                    window,
                    NativeSurface.ExStyle(window) & ~NativeSurface.ExNoActivate
                );
            }
            else
            {
                NativeSurface
                    .SetWindowPos(
                        window,
                        NativeSurface.NotTopmostBand,
                        0,
                        0,
                        0,
                        0,
                        NativeSurface.NoMove | NativeSurface.NoSize | NativeSurface.NoActivate
                    )
                    .ShouldBeTrue();
            }

            var drifted =
                surfaces.Count(surface =>
                    !NativeSurface.HasExStyle(surface, NativeSurface.ExNoActivate)
                )
                + surfaces.Count(surface =>
                    !NativeSurface.HasExStyle(surface, NativeSurface.ExTopmost)
                );
            drifted.ShouldBeGreaterThanOrEqualTo(
                1,
                Say($"Cycle {cycle}: the drift did not happen.")
            );

            WpfThread
                .Invoke(desktop.Lab.Integrity.CheckNow)
                .ShouldBe(drifted, Say($"Cycle {cycle}: every drifted property is repaired once."));

            foreach (var surface in surfaces)
            {
                NativeSurface
                    .HasExStyle(surface, NativeSurface.ExNoActivate | NativeSurface.ExTopmost)
                    .ShouldBeTrue(
                        Say(
                            $"Cycle {cycle}: the check did not put the styles back on 0x{surface:X}."
                        )
                    );
                NativeSurface.IsAbove(surface, desktop.Probe.Window).ShouldBeTrue();
            }

            ForegroundWindows
                .IsForeground(desktop.Probe.Window)
                .ShouldBeTrue(
                    Say($"Cycle {cycle}: the repair changed the foreground: ")
                        + ForegroundWindows.Describe()
                );
            desktop.Lab.Guard.Violations.ShouldBe(violations);
        }

        panel.Activations.ShouldBeEmpty();
        await desktop.ShouldHaveKeptTheForegroundAsync(cursor);
    }

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
