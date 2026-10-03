using System.Globalization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// The readiness condition of every pointer fixture (<see cref="FirstFrame"/>): a surface shown over InputProbe and
/// tapped as soon as its first frame is composed receives the touch; nothing falls through to the probe below. Without
/// the condition the first gesture of <c>NonActivationTests</c>, <c>ControlCenterLeaseTests</c> and
/// <c>DictationTests</c> fell through to the window below on the CI runner (S1.md). 20 fresh surfaces.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class FirstFrameTests
{
    private const int Cycles = 20;
    private const int Size = 64;

    [DesktopFact]
    public async Task A_surface_receives_the_first_touch_once_its_first_frame_is_composed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var probe = await InputProbeSession.StartAsync(cancellationToken);
        await probe.EnsureForegroundAsync(
            SurfaceDesktopFixture.ForegroundTimeout,
            cancellationToken
        );
        using var lab = SurfaceLab.Create();
        var below = NativeSurface.Bounds(probe.Window);
        var spot = new PhysicalRect(
            below.CenterX - (Size / 2),
            below.CenterY - (Size / 2),
            Size,
            Size
        );
        using var finger = new SyntheticPointer(
            SyntheticPointerKind.Finger,
            [Environment.ProcessId, probe.ProcessId]
        );

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            var surface = lab.CreateSurface(SurfaceKind.Bubble, cycle, Size, Size);
            try
            {
                var composed = WpfThread.Invoke(() =>
                {
                    surface.MovePassive(spot);
                    var watched = FirstFrame.Watch(surface);
                    surface.ShowPassive();
                    return watched;
                });
                await composed.WaitAsync(SurfaceDesktopFixture.EventTimeout, cancellationToken);

                // What a falling-through touch would reach is the probe and nothing else.
                var (x, y) = SurfaceDesktopFixture.CenterOf(surface);
                var stack = WindowsAt.Describe(x, y);
                stack.Count.ShouldBeGreaterThanOrEqualTo(2, Say($"Cycle {cycle}: nothing below."));
                stack[1]
                    .ShouldContain(
                        "InputProbe",
                        Case.Sensitive,
                        Say($"Cycle {cycle}: the probe is not right below the surface.")
                    );

                var cursor = probe.Cursor;
                finger.Tap(x, y);
                await SurfaceDesktopFixture.WaitUntilAsync(
                    () => surface.PointerUps > 0,
                    Say($"Cycle {cycle}: the first touch did not reach the composed surface."),
                    () =>
                        string.Join(Environment.NewLine, surface.PointerLogSince(0))
                        + Environment.NewLine
                        + string.Join(Environment.NewLine, probe.EventsSince(cursor))
                );
                probe
                    .EventsSince(cursor)
                    .OfType<MouseButtonEvent>()
                    .ShouldBeEmpty(Say($"Cycle {cycle}: the touch also reached the probe."));
            }
            finally
            {
                lab.Close(surface);
            }
        }
    }

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
