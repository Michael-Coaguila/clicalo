using Clicalo.Domain.Document;
using Clicalo.Domain.Settings;
using PanelSize = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Domain.Tests.Settings;

/// <summary>
/// The settings of schema 1.1 (ADR-0028): defaults, scopes, the repair of each value and the helpers of the handle
/// position per monitor and of the welcome answers.
/// </summary>
public sealed class M6SettingsTests
{
    private const string Monitor =
        @"\\?\DISPLAY#AAA0001#1&1&0&UID1#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string Other =
        @"\\?\DISPLAY#BBB0002#1&2&0&UID2#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    [Fact]
    [Trait("Req", "PES-016")]
    [Trait("Req", "CCM-001")]
    [Trait("Req", "BUR-005")]
    [Trait("Req", "ACC-006")]
    public void The_defaults_remember_nothing_keep_the_shortcut_off_and_the_times_as_they_are()
    {
        var defaults = SettingsSchema.Defaults;

        defaults.HandlePositionsByMonitor.ShouldBeEmpty();
        defaults.ControlCenter.ShouldBeNull();
        defaults.GlobalHotkey.Enabled.ShouldBeFalse();
        defaults.TimeMultiplier.ShouldBe(1);
        SettingsSchema.TimeMultiplier.ShouldBe(new SettingRange(1, 3, 1));
        SettingsSchema.Find(SettingPaths.ControlCenter)!.Default.ShouldBe(SettingsSchema.NoValue);
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public void Positions_are_placement_and_the_shortcut_and_the_times_are_undoable()
    {
        Scope(SettingPaths.HandlePositionsByMonitor).ShouldBe((SettingScope.Placement, false));
        Scope(SettingPaths.ControlCenter).ShouldBe((SettingScope.Placement, false));
        Scope(SettingPaths.GlobalHotkeyEnabled).ShouldBe((SettingScope.Behavior, true));
        Scope(SettingPaths.GlobalHotkeyCombo).ShouldBe((SettingScope.Behavior, true));
        Scope(SettingPaths.TimeMultiplier).ShouldBe((SettingScope.Behavior, true));

        var before = SettingsSchema.Defaults;
        var now = before with
        {
            TimeMultiplier = 3,
            ControlCenter = new ControlCenterPlacement(Monitor, 0, 0, 900, 600, false),
        };
        var restored = SettingsSchema.WithUndoableFrom(now, before);
        restored.TimeMultiplier.ShouldBe(1);
        restored.ControlCenter.ShouldBe(now.ControlCenter);
    }

    [Fact]
    [Trait("Req", "ACC-006")]
    public void The_time_multiplier_is_one_two_or_three()
    {
        var defaults = SettingsSchema.Defaults;

        SettingsSchema
            .Write(defaults, SettingPaths.TimeMultiplier, 3, out var triple)
            .ShouldBe(SettingWriteStatus.Written);
        triple.TimeMultiplier.ShouldBe(3);
        SettingsSchema
            .Write(defaults, SettingPaths.TimeMultiplier, 4, out _)
            .ShouldBe(SettingWriteStatus.OutOfRange);
        SettingsSchema
            .Write(defaults, SettingPaths.TimeMultiplier, 0, out _)
            .ShouldBe(SettingWriteStatus.OutOfRange);
        SettingsSchema
            .Clamp(defaults with { TimeMultiplier = 9 }, out var changed)
            .TimeMultiplier.ShouldBe(3);
        changed.ShouldBe([SettingPaths.TimeMultiplier]);
    }

    [Fact]
    [Trait("Req", "CCM-001")]
    public void An_unusable_control_center_placement_is_forgotten()
    {
        var usable = new ControlCenterPlacement(Monitor, -1500.5, 20, 1120, 680, Maximized: true);
        var defaults = SettingsSchema.Defaults;

        SettingsSchema
            .Write(defaults, SettingPaths.ControlCenter, usable, out var written)
            .ShouldBe(SettingWriteStatus.Written);
        written.ControlCenter.ShouldBe(usable);
        SettingsSchema
            .Write(written, SettingPaths.ControlCenter, null, out var cleared)
            .ShouldBe(SettingWriteStatus.Written);
        cleared.ControlCenter.ShouldBeNull();
        foreach (
            var broken in new[]
            {
                usable with
                {
                    MonitorId = string.Empty,
                },
                usable with
                {
                    Width = 0,
                },
                usable with
                {
                    Height = -1,
                },
                usable with
                {
                    X = double.NaN,
                },
                usable with
                {
                    Y = double.PositiveInfinity,
                },
            }
        )
        {
            SettingsSchema
                .Write(defaults, SettingPaths.ControlCenter, broken, out _)
                .ShouldBe(SettingWriteStatus.OutOfRange);
            SettingsSchema
                .Clamp(defaults with { ControlCenter = broken }, out var changed)
                .ControlCenter.ShouldBeNull();
            changed.ShouldBe([SettingPaths.ControlCenter]);
        }
    }

    [Fact]
    [Trait("Req", "PES-016")]
    public void The_handle_position_per_monitor_falls_back_to_the_one_per_side()
    {
        var settings = SettingsSchema.Defaults with
        {
            Dock = SettingsSchema.Defaults.Dock with
            {
                HandlePositions = new DockHandlePositions(40, 41, 42, 43),
            },
            HandlePositionsByMonitor = [new MonitorHandlePosition(Monitor, DockSide.Left, 20)],
        };

        MonitorHandlePositions.PositionFor(settings, Monitor, DockSide.Left).ShouldBe(20);
        MonitorHandlePositions.PositionFor(settings, Monitor, DockSide.Top).ShouldBe(42);
        MonitorHandlePositions.PositionFor(settings, Other, DockSide.Left).ShouldBe(41);
        MonitorHandlePositions.PositionFor(settings, null, DockSide.Bottom).ShouldBe(43);
        MonitorHandlePositions.PositionFor(settings, Other, DockSide.Right).ShouldBe(40);
    }

    [Fact]
    [Trait("Req", "PES-016")]
    public void Setting_a_handle_position_replaces_the_entry_of_that_monitor_and_side()
    {
        var one = MonitorHandlePositions.With([], Monitor, DockSide.Right, 30);
        var two = MonitorHandlePositions.With(one, Other, DockSide.Right, 60);
        var moved = MonitorHandlePositions.With(two, Monitor, DockSide.Right, 80);

        moved.ShouldBe([
            new MonitorHandlePosition(Monitor, DockSide.Right, 80),
            new MonitorHandlePosition(Other, DockSide.Right, 60),
        ]);
        MonitorHandlePositions.With(moved, Monitor, DockSide.Right, 80).ShouldBe(moved);
        Should.Throw<ArgumentException>(() =>
            MonitorHandlePositions.With(moved, string.Empty, DockSide.Top, 50)
        );
        SettingsSchema
            .Write(SettingsSchema.Defaults, SettingPaths.HandlePositionsByMonitor, moved, out var s)
            .ShouldBe(SettingWriteStatus.Written);
        s.HandlePositionsByMonitor.ShouldBe(moved);
    }

    [Fact]
    [Trait("Req", "PES-016")]
    [Trait("Req", "DAT-003")]
    public void Handle_positions_are_clamped_and_empty_unknown_or_repeated_entries_dropped()
    {
        var settings = SettingsSchema.Defaults with
        {
            HandlePositionsByMonitor =
            [
                new MonitorHandlePosition(Monitor, DockSide.Top, 99),
                new MonitorHandlePosition(Monitor, DockSide.Top, 50),
                new MonitorHandlePosition(string.Empty, DockSide.Top, 50),
                new MonitorHandlePosition(Other, (DockSide)9, 50),
                new MonitorHandlePosition(Other, DockSide.Bottom, 3),
            ],
        };

        var repaired = SettingsSchema.Clamp(settings, out var changed);

        repaired.HandlePositionsByMonitor.ShouldBe([
            new MonitorHandlePosition(Monitor, DockSide.Top, 92),
            new MonitorHandlePosition(Other, DockSide.Bottom, 8),
        ]);
        changed.ShouldBe([SettingPaths.HandlePositionsByMonitor]);
        SettingsSchema
            .Write(
                SettingsSchema.Defaults,
                SettingPaths.HandlePositionsByMonitor,
                settings.HandlePositionsByMonitor,
                out _
            )
            .ShouldBe(SettingWriteStatus.OutOfRange);
    }

    [Fact]
    [Trait("Req", "DAT-003")]
    public void A_missing_global_shortcut_group_takes_its_defaults()
    {
        var broken = SettingsSchema.Defaults with { GlobalHotkey = null! };

        var repaired = SettingsSchema.Clamp(broken, out var changed);

        changed.ShouldBe(["globalHotkey"]);
        repaired.ShouldBe(SettingsSchema.Defaults);
    }

    [Fact]
    [Trait("Req", "BIE-010")]
    public void Welcome_answers_are_kept_in_canonical_form()
    {
        var baseline = WelcomeBaseline.Of(SettingsSchema.Defaults);

        var answers = WelcomeAnswers.Create(
            [WelcomeAnswer.Mouse, WelcomeAnswer.Touch, WelcomeAnswer.Mouse, (WelcomeAnswer)42],
            ["word", "", "basics", "word"],
            baseline
        );

        answers.Uses.ShouldBe([WelcomeAnswer.Touch, WelcomeAnswer.Mouse]);
        answers.Kit.ShouldBe(["basics", "word"]);
        answers.ShouldBe(
            WelcomeAnswers.Create(
                [WelcomeAnswer.Touch, WelcomeAnswer.Mouse],
                ["basics", "word"],
                baseline
            )
        );
        baseline.ShouldBe(
            new WelcomeBaseline(
                SettingsSchema.Defaults.Touch.Preset,
                PanelSize.Medium,
                VoiceNumbers: false,
                NoKeyboardUser: false
            )
        );
        new OnboardingState(Completed: false).Answers.ShouldBeNull();
    }

    private static (SettingScope, bool) Scope(string path)
    {
        var descriptor = SettingsSchema.Find(path)!;
        return (descriptor.Scope, descriptor.Undoable);
    }
}
