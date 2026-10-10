using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Tests.PanelLayout;

/// <summary>
/// The forms of the panel and the rules of the Tab view and the Compact view that are not geometry (PAN-001, PES-008,
/// PES-010, PES-012, PES-015, VCO-002), as tables.
/// </summary>
public sealed class PanelFormsTests
{
    /// <summary>Visible, view, minimized, bar open → form.</summary>
    public static TheoryData<bool, PanelDensity, bool, bool, PanelForm> Forms =>
        new()
        {
            { false, PanelDensity.Full, false, false, PanelForm.Hidden },
            { false, PanelDensity.Dock, true, true, PanelForm.Hidden },
            { true, PanelDensity.Full, false, false, PanelForm.Full },
            { true, PanelDensity.Compact, false, false, PanelForm.Compact },
            { true, PanelDensity.Full, true, false, PanelForm.Bubble },
            { true, PanelDensity.Compact, true, false, PanelForm.Bubble },
            { true, PanelDensity.Dock, false, false, PanelForm.DockClosed },
            { true, PanelDensity.Dock, false, true, PanelForm.DockOpen },
            // The Tab view has no «−»: it never becomes the bubble.
            { true, PanelDensity.Dock, true, false, PanelForm.DockClosed },
            { true, PanelDensity.Full, false, true, PanelForm.Full },
        };

    [Theory]
    [MemberData(nameof(Forms))]
    [Trait("Req", "PAN-001")]
    public void The_panel_has_exactly_one_form(
        bool visible,
        PanelDensity density,
        bool minimized,
        bool dockOpen,
        PanelForm expected
    )
    {
        var form = PanelForms.Of(visible, density, minimized, dockOpen);

        form.ShouldBe(expected);
        PanelForms.IsPanel(form).ShouldBe(form is PanelForm.Full or PanelForm.Compact);
        PanelForms.IsDock(form).ShouldBe(form is PanelForm.DockClosed or PanelForm.DockOpen);
    }

    [Theory]
    [InlineData(PanelDensity.Dock, false, PanelForm.Full)]
    [InlineData(PanelDensity.Dock, true, PanelForm.Full)]
    [InlineData(PanelDensity.Full, false, PanelForm.Full)]
    [InlineData(PanelDensity.Compact, false, PanelForm.Compact)]
    [Trait("Req", "PAN-001")]
    [Trait("Req", "BUS-006")]
    public void A_search_from_the_bar_shows_the_Full_view_for_a_while_without_changing_the_view(
        PanelDensity density,
        bool dockOpen,
        PanelForm expected
    )
    {
        PanelForms.Of(true, density, false, dockOpen, searchPeek: true).ShouldBe(expected);

        // Hidden from the tray stays hidden, and once the search ends the Tab view is back as it was.
        PanelForms
            .Of(false, density, false, dockOpen, searchPeek: true)
            .ShouldBe(PanelForm.Hidden);
        PanelForms
            .Of(true, PanelDensity.Dock, false, dockOpen)
            .ShouldBe(dockOpen ? PanelForm.DockOpen : PanelForm.DockClosed);
    }

    [Theory]
    [InlineData(PanelForm.DockOpen, true, false, false, true)]
    [InlineData(PanelForm.DockClosed, true, false, false, true)]
    [InlineData(PanelForm.DockClosed, false, true, false, true)]
    [InlineData(PanelForm.DockOpen, false, false, true, true)]
    [InlineData(PanelForm.DockOpen, false, false, false, false)]
    [InlineData(PanelForm.Full, true, true, true, false)]
    [InlineData(PanelForm.Bubble, true, false, false, false)]
    [InlineData(PanelForm.Hidden, true, false, false, false)]
    [Trait("Req", "PES-014")]
    public void The_notice_surface_shows_in_the_Tab_view_while_there_is_something_to_tell(
        PanelForm form,
        bool notice,
        bool adminNotice,
        bool testMode,
        bool expected
    ) => DockRules.ShowsNotices(form, notice, adminNotice, testMode).ShouldBe(expected);

    [Theory]
    [InlineData(PanelForm.DockOpen, true, true)]
    [InlineData(PanelForm.DockOpen, false, false)]
    [InlineData(PanelForm.DockClosed, true, false)]
    [InlineData(PanelForm.Full, true, false)]
    [Trait("Req", "PES-009")]
    [Trait("Req", "CUA-014")]
    public void Quick_settings_and_the_menu_show_beside_the_bar_only_while_it_is_open(
        PanelForm form,
        bool open,
        bool expected
    ) => DockRules.ShowsBesideBar(form, open).ShouldBe(expected);

    /// <summary>What a button did, the lock of the bar, and whether the bar collapses after it.</summary>
    public static TheoryData<DockUse, bool, bool> Collapses =>
        new()
        {
            { DockUse.Ran, false, true },
            { DockUse.HoldReleased, false, true },
            { DockUse.ToggleChanged, false, false },
            { DockUse.ConfirmArmed, false, false },
            { DockUse.Ran, true, false },
            { DockUse.HoldReleased, true, false },
            { DockUse.ToggleChanged, true, false },
            { DockUse.ConfirmArmed, true, false },
        };

    [Theory]
    [MemberData(nameof(Collapses))]
    [Trait("Req", "PES-012")]
    public void The_bar_collapses_after_an_action_unless_it_is_kept_open(
        DockUse use,
        bool pinOpen,
        bool expected
    ) => DockRules.CollapsesAfter(use, pinOpen).ShouldBe(expected);

    [Theory]
    [InlineData(true, 3, true)]
    [InlineData(true, 0, false)]
    [InlineData(false, 3, false)]
    [Trait("Req", "PES-008")]
    public void Pinned_shows_with_the_row_on_and_some_shortcut_in_it(
        bool row,
        int count,
        bool expected
    ) => DockRules.ShowsPinned(row, count).ShouldBe(expected);

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [Trait("Req", "PES-010")]
    public void Pinned_closes_after_use_unless_the_shortcut_holds_or_latches(
        bool holds,
        bool closes
    ) => DockRules.SideWindowClosesAfterUse(holds).ShouldBe(closes);

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [Trait("Req", "PES-015")]
    public void The_guide_shows_on_the_open_bar_until_it_is_done_and_without_side_windows(
        bool open,
        bool done,
        bool sideWindow,
        bool expected
    ) => DockRules.ShowsCoach(open, done, sideWindow).ShouldBe(expected);

    [Fact]
    [Trait("Req", "PES-015")]
    public void The_guide_has_three_steps()
    {
        DockRules.NextCoachStep(0).ShouldBe(1);
        DockRules.NextCoachStep(1).ShouldBe(2);
        DockRules.NextCoachStep(2).ShouldBeNull();
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [Trait("Req", "VCO-002")]
    public void The_compact_bottom_row_shows_in_Compact_without_search_text(
        bool compact,
        bool searching,
        bool expected
    ) =>
        CompactRowRules
            .Visible(PanelLayoutSettings.Default with { Compact = compact }, searching)
            .ShouldBe(expected);

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(3, false, true)]
    [InlineData(3, true, false)]
    [Trait("Req", "VCO-002")]
    public void Its_arrows_and_dots_show_with_several_pages(int pages, bool empty, bool expected) =>
        CompactRowRules.ShowsPager(pages, empty).ShouldBe(expected);
}
