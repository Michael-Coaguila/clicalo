using Clicalo.Domain.PanelLayout;

namespace Clicalo.Domain.Tests.PanelLayout;

/// <summary>
/// <see cref="BodyLayerRules"/>, <see cref="CrampedRule"/> and <see cref="SelectorRules"/>: the deterministic visibility
/// of PAN-008 «Acepta», tested without an interface.
/// </summary>
public sealed class BodyLayerRulesTests
{
    private static readonly BodyLayerInputs Idle = new(
        PanelLayoutSettings.Default,
        SearchingWithText: false,
        Cramped: false,
        Frequents: false,
        PickerOpen: false,
        ListCount: 9,
        PageCount: 1,
        HasNotice: false,
        CanRepeat: false,
        EditMode: false,
        ElevatedTarget: false
    );

    /// <summary>Was cramped, alert, measured space, expected (tile 78).</summary>
    public static TheoryData<bool, bool, double?, bool> Cramped =>
        new()
        {
            { false, false, 10, false },
            { true, false, 10, false },
            { false, true, null, false },
            { false, true, 78, true },
            { false, true, 82, false },
            { false, true, 81.9, true },
            { false, true, 10, true },
            { true, true, 500, true },
        };

    [Fact]
    [Trait("Req", "PAN-007")]
    public void By_default_the_full_view_shows_the_rows_the_selector_and_the_notice_bar()
    {
        var layers = BodyLayerRules.Evaluate(Idle);

        layers.ShouldBe(
            new BodyLayers(
                AdminNotice: false,
                AlwaysVisibleRow: true,
                AlwaysVisibleLabel: true,
                StickyRow: false,
                Selector: true,
                PickerGrid: false,
                EmptyProfile: false,
                Pager: false,
                NoticeBar: true,
                Repeat: false
            )
        );
    }

    [Fact]
    [Trait("Req", "PAN-008")]
    [Trait("Req", "FIJ-001")]
    [Trait("Req", "FIJ-005")]
    [Trait("Req", "SEL-001")]
    public void Searching_with_text_hides_the_rows_the_selector_and_the_profile_grid()
    {
        var layers = BodyLayerRules.Evaluate(
            Idle with
            {
                SearchingWithText = true,
                PickerOpen = true,
                ListCount = 0,
                Settings = PanelLayoutSettings.Default with { StickyRow = true },
            }
        );

        layers.AlwaysVisibleRow.ShouldBeFalse();
        layers.StickyRow.ShouldBeFalse();
        layers.Selector.ShouldBeFalse();
        layers.PickerGrid.ShouldBeFalse();
        layers.EmptyProfile.ShouldBeFalse();
        layers.NoticeBar.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "CUA-003")]
    public void Lack_of_space_with_an_alert_hides_the_Always_visible_row_and_the_selector_only()
    {
        var layers = BodyLayerRules.Evaluate(
            Idle with
            {
                Cramped = true,
                ElevatedTarget = true,
                Settings = PanelLayoutSettings.Default with { StickyRow = true },
            }
        );

        layers.AlwaysVisibleRow.ShouldBeFalse();
        layers.Selector.ShouldBeFalse();
        layers.StickyRow.ShouldBeTrue();
        layers.AdminNotice.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Cramped))]
    [Trait("Req", "CUA-003")]
    public void The_rows_hide_when_a_whole_row_does_not_fit_and_stay_hidden_while_the_alert_lasts(
        bool wasCramped,
        bool alert,
        double? space,
        bool expected
    ) => CrampedRule.Evaluate(wasCramped, alert, space, 78).ShouldBe(expected);

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, true, true)]
    [Trait("Req", "VCO-001")]
    [Trait("Req", "AVI-001")]
    public void In_Compact_the_notice_bar_shows_only_with_a_notice_or_something_to_repeat(
        bool compact,
        bool notice,
        bool repeat,
        bool shows
    )
    {
        var layers = BodyLayerRules.Evaluate(
            Idle with
            {
                Settings = PanelLayoutSettings.Default with { Compact = compact },
                HasNotice = notice,
                CanRepeat = repeat,
            }
        );

        layers.NoticeBar.ShouldBe(!compact || shows);
    }

    [Fact]
    [Trait("Req", "VCO-001")]
    [Trait("Req", "FIJ-001")]
    public void Compact_has_no_strip_label_no_selector_and_no_pager_under_the_grid()
    {
        var layers = BodyLayerRules.Evaluate(
            Idle with
            {
                Settings = PanelLayoutSettings.Default with { Compact = true },
                PageCount = 3,
            }
        );

        layers.AlwaysVisibleRow.ShouldBeTrue();
        layers.AlwaysVisibleLabel.ShouldBeFalse();
        layers.Selector.ShouldBeFalse();
        layers.Pager.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, false, 0, true)]
    [InlineData(true, false, 0, false)]
    [InlineData(false, true, 0, false)]
    [InlineData(false, false, 1, false)]
    [Trait("Req", "CUA-010")]
    public void An_empty_profile_shows_its_card_outside_Frequents_and_the_search(
        bool frequents,
        bool searching,
        int count,
        bool empty
    ) =>
        BodyLayerRules
            .Evaluate(
                Idle with
                {
                    Frequents = frequents,
                    SearchingWithText = searching,
                    ListCount = count,
                }
            )
            .EmptyProfile.ShouldBe(empty);

    [Fact]
    [Trait("Req", "CUA-004")]
    [Trait("Req", "AVI-004")]
    public void The_pager_needs_two_pages_and_Repeat_hides_in_edit_mode()
    {
        BodyLayerRules.Evaluate(Idle with { PageCount = 2 }).Pager.ShouldBeTrue();
        BodyLayerRules.Evaluate(Idle with { CanRepeat = true }).Repeat.ShouldBeTrue();
        BodyLayerRules
            .Evaluate(Idle with { CanRepeat = true, EditMode = true })
            .Repeat.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, false, SelectorCaret.Expand, SelectorLook.Active, SelectorTap.TogglePicker)]
    [InlineData(false, true, SelectorCaret.Collapse, SelectorLook.Open, SelectorTap.TogglePicker)]
    [InlineData(
        true,
        false,
        SelectorCaret.Return,
        SelectorLook.Frequents,
        SelectorTap.ReturnFromFrequents
    )]
    [Trait("Req", "SEL-001")]
    [Trait("Req", "SEL-002")]
    public void The_profile_button_ends_in_its_caret_and_returns_from_Frequents_in_one_tap(
        bool frequents,
        bool open,
        SelectorCaret caret,
        SelectorLook look,
        SelectorTap tap
    )
    {
        SelectorRules.Caret(frequents, open).ShouldBe(caret);
        SelectorRules.Look(frequents, open).ShouldBe(look);
        SelectorRules.Tap(frequents).ShouldBe(tap);
    }

    [Theory]
    [InlineData(276, false, 3)]
    [InlineData(200, false, 2)]
    [InlineData(87, false, 1)]
    [InlineData(276, true, 3)]
    [InlineData(172, true, 2)]
    [InlineData(double.NaN, false, 1)]
    [Trait("Req", "SEL-003")]
    public void The_profile_grid_fits_as_many_columns_of_88_or_80_as_its_width_allows(
        double width,
        bool compact,
        int columns
    ) => PickerLayout.Columns(width, compact).ShouldBe(columns);
}
