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
/// The condition a desktop test waits for after a surface on screen grows (<see cref="ComposedFrame"/>): a surface over
/// InputProbe that grows downwards and is tapped in its new part as soon as the grown frame is composed receives the
/// touch; nothing falls through to the probe below. Without the condition, the tap on «Soltar todo» of
/// <c>PanelDesktopTests</c>, sent 5–10 ms after the panic strip grew the panel, fell through to the window below in 47
/// of 300 CI runs (S1.md, finding 17). 20 surfaces, each grown once.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class ComposedFrameTests
{
    private const int Cycles = 20;
    private const int Size = 64;
    private const int Growth = 96;

    [DesktopFact]
    public async Task A_surface_receives_a_touch_on_its_new_part_once_the_grown_frame_is_composed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var probe = await InputProbeSession.StartAsync(cancellationToken);
        await probe.EnsureForegroundAsync(
            SurfaceDesktopFixture.ForegroundTimeout,
            cancellationToken
        );
        using var lab = SurfaceLab.Create();
        var below = NativeSurface.Bounds(probe.Window);
        var small = new PhysicalRect(below.CenterX - (Size / 2), below.CenterY - Size, Size, Size);
        var grown = small with { Height = Size + Growth };
        using var finger = new SyntheticPointer(
            SyntheticPointerKind.Finger,
            [Environment.ProcessId, probe.ProcessId]
        );

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            var surface = lab.CreateSurface(SurfaceKind.Panel, cycle, Size, Size);
            try
            {
                var shown = WpfThread.Invoke(() =>
                {
                    surface.MovePassive(small);
                    var watched = FirstFrame.Watch(surface);
                    surface.ShowPassive();
                    return watched;
                });
                await shown.WaitAsync(SurfaceDesktopFixture.EventTimeout, cancellationToken);

                // The surface grows downwards over the probe, as the panic strip grows the panel.
                WpfThread.Invoke(() => surface.MovePassive(grown));
                await ComposedFrame
                    .WaitAsync(surface, cancellationToken)
                    .WaitAsync(SurfaceDesktopFixture.EventTimeout, cancellationToken);

                var x = grown.Left + (Size / 2);
                var y = grown.Top + Size + (Growth / 2);
                var bounds = NativeSurface.Bounds(surface.Handle);
                bounds.Height.ShouldBe(
                    Size + Growth,
                    Say($"Cycle {cycle}: the surface did not grow.")
                );
                var stack = WindowsAt.Describe(x, y);
                stack.Count.ShouldBeGreaterThanOrEqualTo(2, Say($"Cycle {cycle}: nothing below."));
                stack[1]
                    .ShouldContain(
                        "InputProbe",
                        Case.Sensitive,
                        Say($"Cycle {cycle}: the probe is not right below the new part.")
                    );

                var cursor = probe.Cursor;
                finger.Tap(x, y);
                await SurfaceDesktopFixture.WaitUntilAsync(
                    () => surface.PointerUps > 0,
                    Say(
                        $"Cycle {cycle}: the touch on the new part did not reach the grown surface."
                    ),
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
