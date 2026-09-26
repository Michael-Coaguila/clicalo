using Clicalo.Domain.Touch;
using Clicalo.TestKit.Time;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>
/// The single filter of TAC-002 as a table for the four configurations of TAC-001 (Standard 150/8/45/0, Mild tremor
/// 300/14/35/0, Strong tremor 600/24/28/80 and a Personal one with the cancel distance off).
/// </summary>
[Trait("Req", "TAC-001")]
[Trait("Req", "TAC-002")]
public sealed class TouchFilterTests
{
    private static readonly DateTimeOffset Now = TestTime.Epoch + TimeSpan.FromSeconds(10);

    /// <summary>
    /// preset · duration (ms) · displacement (logical px) · palm · time since the last accepted touch on the same
    /// target (ms; null when none) · verdict.
    /// </summary>
    public static TheoryData<string, int, double, bool, int?, TouchVerdict> Table =>
        new()
        {
            // Standard: debounce 150 ms, cancel 45 px, no minimum contact.
            { "standard", 40, 0, false, null, TouchVerdict.Accepted },
            { "standard", 1, 0, false, null, TouchVerdict.Accepted },
            { "standard", 40, 45, false, null, TouchVerdict.Accepted },
            { "standard", 40, 45.5, false, null, TouchVerdict.IgnoredSwipe },
            { "standard", 40, 0, false, 149, TouchVerdict.IgnoredDouble },
            { "standard", 40, 0, false, 150, TouchVerdict.Accepted },
            { "standard", 40, 0, true, null, TouchVerdict.IgnoredPalm },
            // Mild tremor (default): debounce 300 ms, cancel 35 px, no minimum contact.
            { "mild-tremor", 40, 35, false, null, TouchVerdict.Accepted },
            { "mild-tremor", 40, 36, false, null, TouchVerdict.IgnoredSwipe },
            { "mild-tremor", 40, 0, false, 299, TouchVerdict.IgnoredDouble },
            { "mild-tremor", 40, 0, false, 300, TouchVerdict.Accepted },
            { "mild-tremor", 40, 0, true, 1000, TouchVerdict.IgnoredPalm },
            // Strong tremor: debounce 600 ms, cancel 28 px, minimum contact 80 ms.
            { "strong-tremor", 80, 0, false, null, TouchVerdict.Accepted },
            { "strong-tremor", 79, 0, false, null, TouchVerdict.IgnoredShort },
            { "strong-tremor", 80, 28, false, null, TouchVerdict.Accepted },
            { "strong-tremor", 80, 29, false, null, TouchVerdict.IgnoredSwipe },
            { "strong-tremor", 80, 0, false, 599, TouchVerdict.IgnoredDouble },
            { "strong-tremor", 80, 0, false, 600, TouchVerdict.Accepted },
            // The order of TAC-002: palm, then movement (1), then duration (2), then debounce (3).
            { "strong-tremor", 10, 29, true, 100, TouchVerdict.IgnoredPalm },
            { "strong-tremor", 10, 29, false, 100, TouchVerdict.IgnoredSwipe },
            { "strong-tremor", 10, 0, false, 100, TouchVerdict.IgnoredShort },
            // Personal: debounce 450 ms, extra area 20 px, cancel distance off (0), minimum contact 120 ms.
            { "personal", 120, 500, false, null, TouchVerdict.Accepted },
            { "personal", 119, 0, false, null, TouchVerdict.IgnoredShort },
            { "personal", 120, 0, false, 449, TouchVerdict.IgnoredDouble },
            { "personal", 120, 0, false, 450, TouchVerdict.Accepted },
        };

