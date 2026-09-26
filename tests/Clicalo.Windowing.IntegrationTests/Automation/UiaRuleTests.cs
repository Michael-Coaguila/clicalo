using System.Windows;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;
using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// Each UIA rule of the verifier fails on the tree it exists to reject and passes on a correct one, so a green
/// verifier means something (blueprint §10.2).
/// </summary>
public sealed class UiaRuleTests
{
    private static readonly UiaNode GoodTile = new()
    {
        AutomationId = "tile.bold",
        Name = "4 Negrita",
        ControlType = ControlType.Button,
        Patterns = UiaPatterns.Invoke,
        HelpText = "Ctrl + N",
        Bounds = new Rect(0, 0, 92, 78),
    };

    private static readonly UiaExpectation BoldExpected = new(
        "tile.bold",
        "Negrita",
        4,
        ControlType.Button,
        UiaPatterns.Invoke
    )
    {
        HelpText = "Ctrl + N",
    };

    [Fact]
    [Trait("Req", "REG-06")]
    public void A_correct_tree_passes_every_rule() => Passes(Root(GoodTile), [BoldExpected]);

    [Fact]
    [Trait("Req", "ACC-009")]
    public void UIA001_rejects_a_missing_number_an_empty_name_and_a_missing_element()
    {
        Fails("UIA001", Root(GoodTile with { Name = "Negrita" }), [BoldExpected]);
        Fails("UIA001", Root(GoodTile with { Name = string.Empty }), [BoldExpected]);
        Fails(
            "UIA001",
            Root(GoodTile with { AutomationId = "other", Name = "Otra" }),
            [BoldExpected]
        );
        Passes(
            Root(GoodTile with { Name = "Negrita" }),
            [BoldExpected with { VoiceNumber = null }]
        );
    }

