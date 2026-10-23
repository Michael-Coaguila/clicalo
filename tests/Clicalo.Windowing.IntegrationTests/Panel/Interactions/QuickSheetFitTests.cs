using Clicalo.UI.Wpf.Surfaces.Panel.QuickSettings;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>
/// AJR-001 without a screen: the sheet in its window takes the room it has down to the foot of the work area, never
/// less than its minimum, and its content when that is less (D21: no longer 62 % of the screen).
/// </summary>
[Trait("Req", "AJR-001")]
public sealed class QuickSheetFitTests
{
    [Fact]
    public void The_sheet_takes_its_room_never_below_its_minimum_and_its_content_when_less()
    {
        QuickSheetFit.MaxHeight(2000).ShouldBe(2000);
        QuickSheetFit.MaxHeight(10).ShouldBe(QuickSheetFit.MinimumHeight);
        QuickSheetFit.Height(400, natural: 300).ShouldBe(300);
        QuickSheetFit
            .Height(400, natural: null)
            .ShouldBe(400, "a sheet never laid out fills its room");
    }
}
