using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Tests.Execution.Support;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// The single activation flow (EJE-001, blueprint §7.2): one table of inputs and outputs, the same for every origin
/// (TAC-002), in the normative order filter → edit → test → elevation → incomplete or blocked → confirmation → execute.
/// </summary>
[Trait("Req", "EJE-001")]
public sealed class ActivationPolicyTests
{
    private static readonly DateTimeOffset Now = EngineHarness.Start;
    private static readonly Shortcut Copy = Shortcuts.Tap("copy", "ctrl", "c");

    private static ActivationContext Context(
        Shortcut? shortcut = null,
        ActivationPhase phase = ActivationPhase.ContactEnded,
        ActivationOrigin origin = ActivationOrigin.Touch,
        ContactSummary? contact = null,
        bool editMode = false,
        bool testMode = false,
        ElevationState elevation = ElevationState.Allowed,
        ArmedConfirmation? armed = null,
        ButtonFilterState filter = default
    ) =>
        new(
            new ActivationRequest(
                phase,
                origin,
                phase == ActivationPhase.Invoke ? null : 7,
                phase == ActivationPhase.ContactEnded
                    ? contact ?? new ContactSummary(TimeSpan.FromMilliseconds(100), 0, false)
                    : null,
                Now
            ),
            shortcut ?? Copy,
            OriginProfile: null,
            InjectionMode.VirtualKey,
            LastExternalPointer: null,
            editMode,
            testMode,
            elevation,
            armed,
            filter,
            EngineHarness.Touch,
            Now
        );

    private static ActivationDecision Decide(in ActivationContext context) =>
        ActivationPolicy.Decide(context).Decision;

    public static TheoryData<ActivationOrigin> Origins =>
        [
            ActivationOrigin.Touch,
            ActivationOrigin.Pen,
            ActivationOrigin.Mouse,
            ActivationOrigin.UiaInvoke,
            ActivationOrigin.Keyboard,
            ActivationOrigin.Repeat,
            ActivationOrigin.TryNow,
        ];

    [Theory]
    [MemberData(nameof(Origins))]
    [Trait("Req", "TAC-002")]
    public void Every_origin_follows_the_same_table(ActivationOrigin origin)
    {
        var phase = origin
            is ActivationOrigin.UiaInvoke
                or ActivationOrigin.Keyboard
                or ActivationOrigin.Repeat
                or ActivationOrigin.TryNow
            ? ActivationPhase.Invoke
            : ActivationPhase.ContactEnded;

        Decide(Context(phase: phase, origin: origin))
            .ShouldBe(new ActivationDecision.Execute(Copy, InjectionMode.VirtualKey));
        Decide(Context(phase: phase, origin: origin, editMode: true))
            .ShouldBe(new ActivationDecision.OpenEditor(Copy.Id));
        Decide(Context(phase: phase, origin: origin, testMode: true))
            .ShouldBe(new ActivationDecision.TestMark(true, TouchVerdict.Accepted));
        Decide(Context(phase: phase, origin: origin, elevation: ElevationState.TargetElevated))
            .ShouldBeOfType<ActivationDecision.BlockedElevated>();
        Decide(Context(Shortcuts.Tap("empty"), phase, origin))
            .ShouldBe(new ActivationDecision.Refused(RefusalReason.Incomplete));
        Decide(Context(Shortcuts.Tap("cad", "ctrl", "alt", "delete"), phase, origin))
            .ShouldBe(new ActivationDecision.Refused(RefusalReason.BlockedCombo));
    }