    [Theory]
    [MemberData(nameof(Table))]
    public void Each_preset_gives_the_verdict_of_its_table(
        string preset,
        int durationMs,
        double displacementPx,
        bool palm,
        int? sinceAcceptedMs,
        TouchVerdict expected
    )
    {
        var settings = TouchPresetSettings.Get(preset);
        var state = new ButtonFilterState();
        if (sinceAcceptedMs is { } since)
        {
            state.LastAccepted = Now - TimeSpan.FromMilliseconds(since);
        }

        var before = state.LastAccepted;
        var contact = new ContactSummary(
            TimeSpan.FromMilliseconds(durationMs),
            displacementPx,
            palm
        );

        var verdict = TouchFilter.Evaluate(ref state, contact, settings, Now);

        verdict.ShouldBe(expected);
        state.LastAccepted.ShouldBe(expected == TouchVerdict.Accepted ? Now : before);
    }

    [Fact]
    [Trait("Req", "ACC-007")]
    public void A_palm_is_never_accepted_whatever_the_settings()
    {
        var state = new ButtonFilterState();
        var off = new TouchSettings(TimeSpan.Zero, 0, 0, TimeSpan.Zero);

        TouchFilter
            .Evaluate(ref state, new ContactSummary(TimeSpan.FromSeconds(1), 0, true), off, Now)
            .ShouldBe(TouchVerdict.IgnoredPalm);
        state.LastAccepted.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void An_ignored_touch_does_not_restart_the_debounce_window(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var state = new ButtonFilterState();
        var tap = new ContactSummary(settings.MinContact, 0, false);
        var first = Now;
        TouchFilter.Evaluate(ref state, tap, settings, first).ShouldBe(TouchVerdict.Accepted);

        // Two bounces inside the window: both ignored, and neither moves the start of the window.
        var bounce = first + (settings.Debounce / 2);
        TouchFilter.Evaluate(ref state, tap, settings, bounce).ShouldBe(TouchVerdict.IgnoredDouble);
        var lastBounce = first + settings.Debounce - TimeSpan.FromMilliseconds(1);
        TouchFilter
            .Evaluate(ref state, tap, settings, lastBounce)
            .ShouldBe(TouchVerdict.IgnoredDouble);
        state.LastAccepted.ShouldBe(first);

        // The window ends counted from the ACCEPTED touch, not from the bounces.
        TouchFilter
            .Evaluate(ref state, tap, settings, first + settings.Debounce)
            .ShouldBe(TouchVerdict.Accepted);
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    public void The_debounce_of_one_target_never_blocks_another(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var first = new ButtonFilterState();
        var second = new ButtonFilterState();
        var tap = new ContactSummary(settings.MinContact, 0, false);

        TouchFilter.Evaluate(ref first, tap, settings, Now).ShouldBe(TouchVerdict.Accepted);
        TouchFilter.Evaluate(ref second, tap, settings, Now).ShouldBe(TouchVerdict.Accepted);
        TouchFilter
            .Evaluate(ref first, tap, settings, Now + TimeSpan.FromMilliseconds(1))
            .ShouldBe(TouchVerdict.IgnoredDouble);
    }

    [Fact]
    public void Zero_switches_each_check_off()
    {
        var off = new TouchSettings(TimeSpan.Zero, 0, 0, TimeSpan.Zero);
        var state = new ButtonFilterState { LastAccepted = Now };
        var wild = new ContactSummary(TimeSpan.Zero, 10_000, false);

        TouchFilter.Evaluate(ref state, wild, off, Now).ShouldBe(TouchVerdict.Accepted);
        TouchFilter.Evaluate(ref state, wild, off, Now).ShouldBe(TouchVerdict.Accepted);
    }

    [Theory]
    [MemberData(nameof(TouchPresetSettings.All), MemberType = typeof(TouchPresetSettings))]
    [Trait("Req", "EJE-004")]
    public void The_debounce_applies_to_the_start_of_a_hold(string preset)
    {
        var settings = TouchPresetSettings.Get(preset);
        var state = new ButtonFilterState();

        TouchFilter.CanStartHold(state, settings, Now).ShouldBeTrue();

        state.LastAccepted = Now;
        TouchFilter
            .CanStartHold(state, settings, Now + settings.Debounce - TimeSpan.FromMilliseconds(1))
            .ShouldBeFalse();
        TouchFilter.CanStartHold(state, settings, Now + settings.Debounce).ShouldBeTrue();
    }
}
