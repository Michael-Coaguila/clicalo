using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Windows.PointerTracking;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// <see cref="CursorTrail"/> (EJE-009, D21): a mouse action goes where the cursor last rested outside Clícalo, not to
/// the border a touchpad slid across into the panel.
/// </summary>
[Trait("Req", "EJE-009")]
public sealed class CursorTrailTests
{
    private static readonly PhysicalPoint Document = new(800, 500);
    private static readonly PhysicalPoint Border = new(1880, 520);

    private readonly FakeTimeProvider _time = new();
    private CursorTrail _trail;

    [Fact]
    public void A_touchpad_that_slides_into_the_panel_keeps_the_place_it_rested_on()
    {
        See(Document);
        Wait(Timings.Mouse.PointerRest);

        // The slide toward the bar: a few points a few milliseconds apart, then the bar.
        See(new PhysicalPoint(1200, 510));
        Wait(TimeSpan.FromMilliseconds(16));
        See(Border);
        Wait(TimeSpan.FromMilliseconds(16));
        OnClicalo();

        _trail.Place.ShouldBe(Document);
        _trail.Last.ShouldBe(Border);
    }

    [Fact]
    public void A_finger_that_rests_on_the_app_and_then_touches_the_panel_keeps_that_point()
    {
        See(Document);
        Wait(TimeSpan.FromSeconds(2));
        OnClicalo();

        _trail.Place.ShouldBe(Document);
    }

    [Fact]
    public void Time_on_the_panel_and_the_round_trip_of_a_wheel_step_never_make_a_border_a_resting_place()
    {
        See(Document);
        Wait(Timings.Mouse.PointerRest);
        See(Border);
        OnClicalo();

        // The person holds ⏷ for seconds; each wheel step takes the cursor to the document and back at once.
        Wait(TimeSpan.FromSeconds(3));
        OnClicalo();
        See(Document);
        OnClicalo();

        _trail.Place.ShouldBe(Document);
    }

    [Fact]
    public void Without_a_resting_place_the_last_point_outside_is_used()
    {
        See(Border);
        OnClicalo();

        _trail.Place.ShouldBe(Border);
        default(CursorTrail).Place.ShouldBeNull();
    }

    private void See(PhysicalPoint point) =>
        _trail = _trail.Outside(point, _time.GetTimestamp(), _time);

    private void OnClicalo() => _trail = _trail.OnClicalo(_time.GetTimestamp(), _time);

    private void Wait(TimeSpan time) => _time.Advance(time);
}