    [Fact]
    [Trait("Req", "TAC-002")]
    public void An_ignored_touch_is_ignored_before_anything_else_and_never_restarts_the_window()
    {
        var swipe = new ContactSummary(TimeSpan.FromMilliseconds(100), 200, false);
        var outcome = ActivationPolicy.Decide(
            Context(contact: swipe, editMode: true, elevation: ElevationState.TargetElevated)
        );

        outcome.Decision.ShouldBe(new ActivationDecision.Ignored(TouchVerdict.IgnoredSwipe));
        outcome.NextFilter.LastAccepted.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "TAC-002")]
    public void A_second_touch_inside_the_debounce_is_a_double()
    {
        var first = ActivationPolicy.Decide(Context());

        Decide(Context(filter: first.NextFilter))
            .ShouldBe(new ActivationDecision.Ignored(TouchVerdict.IgnoredDouble));
    }

    [Fact]
    [Trait("Req", "TAC-008")]
    public void Test_mode_marks_ignored_touches_too_and_never_executes() =>
        Decide(
                Context(
                    contact: new ContactSummary(TimeSpan.FromMilliseconds(100), 0, PalmLike: true),
                    testMode: true
                )
            )
            .ShouldBe(new ActivationDecision.TestMark(false, TouchVerdict.IgnoredPalm));

    [Fact]
    [Trait("Req", "EJE-005")]
    public void An_invocation_has_no_touch_filter()
    {
        var filter = ActivationPolicy.Decide(Context()).NextFilter;

        Decide(
                Context(
                    phase: ActivationPhase.Invoke,
                    origin: ActivationOrigin.UiaInvoke,
                    filter: filter
                )
            )
            .ShouldBeOfType<ActivationDecision.Execute>();
    }

    [Fact]
    [Trait("Req", "EJE-004")]
    public void A_hold_starts_only_outside_the_debounce_of_its_tile()
    {
        var hold = Shortcuts.Hold("shift", "shift");
        var started = ActivationPolicy.Decide(Context(hold, ActivationPhase.ContactStarted));

        started.Decision.ShouldBeOfType<ActivationDecision.Execute>();
        Decide(Context(hold, ActivationPhase.ContactStarted, filter: started.NextFilter))
            .ShouldBe(new ActivationDecision.Ignored(TouchVerdict.IgnoredDouble));
    }

    [Fact]
    [Trait("Req", "EJE-013")]
    public void An_elevated_target_blocks_what_injects_but_not_what_the_shell_starts()
    {
        Decide(
                Context(
                    Shortcuts.Url("web", "https://example.com"),
                    elevation: ElevationState.TargetElevated
                )
            )
            .ShouldBeOfType<ActivationDecision.Execute>();
        Decide(Context(Shortcuts.Text("text", "hola"), elevation: ElevationState.TargetElevated))
            .ShouldBeOfType<ActivationDecision.BlockedElevated>();
        Decide(Context(elevation: ElevationState.Unknown))
            .ShouldBeOfType<ActivationDecision.Execute>();
    }

    [Fact]
    [Trait("Req", "EJE-002")]
    public void A_confirming_shortcut_arms_on_the_first_tap_and_runs_on_the_second()
    {
        var close = Shortcuts.Of(
            "close",
            new TapAction(Chords.Of("alt", "f4"), []),
            Shortcuts.Confirming
        );
        var first = ActivationPolicy.Decide(Context(close));

        first.Decision.ShouldBe(
            new ActivationDecision.Armed(close.Id, Now + Timings.Confirmation.ExecuteConfirmWindow)
        );

        // EC-TAC-03: the debounce of the armed tile does not refuse the confirmation tap.
        var armed = new ArmedConfirmation(
            close.Id,
            Now + Timings.Confirmation.ExecuteConfirmWindow
        );
        Decide(Context(close, armed: armed, filter: first.NextFilter))
            .ShouldBe(new ActivationDecision.Execute(close, InjectionMode.VirtualKey));
        Decide(Context(close, armed: new ArmedConfirmation(close.Id, Now - TimeSpan.FromTicks(1))))
            .ShouldBeOfType<ActivationDecision.Armed>();
        Decide(Context(close, armed: new ArmedConfirmation(Copy.Id, Now + TimeSpan.FromSeconds(1))))
            .ShouldBeOfType<ActivationDecision.Armed>();
    }

    [Fact]
    [Trait("Req", "EJE-015")]
    public void Every_incomplete_kind_is_refused()
    {
        Shortcut[] incomplete =
        [
            Shortcuts.Hold("h"),
            Shortcuts.Toggle("t"),
            Shortcuts.Text("x", string.Empty),
            Shortcuts.Macro("m"),
            Shortcuts.Of("u", new UrlAction(new UrlTarget.Raw("ejemplo"))),
            Shortcuts.Of("a", new AppAction(new AppTarget.Raw("cmd /c del"))),
        ];

        foreach (var shortcut in incomplete)
        {
            Decide(Context(shortcut, phase: ActivationPhase.Invoke))
                .ShouldBe(
                    new ActivationDecision.Refused(RefusalReason.Incomplete),
                    shortcut.Id.Value
                );
        }
    }

    [Fact]
    [Trait("Req", "EJE-017")]
    public void The_decision_depends_only_on_the_context_so_dimming_cannot_change_it() =>
        Decide(Context()).ShouldBe(Decide(Context()));
}
