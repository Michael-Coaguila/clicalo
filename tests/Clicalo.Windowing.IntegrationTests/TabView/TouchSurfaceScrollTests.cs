using Clicalo.UI.Wpf.Surfaces;

namespace Clicalo.Windowing.IntegrationTests.TabView;

/// <summary>
/// <see cref="TouchSurfaceScroll"/> (TAC-004): in a zone that scrolls, a gesture that passes the «cancel if you slide»
/// distance scrolls and activates nothing; one that stays within it is still a tap.
/// </summary>
public sealed class TouchSurfaceScrollTests
{
    private const double Threshold = 12;

    [Fact]
    [Trait("Req", "TAC-004")]
    public void A_contact_that_slides_past_the_threshold_scrolls_by_as_much_and_is_not_a_tap()
    {
        var scroll = new TouchSurfaceScroll();
        scroll.Down(7, position: 500, offset: 40);

        // Within the threshold nothing scrolls.
        scroll.Move(7, 492, Threshold, scale: 1).ShouldBeNull();
        scroll.IsScrolling.ShouldBeFalse();

        // The finger goes up 60 px: the content follows it, 60 px further down.
        scroll.Move(7, 440, Threshold, scale: 1).ShouldBe(100);
        scroll.IsScrolling.ShouldBeTrue();

        // Once it scrolls it keeps following the finger, back inside the threshold too.
        scroll.Move(7, 495, Threshold, scale: 1).ShouldBe(45);

        scroll.Up(7).ShouldBeTrue();
        scroll.IsScrolling.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "TAC-004")]
    public void A_contact_that_stays_within_the_threshold_scrolls_nothing_and_stays_a_tap()
    {
        var scroll = new TouchSurfaceScroll();
        scroll.Down(7, position: 500, offset: 40);

        scroll.Move(7, 510, Threshold, scale: 1).ShouldBeNull();
        scroll.Move(7, 488, Threshold, scale: 1).ShouldBeNull();

        scroll.Up(7).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "TAC-004")]
    public void The_offset_is_in_logical_pixels_whatever_the_scale_of_the_monitor()
    {
        var scroll = new TouchSurfaceScroll();
        scroll.Down(7, position: 900, offset: 0);

        // 150 physical pixels at 150 % are 100 logical ones.
        scroll.Move(7, 750, Threshold * 1.5, scale: 1.5).ShouldBe(100);
    }

    [Fact]
    [Trait("Req", "TAC-004")]
    public void Another_contact_neither_scrolls_nor_ends_the_gesture()
    {
        var scroll = new TouchSurfaceScroll();
        scroll.Down(7, position: 500, offset: 0);

        scroll.Move(8, 300, Threshold, scale: 1).ShouldBeNull();
        scroll.Up(8).ShouldBeFalse();

        scroll.Move(7, 400, Threshold, scale: 1).ShouldBe(100);
        scroll.Up(7).ShouldBeTrue();

        // Without a contact down nothing scrolls.
        scroll.Move(7, 100, Threshold, scale: 1).ShouldBeNull();
        scroll.Up(7).ShouldBeFalse();
    }
}