    [Fact]
    [Trait("Req", "REG-06")]
    public void UIA002_rejects_a_wrong_role_and_an_actionable_text()
    {
        Fails("UIA002", Root(GoodTile with { ControlType = ControlType.CheckBox }), [BoldExpected]);
        Fails(
            "UIA002",
            Root(
                new UiaNode
                {
                    AutomationId = "label",
                    Name = "Listo",
                    ControlType = ControlType.Text,
                    Patterns = UiaPatterns.Invoke,
                    Bounds = new Rect(0, 0, 60, 60),
                }
            ),
            []
        );
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    public void UIA003_rejects_extra_patterns_and_buttons_with_two_meanings_or_none()
    {
        Fails(
            "UIA003",
            Root(
                GoodTile with
                {
                    Patterns = UiaPatterns.Invoke | UiaPatterns.Toggle,
                    ToggleState = ToggleState.Off,
                }
            ),
            [BoldExpected]
        );
        Fails(
            "UIA003",
            Root(
                GoodTile with
                {
                    AutomationId = "free",
                    Name = "Libre",
                    Patterns = UiaPatterns.Invoke | UiaPatterns.ExpandCollapse,
                    ExpandCollapseState = ExpandCollapseState.Collapsed,
                }
            ),
            []
        );
        Fails(
            "UIA003",
            Root(
                GoodTile with
                {
                    AutomationId = "bare",
                    Name = "Nada",
                    Patterns = UiaPatterns.None,
                }
            ),
            []
        );
    }

    [Fact]
    [Trait("Req", "ACC-003")]
    public void UIA004_rejects_states_that_differ_from_the_view_model()
    {
        var shift = new UiaNode
        {
            AutomationId = "tile.shift",
            Name = "Mayús",
            ControlType = ControlType.Button,
            Patterns = UiaPatterns.Toggle,
            ToggleState = ToggleState.On,
            ItemStatus = "ACTIVO",
            Bounds = new Rect(0, 0, 92, 78),
        };
        var expected = new UiaExpectation(
            "tile.shift",
            "Mayús",
            null,
            ControlType.Button,
            UiaPatterns.Toggle
        )
        {
            ToggleState = ToggleState.On,
            ItemStatus = "ACTIVO",
        };

        Passes(Root(shift), [expected]);
        Fails("UIA004", Root(shift with { ToggleState = ToggleState.Indeterminate }), [expected]);
        Fails("UIA004", Root(shift with { ItemStatus = string.Empty }), [expected]);
        Fails("UIA004", Root(shift with { ToggleState = null }), []);
        Fails("UIA004", Root(GoodTile with { HelpText = "Ctrl + B" }), [BoldExpected]);
    }

    [Fact]
    [Trait("Req", "REG-02")]
    public void UIA005_rejects_targets_under_44_logical_pixels_at_the_monitor_scale()
    {
        Fails("UIA005", Root(GoodTile with { Bounds = new Rect(0, 0, 43, 60) }), [BoldExpected]);
        Fails(
            "UIA005",
            Root(GoodTile with { Bounds = new Rect(0, 0, 60, 60), Scale = 1.5 }),
            [BoldExpected]
        );
        Fails("UIA005", Root(GoodTile with { Bounds = Rect.Empty }), [BoldExpected]);
        Passes(
            Root(GoodTile with { Bounds = new Rect(0, 0, 66, 66), Scale = 1.5 }),
            [BoldExpected]
        );
        Passes(
            Root(GoodTile with { Bounds = new Rect(0, 0, 10, 10), IsOffscreen = true }),
            [BoldExpected]
        );
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    public void UIA006_rejects_a_notice_region_that_is_not_live()
    {
        var notice = new UiaNode
        {
            AutomationId = "notice",
            Name = "Listo",
            ControlType = ControlType.Text,
            LiveSetting = LiveSetting.Off,
        };
        var expected = new UiaExpectation(
            "notice",
            "Listo",
            null,
            ControlType.Text,
            UiaPatterns.None
        )
        {
            LiveSetting = LiveSetting.Polite,
        };

        Fails("UIA006", Root(notice), [expected]);
        Passes(Root(notice with { LiveSetting = LiveSetting.Polite }), [expected]);
    }

    [Theory]
    [Trait("Req", "ACC-001")]
    [InlineData("←")]
    [InlineData("4 ★")]
    [InlineData("")]
    [InlineData("Borrar ")]
    public void UIA008_rejects_glyphs_as_names(string name) =>
        Fails("UIA008", Root(GoodTile with { AutomationId = "glyph", Name = name }), []);

    [Fact]
    [Trait("Req", "ACC-011")]
    [Trait("Req", "REG-05")]
    public void UIA010_requires_a_dictation_button_next_to_each_free_text_field()
    {
        var field = new UiaNode
        {
            AutomationId = "search",
            Name = "Buscar",
            ControlType = ControlType.Edit,
            Patterns = UiaPatterns.Value,
        };
        var dictate = GoodTile with
        {
            AutomationId = "dictate",
            Name = "3 Dictar",
            HelpText = string.Empty,
        };
        var expectations = UiaExpectations.For([]) with { DictationNames = ["Dictar", "Pegar"] };

        RuleIds(UiaVerifier.Verify(Root(field), expectations)).ShouldBe("UIA010");
        UiaVerifier.Verify(Root(field, dictate), expectations).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void UIA009_reports_a_foreground_change_an_active_surface_and_a_guard_violation()
    {
        var foreground = ForegroundWindows.Current;

        ForegroundInvariant.Check("invoke", foreground, 1, false, [], 0, 0).ShouldBeEmpty();
        var broken = ForegroundInvariant.Check("invoke", foreground + 1, 1, true, [], 1, 1);
        broken.Count.ShouldBe(3);
        RuleIds(broken).ShouldBe(ForegroundInvariant.RuleId);

        var deactivation = ProbeEventParser.Parse(
            """{"kind":"activate","seq":1,"qpc":1,"fg":1,"msg":6,"name":"WM_ACTIVATE","time":0,"wParam":0,"lParam":0,"extra":0,"state":0,"minimized":false,"other":0}"""
        );
        ForegroundInvariant
            .Check("invoke", foreground, 1, false, [deactivation], 0, 0)
            .ShouldHaveSingleItem()
            .Message.ShouldStartWith("InputProbe received WM_ACTIVATE");
    }

    private static UiaNode Root(params UiaNode[] children) =>
        new()
        {
            Name = "Panel",
            ControlType = ControlType.Window,
            Children = [.. children],
        };

    private static void Passes(UiaNode root, IEnumerable<UiaExpectation> expected) =>
        UiaVerifier.Verify(root, UiaExpectations.For(expected)).ShouldBeEmpty();

    private static void Fails(string ruleId, UiaNode root, IEnumerable<UiaExpectation> expected) =>
        RuleIds(UiaVerifier.Verify(root, UiaExpectations.For(expected))).ShouldBe(ruleId);

    private static string RuleIds(IEnumerable<UiaViolation> violations) =>
        string.Join(
            ",",
            violations
                .Select(violation => violation.RuleId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        );
}
