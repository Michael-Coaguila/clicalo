using Clicalo.TestKit.Windows.Input;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// The guards of <see cref="SyntheticPointer"/> refuse before anything is injected. Headless and safe on any machine:
/// the only allowed process does not exist, so every point belongs to someone else, and without
/// <c>CLICALO_DESKTOP_TESTS=1</c> the injector refuses even earlier.
/// </summary>
public sealed class SyntheticPointerSafetyTests
{
    private static readonly int[] NoRealProcess = [-1];

    [Theory]
    [InlineData(SyntheticPointerKind.Finger)]
    [InlineData(SyntheticPointerKind.Pen)]
    [InlineData(SyntheticPointerKind.Mouse)]
    [Trait("Req", "REG-01")]
    public void A_point_outside_the_allowed_processes_is_refused_without_injecting(
        SyntheticPointerKind kind
    )
    {
        using var pointer = new SyntheticPointer(kind, NoRealProcess);
        var (work, _) = NativeSurface.PrimaryWorkArea();

        Should.Throw<InjectionRefusedException>(() => pointer.Tap(work.CenterX, work.CenterY));
        Should.Throw<InjectionRefusedException>(() =>
            pointer.Hold(work.CenterX, work.CenterY, SyntheticPointer.FrameInterval)
        );
        Should.Throw<InjectionRefusedException>(() =>
            pointer.Drag(
                work.Left + 1,
                work.Top + 1,
                work.CenterX,
                work.CenterY,
                SyntheticPointer.FrameInterval
            )
        );

        pointer.Gestures.ShouldBe(0, "Nothing went down.");
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void A_target_outside_the_allowed_processes_is_named_without_its_title()
    {
        using var pointer = new SyntheticPointer(SyntheticPointerKind.Finger, NoRealProcess);
        var (work, _) = NativeSurface.PrimaryWorkArea();

        pointer.IsAllowedTarget(work.CenterX, work.CenterY, out var description).ShouldBeFalse();

        description.ShouldMatch("^(no window|window 0x[0-9A-F]+ of .+ \\(pid [0-9]+\\))$");
    }

    [Fact]
    public void An_injector_needs_at_least_one_allowed_process() =>
        Should.Throw<ArgumentException>(() =>
            new SyntheticPointer(SyntheticPointerKind.Finger, [])
        );
}
