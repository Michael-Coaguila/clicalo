using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Settings;
using static Clicalo.Domain.Tests.Migration.V1Docs;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>The v1 window position on this machine (catalog §7.4, MIG-006).</summary>
[Trait("Req", "MIG-006")]
public sealed class V1PlacementTests
{
    [Fact]
    public void A_point_inside_a_monitor_is_scaled_to_physical_pixels()
    {
        var (position, moved) = V1Placement.ToPanelPosition(new V1Pair(743, 46), [Primary]);

        position.ShouldBe(new MonitorPosition(Primary.Id, 1300, 81));
        moved.ShouldBeFalse();
    }

    [Fact]
    public void A_point_outside_every_monitor_moves_to_the_primary_one()
    {
        // x = 1963 with 1371 logical pixels (2400 at 175 %), the real dist\profiles.json.
        var (position, moved) = V1Placement.ToPanelPosition(new V1Pair(1963, 290), [Primary]);

        moved.ShouldBeTrue();
        position.ShouldBe(new MonitorPosition(Primary.Id, 140, 140));
    }

    [Fact]
    public void A_point_on_the_second_monitor_stays_there()
    {
        var (position, moved) = V1Placement.ToPanelPosition(
            new V1Pair(2500, 100),
            [Primary, Secondary]
        );

        moved.ShouldBeFalse();
        position.ShouldBe(new MonitorPosition(Secondary.Id, 2500, 100));
    }

    [Fact]
    public void The_primary_monitor_is_found_wherever_it_is_listed()
    {
        var (position, moved) = V1Placement.ToPanelPosition(
            new V1Pair(-5000, -5000),
            [Secondary, Primary]
        );

        moved.ShouldBeTrue();
        position!.MonitorId.ShouldBe(Primary.Id);
    }

    [Fact]
    public void Without_monitors_the_default_placement_is_kept()
    {
        var (position, moved) = V1Placement.ToPanelPosition(new V1Pair(80, 80), []);

        position.ShouldBeNull();
        moved.ShouldBeFalse();
    }
}
