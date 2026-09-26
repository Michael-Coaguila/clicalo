using System.Globalization;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// Spike S1 (docs/testing/spikes/S1.md, blueprint §15.1): with InputProbe in front, touching, holding and dragging the
/// surfaces with a synthetic finger, pen and mouse never changes the foreground, never takes the probe's focus and never
/// activates a surface. 20 gestures per combination; each one is checked to have reached the surface.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
public sealed class NonActivationTests(SurfaceDesktopFixture desktop)
    : IClassFixture<SurfaceDesktopFixture>
{
    private const int Cycles = 20;

    public static TheoryData<LabSurface, SyntheticPointerKind> Taps { get; } =
        new()
        {
            { LabSurface.Panel, SyntheticPointerKind.Finger },
            { LabSurface.Panel, SyntheticPointerKind.Pen },
            { LabSurface.Panel, SyntheticPointerKind.Mouse },
            { LabSurface.TabWithSide, SyntheticPointerKind.Finger },
            { LabSurface.TabWithSide, SyntheticPointerKind.Pen },
            { LabSurface.TabWithSide, SyntheticPointerKind.Mouse },
            { LabSurface.Bubble, SyntheticPointerKind.Finger },
            { LabSurface.Bubble, SyntheticPointerKind.Pen },
            { LabSurface.Bubble, SyntheticPointerKind.Mouse },
        };

    [DesktopTheory]
    [MemberData(nameof(Taps))]
    public async Task Tapping_a_surface_never_changes_the_foreground(
        LabSurface surface,
        SyntheticPointerKind kind
    )
    {
        var windows = desktop.WindowsOf(surface);
        var cursor = await desktop.PrepareAsync();
        var violations = desktop.Lab.Guard.Violations;
        using var pointer = desktop.CreatePointer(kind);

        for (var tap = 1; tap <= Cycles; tap++)
        {
            var window = windows[tap % windows.Count];
            var landed = Landed(window, kind);
            var (x, y) = SurfaceDesktopFixture.CenterOf(window);

            pointer.Tap(x, y);

            await SurfaceDesktopFixture.WaitUntilAsync(
                () => Landed(window, kind) > landed,
                Say($"Tap {tap} of {Cycles} by {kind} did not reach {window.Id}.")
            );
            ShouldStillBeInFront(
                Say($"after tap {tap} of {Cycles} on {window.Id} by {kind}"),
                violations
            );
        }

        await desktop.ShouldHaveKeptTheForegroundAsync(cursor);
        ShouldNotHaveBeenActivated(windows);
    }

    [DesktopTheory]
    [InlineData(LabSurface.Panel)]
    [InlineData(LabSurface.TabWithSide)]
    [InlineData(LabSurface.Bubble)]
    public async Task Holding_and_dragging_on_a_surface_never_changes_the_foreground(
        LabSurface surface
    )
    {
        var windows = desktop.WindowsOf(surface);
        var cursor = await desktop.PrepareAsync();
        var violations = desktop.Lab.Guard.Violations;
        using var finger = desktop.CreatePointer(SyntheticPointerKind.Finger);
        var hold = Timings.Touch.LongPress + SyntheticPointer.FrameInterval;
        var drag = TimeSpan.FromMilliseconds(200);

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            var held = windows[cycle % windows.Count];
            var landed = held.PointerUps;
            var (x, y) = SurfaceDesktopFixture.CenterOf(held);
            finger.Hold(x, y, hold);
            await SurfaceDesktopFixture.WaitUntilAsync(
                () => held.PointerUps > landed,
                Say($"Hold {cycle} of {Cycles} did not reach {held.Id}.")
            );
            ShouldStillBeInFront(Say($"after hold {cycle} of {Cycles} on {held.Id}"), violations);

            // The handle of the Tab view is dragged along the bar, the others across their width.
            var dragged = windows[(cycle + 1) % windows.Count];
            landed = dragged.PointerUps;
            var (fromX, fromY, toX, toY) = AlongLongerSide(NativeSurface.Bounds(dragged.Handle));
            finger.Drag(fromX, fromY, toX, toY, drag);
            await SurfaceDesktopFixture.WaitUntilAsync(
                () => dragged.PointerUps > landed,
                Say($"Drag {cycle} of {Cycles} did not reach {dragged.Id}.")
            );
            ShouldStillBeInFront(
                Say($"after drag {cycle} of {Cycles} on {dragged.Id}"),
                violations
            );
        }

        await desktop.ShouldHaveKeptTheForegroundAsync(cursor);
        ShouldNotHaveBeenActivated(windows);
    }

    private static int Landed(TestSurface window, SyntheticPointerKind kind) =>
        kind == SyntheticPointerKind.Mouse ? window.MouseUps : window.PointerUps;

    private static (int FromX, int FromY, int ToX, int ToY) AlongLongerSide(
        NativeSurface.Rect bounds
    ) =>
        bounds.Width >= bounds.Height
            ? (
                bounds.Left + (bounds.Width / 4),
                bounds.CenterY,
                bounds.Right - (bounds.Width / 4),
                bounds.CenterY
            )
            : (
                bounds.CenterX,
                bounds.Top + (bounds.Height / 4),
                bounds.CenterX,
                bounds.Bottom - (bounds.Height / 4)
            );

    private static void ShouldNotHaveBeenActivated(IEnumerable<TestSurface> windows)
    {
        foreach (var window in windows)
        {
            window.Activations.ShouldBeEmpty(
                Say($"{window.Id} received activation or focus messages.")
            );
        }
    }

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);

    private void ShouldStillBeInFront(string when, long violations)
    {
        ForegroundWindows
            .IsForeground(desktop.Probe.Window)
            .ShouldBeTrue("The foreground changed " + when + ": " + ForegroundWindows.Describe());
        desktop.Lab.Guard.Violations.ShouldBe(
            violations,
            "ActivationGuard saw a surface activated " + when + "."
        );
    }
}
