using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// Target resolution with the extra hit area: a direct hit wins, overlapping bounds go to the nearest center
/// (REG-02) and a touch in a gap goes to the nearest target, with the nearest center breaking ties (TAC-002).
/// </summary>
[Trait("Req", "REG-02")]
[Trait("Req", "TAC-002")]
public sealed class HitResolverTests
{
    // Two 80×80 tiles with a 20 px gap: [100, 180) and [200, 280) horizontally, [100, 180) vertically.
    private static readonly TouchTarget[] Pair =
    [
        TouchScript.Tile(1, 100, 100),
        TouchScript.Tile(2, 200, 100),
    ];

    [Theory]
    [InlineData(140, 140, 0)] // inside the first tile
    [InlineData(179, 140, 0)] // last column of the first tile
    [InlineData(200, 140, 1)] // first column of the second tile
    [InlineData(185, 140, 0)] // gap, 6 px from the first tile, 15 from the second
    [InlineData(195, 140, 1)] // gap, 16 px from the first tile, 5 from the second
    [InlineData(100, 90, 0)] // above the first tile, inside its extra area
    [InlineData(100, 85, -1)] // above the first tile, just beyond its extra area
    [InlineData(290, 140, -1)] // right of the second tile, beyond its extra area
    public void A_touch_goes_to_the_target_it_hits_or_to_the_nearest_one_within_the_extra_area(
        int x,
        int y,
        int expected
    ) => HitResolver.Resolve(Pair, new PhysicalPoint(x, y), hitSlop: 10).ShouldBe(expected);

    [Fact]
    public void A_touch_exactly_between_two_targets_goes_to_the_nearest_center()
    {
        // A wide tile and a small one, both 11 px away from the touch, whose centers are at different distances.
        TouchTarget[] targets =
        [
            TouchScript.Tile(1, 100, 100, width: 80, height: 80),
            TouchScript.Tile(2, 201, 130, width: 40, height: 20),
        ];

        // (190, 140): 11 px from the first tile's edge (x 179) and 11 px from the second's (x 201).
        var point = new PhysicalPoint(190, 140);
        HitResolver.EdgeDistanceSquared(targets[0].Bounds, point).ShouldBe(121);
        HitResolver.EdgeDistanceSquared(targets[1].Bounds, point).ShouldBe(121);

        // Centers: (140, 140) is 50 px away and (221, 140) is 31 px away: the second tile wins.
        HitResolver.Resolve(targets, point, hitSlop: 14).ShouldBe(1);
    }

    [Fact]
    public void Overlapping_bounds_go_to_the_control_whose_center_is_nearest()
    {
        // Two controls drawn smaller than 44 and enlarged to 44 × 44 overlap between x 130 and 140.
        TouchTarget[] targets =
        [
            TouchScript.Tile(1, 96, 100, width: 44, height: 44),
            TouchScript.Tile(2, 130, 100, width: 44, height: 44),
        ];

        HitResolver.Resolve(targets, new PhysicalPoint(133, 120), hitSlop: 8).ShouldBe(0);
        HitResolver.Resolve(targets, new PhysicalPoint(137, 120), hitSlop: 8).ShouldBe(1);
    }

    [Fact]
    public void A_direct_hit_wins_over_a_nearer_center_in_the_extra_area()
    {
        // A wide tile and a small one: the point is inside the wide tile, whose center is far, and in the extra area
        // of the small one, whose center is near.
        TouchTarget[] targets =
        [
            TouchScript.Tile(1, 0, 100, width: 300, height: 80),
            TouchScript.Tile(2, 305, 100, width: 44, height: 44),
        ];

        HitResolver.Resolve(targets, new PhysicalPoint(298, 110), hitSlop: 14).ShouldBe(0);
    }

    [Fact]
    public void Empty_targets_and_empty_layouts_are_never_hit()
    {
        TouchTarget[] targets = [TouchScript.Tile(1, 100, 100, width: 0, height: 80)];

        HitResolver.Resolve(targets, new PhysicalPoint(100, 120), hitSlop: 40).ShouldBe(-1);
        HitResolver.Resolve([], new PhysicalPoint(100, 120), hitSlop: 40).ShouldBe(-1);
    }

    [Fact]
    public void A_remaining_tie_goes_to_the_target_listed_first()
    {
        TouchTarget[] targets = [TouchScript.Tile(7, 100, 100), TouchScript.Tile(3, 100, 100)];

        HitResolver.Resolve(targets, new PhysicalPoint(140, 140), hitSlop: 0).ShouldBe(0);
    }
}
