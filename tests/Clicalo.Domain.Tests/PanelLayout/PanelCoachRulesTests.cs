using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Tests.PanelLayout;

/// <summary>
/// The guide of the Full and Compact views (R-38, as PES-015 is for the Tab view): when it shows beside the panel and
/// its three steps, as tables.
/// </summary>
[Trait("Req", "PES-015")]
public sealed class PanelCoachRulesTests
{
    /// <summary>Form, saved view, done, Quick settings open, welcome open → shows.</summary>
    public static TheoryData<PanelForm, PanelDensity, bool, bool, bool, bool> Cases =>
        new()
        {
            { PanelForm.Full, PanelDensity.Full, false, false, false, true },
            { PanelForm.Compact, PanelDensity.Compact, false, false, false, true },
            // Finished or skipped: never again until General asks for it.
            { PanelForm.Full, PanelDensity.Full, true, false, false, false },
            { PanelForm.Compact, PanelDensity.Compact, true, false, false, false },
            // Quick settings open in the same place beside the panel (AJR-001).
            { PanelForm.Full, PanelDensity.Full, false, true, false, false },
            // The welcome comes first.
            { PanelForm.Compact, PanelDensity.Compact, false, false, true, false },
            // The Full view the search of the bar borrows (BUS-006) has the guide of the bar instead.
            { PanelForm.Full, PanelDensity.Dock, false, false, false, false },
            // No panel on screen: hidden, the bubble or the Tab view.
            { PanelForm.Hidden, PanelDensity.Full, false, false, false, false },
            { PanelForm.Bubble, PanelDensity.Compact, false, false, false, false },
            { PanelForm.DockOpen, PanelDensity.Dock, false, false, false, false },
            { PanelForm.DockClosed, PanelDensity.Dock, false, false, false, false },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_guide_shows_beside_the_panel_until_it_is_done(
        PanelForm form,
        PanelDensity density,
        bool done,
        bool quickSettingsOpen,
        bool welcomeOpen,
        bool expected
    ) =>
        PanelCoachRules
            .Shows(form, density, done, quickSettingsOpen, welcomeOpen)
            .ShouldBe(expected);

    [Fact]
    public void The_guide_has_three_steps()
    {
        PanelCoachRules.Steps.ShouldBe(3);
        PanelCoachRules.NextStep(0).ShouldBe(1);
        PanelCoachRules.NextStep(1).ShouldBe(2);
        PanelCoachRules.NextStep(2).ShouldBeNull();
    }

    [Fact]
    public void A_new_installation_shows_it_and_keeps_it_apart_from_the_tab_guide()
    {
        SettingsSchema.Defaults.PanelCoachDone.ShouldBeFalse();
        var done = SettingsSchema.Defaults with { PanelCoachDone = true };
        done.Dock.CoachDone.ShouldBeFalse();
    }
}
