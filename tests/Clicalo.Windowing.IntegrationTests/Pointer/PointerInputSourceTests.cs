using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.Windowing.IntegrationTests.Desktop;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// S1 support (docs/testing/spikes/S1.md, folder Pointer/): synthetic finger, pen and mouse input on a real
/// non-activatable surface reaches its <see cref="PointerInputSource"/> as frames with the right phase, device,
/// physical position, contact area and origin (ACC-007, blueprint §8.3).
/// </summary>
[Trait("Requires", "Desktop")]
[Trait("Req", "ACC-007")]
[Collection(DesktopCollectionDefinition.Name)]
public sealed class PointerInputSourceTests(PointerDesktopFixture fixture)
    : IClassFixture<PointerDesktopFixture>
{
    /// <summary>A contact's position may differ this much from the injected point (mouse coordinates are normalized).</summary>
    private const int Tolerance = 2;

    [DesktopTheory]
    [InlineData(SyntheticPointerKind.Finger, PointerKind.Finger)]
    [InlineData(SyntheticPointerKind.Pen, PointerKind.Pen)]
    [InlineData(SyntheticPointerKind.Mouse, PointerKind.Mouse)]
    public async Task Finger_pen_and_mouse_reach_the_surface_as_frames(
        SyntheticPointerKind device,
        PointerKind expected
    )
    {
        PointerSetup.IsMouseInPointerEnabled.ShouldBeTrue(
            "the test process routes the mouse like the product"
        );
        await fixture.PrepareAsync();
        var at = fixture.CenterOf(3);

        using (var pointer = PointerDesktopFixture.CreatePointer(device))
        {
            pointer.Tap(at.X, at.Y);
        }

        await PointerDesktopFixture.WaitUntilAsync(
            () => fixture.Recorder.Gestures.Count > 0,
            "the tap never became a gesture"
        );
        var samples = fixture.Recorder.Samples;
        samples[0].Phase.ShouldBe(PointerPhase.Down);
        samples[^1].Phase.ShouldBe(PointerPhase.Up);
        samples.Skip(1).SkipLast(1).ShouldAllBe(s => s.Phase == PointerPhase.Move);
        samples.Select(s => s.PointerId).Distinct().ShouldHaveSingleItem();
        samples.ShouldAllBe(s => s.Kind == expected);
        samples.ShouldAllBe(s => s.Origin == PointerInputOrigin.Injected);
        samples.ShouldAllBe(s => Near(s.Position, at), "positions are physical screen pixels");
        if (expected == PointerKind.Finger)
        {
            samples.ShouldAllBe(s =>
                !s.Contact.IsEmpty && s.Contact.Inflate(Tolerance).Contains(s.Position)
            );
        }
        else
        {
            samples.ShouldAllBe(s =>
                s.Contact == new PhysicalRect(s.Position.X, s.Position.Y, 1, 1)
            );
        }

        var frames = fixture.Recorder.Frames;
        frames.ShouldAllBe(
            f => f.Frame.Timestamp <= f.ReceivedAt,
            "a frame is never dated in the future"
        );
        frames.Select(f => f.Frame.Timestamp).ShouldBeInOrder(SortDirection.Ascending);

        var tap = fixture.Recorder.Gestures.ShouldHaveSingleItem().Gesture;
        tap.Kind.ShouldBe(GestureKind.Tap);
        tap.Target.ShouldBe(new TouchTargetId(3));
        fixture.Arbiter.Violations.ShouldBeEmpty();
    }

    [DesktopTheory]
    [InlineData(SyntheticPointerKind.Finger)]
    [InlineData(SyntheticPointerKind.Pen)]
    [InlineData(SyntheticPointerKind.Mouse)]
    public async Task Entering_and_leaving_the_surface_is_reported(SyntheticPointerKind device)
    {
        await fixture.PrepareAsync();
        var at = fixture.CenterOf(3);

        using (var pointer = PointerDesktopFixture.CreatePointer(device))
        {
            pointer.Tap(at.X, at.Y);
        }

        await PointerDesktopFixture.WaitUntilAsync(
            () => fixture.Recorder.Hovers is [.., false],
            "the pointer never left the surface"
        );
        fixture.Recorder.Hovers[0].ShouldBeTrue();
    }

    [DesktopFact]
    public async Task The_thread_runs_above_normal_only_while_a_contact_is_down()
    {
        await fixture.PrepareAsync();
        var at = fixture.CenterOf(1);
        var priorities = new List<ThreadPriority>();
        var host = fixture.Host;
        var before = WpfThread.Invoke(() => Thread.CurrentThread.Priority);

        using (var pointer = PointerDesktopFixture.CreatePointer(SyntheticPointerKind.Finger))
        {
            var holding = Task.Run(() => pointer.Hold(at.X, at.Y, TimeSpan.FromMilliseconds(400)));
            await PointerDesktopFixture.WaitUntilAsync(
                () => WpfThread.Invoke(() => fixture.Surface.Source.ActiveContacts) == 1,
                "the contact never went down"
            );
            priorities.Add(WpfThread.Invoke(() => Thread.CurrentThread.Priority));
            await holding;
        }

        await PointerDesktopFixture.WaitUntilAsync(
            () => WpfThread.Invoke(() => fixture.Surface.Source.ActiveContacts) == 0,
            "the contact never lifted"
        );
        priorities.Add(WpfThread.Invoke(() => Thread.CurrentThread.Priority));

        priorities.ShouldBe([ThreadPriority.AboveNormal, before]);
        host.Recognizer.ActiveContacts.ShouldBe(0);
    }

    private static bool Near(PhysicalPoint actual, PhysicalPoint expected) =>
        Math.Abs(actual.X - expected.X) <= Tolerance
        && Math.Abs(actual.Y - expected.Y) <= Tolerance;
}
