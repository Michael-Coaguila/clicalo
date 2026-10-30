using Clicalo.Domain.Guides;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Tests.Guides;

/// <summary>
/// The guides of every screen (R-39, as PES-015 is for the Tab view): which one shows, which step, when an action ends
/// a step and what is remembered, as tables.
/// </summary>
[Trait("Req", "PES-015")]
public sealed class GuideRulesTests
{
    private static readonly UserSettings New = SettingsSchema.Defaults;

    /// <summary>Form, saved view, beside the bar, Quick settings, welcome, Control Center → the guide.</summary>
    public static TheoryData<PanelForm, PanelDensity, bool, bool, bool, bool, GuideId?> Cases =>
        new()
        {
            { PanelForm.Full, PanelDensity.Full, false, false, false, false, GuideId.Panel },
            { PanelForm.Compact, PanelDensity.Compact, false, false, false, false, GuideId.Panel },
            { PanelForm.DockOpen, PanelDensity.Dock, false, false, false, false, GuideId.Tab },
            { PanelForm.Bubble, PanelDensity.Full, false, false, false, false, GuideId.Bubble },
            // Quick settings open where the guide of the panel goes (AJR-001); a window beside the bar hides its own.
            { PanelForm.Full, PanelDensity.Full, false, true, false, false, null },
            { PanelForm.DockOpen, PanelDensity.Dock, true, false, false, false, null },
            // One guide at a time: the welcome and the Control Center come first.
            { PanelForm.Compact, PanelDensity.Compact, false, false, true, false, null },
            { PanelForm.Full, PanelDensity.Full, false, false, false, true, null },
            { PanelForm.Bubble, PanelDensity.Full, false, false, false, true, null },
            // The Full view the search of the bar borrows (BUS-006) is not the person's view.
            { PanelForm.Full, PanelDensity.Dock, false, false, false, false, null },
            { PanelForm.DockClosed, PanelDensity.Dock, false, false, false, false, null },
            { PanelForm.Hidden, PanelDensity.Full, false, false, false, false, null },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void One_guide_shows_on_the_surfaces_at_a_time(
        PanelForm form,
        PanelDensity density,
        bool besideBar,
        bool quickSettings,
        bool welcome,
        bool controlCenter,
        GuideId? expected
    ) =>
        GuideRules
            .OnSurfaces(
                form,
                New with
                {
                    Density = density,
                },
                besideBar,
                quickSettings,
                welcome,
                controlCenter
            )
            .ShouldBe(expected);

    [Fact]
    public void A_guide_done_never_shows_again_and_the_tab_keeps_its_own_flag()
    {
        var done = New with
        {
            GuidesDone = ["panel", "bubble"],
            Dock = New.Dock with { CoachDone = true },
        };

        GuideRules.OnSurfaces(PanelForm.Full, done, false, false, false, false).ShouldBeNull();
        GuideRules.OnSurfaces(PanelForm.Bubble, done, false, false, false, false).ShouldBeNull();
        GuideRules
            .OnSurfaces(
                PanelForm.DockOpen,
                done with
                {
                    Density = PanelDensity.Dock,
                },
                false,
                false,
                false,
                false
            )
            .ShouldBeNull();
        GuideRules.InControlCenter(GuideId.Shortcuts, done).ShouldBe(GuideId.Shortcuts);
        GuideRules
            .InControlCenter(GuideId.Shortcuts, done with { GuidesDone = ["shortcuts"] })
            .ShouldBeNull();
    }

    [Fact]
    public void The_pager_step_is_left_out_with_a_single_page()
    {
        var pager = GuideCatalog
            .Steps(GuideId.Panel)
            .ToList()
            .FindIndex(static s => s.Target == GuideTarget.PanelPager);

        GuideRules.Showing(GuideId.Panel, pager, static _ => true).ShouldBe(pager);
        GuideRules
            .Showing(GuideId.Panel, pager, static t => t != GuideTarget.PanelPager)
            .ShouldBe(pager + 1);
        GuideRules
            .Showing(GuideId.Panel, GuideCatalog.Steps(GuideId.Panel).Length, static _ => true)
            .ShouldBeNull();
    }

    [Fact]
    public void Only_the_awaited_action_ends_a_step_by_itself()
    {
        // ★ of the panel waits for the Frequents; the tiles never wait: a tile acts on the person's app.
        GuideRules.Completes(GuideId.Panel, 1, GuideSignal.FrequentsShown).ShouldBeTrue();
        GuideRules.Completes(GuideId.Panel, 1, GuideSignal.Minimized).ShouldBeFalse();
        GuideRules.Completes(GuideId.Panel, 0, GuideSignal.FrequentsShown).ShouldBeFalse();
        GuideRules.Completes(GuideId.Panel, 99, GuideSignal.Minimized).ShouldBeFalse();
        GuideCatalog.Steps(GuideId.Panel)[^1].Awaits.ShouldBe(GuideSignal.Minimized);
        GuideCatalog.Steps(GuideId.Bubble).Single().Awaits.ShouldBe(GuideSignal.Restored);
    }

    [Fact]
    public void Every_guide_has_steps_with_its_own_controls_and_a_name()
    {
        foreach (var guide in Enum.GetValues<GuideId>())
        {
            var steps = GuideCatalog.Steps(guide);
            steps.ShouldNotBeEmpty();
            steps.Select(static s => s.Target).ShouldBeUnique();
            (
                GuideCatalog.Name(guide) is not null ^ GuideCatalog.Section(guide) is not null
            ).ShouldBeTrue($"{guide} needs one name");
            steps
                .Where(static s => s.Target is GuideTarget.PanelGrid or GuideTarget.TabTiles)
                .ShouldAllBe(static s => s.Awaits == null);
        }
    }

    [Fact]
    public void A_guide_done_is_added_once_and_the_ids_are_stable()
    {
        var done = GuideProgress.WithDone([], GuideId.Panel);
        done = GuideProgress.WithDone(done, GuideId.Touch);

        GuideProgress.WithDone(done, GuideId.Panel).ShouldBe(done);
        done.ShouldBe(["panel", "touch"]);
        Enum.GetValues<GuideId>()
            .Select(GuideProgress.Id)
            .ShouldBe([
                "panel",
                "bubble",
                "tab",
                "shortcuts",
                "templates",
                "general",
                "touch",
                "system",
                "about",
            ]);
    }

    [Fact]
    public void The_list_of_guides_done_is_repaired_when_it_is_read()
    {
        string[] read = ["panel", "", "panel", "aNewGuide", new string('x', 40)];

        var repaired = SettingsSchema.Clamp(New with { GuidesDone = [.. read] }, out var changed);

        // An id a later version added is kept, so that version does not show its guide again.
        repaired.GuidesDone.ShouldBe(["panel", "aNewGuide"]);
        changed.ShouldBe([SettingPaths.GuidesDone]);
        New.GuidesDone.ShouldBeEmpty();
    }
}
