using Clicalo.Domain.Execution.Internal;
using Clicalo.Domain.Library;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Execution;

/// <summary>
/// The single activation flow of every surface and origin (EJE-001, blueprint §7.2), in normative order: touch
/// filter → edit mode → test mode → elevation → incomplete or blocked → confirmation → execute. Dimming never changes
/// the result (EJE-017).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>An ended contact goes through <see cref="TouchFilter.Evaluate"/>; the start of a contact (a Hold) through
/// <see cref="TouchFilter.CanStartHold"/>; an invocation (voice, keyboard, switch, Repeat) through no filter.</item>
/// <item>The debounce never refuses the confirmation tap of the armed shortcut (EC-TAC-03).</item>
/// <item>In test mode even an ignored touch is marked (⊘), and nothing is ever sent (TAC-008, INV-7).</item>
/// <item>An elevated target blocks what injects input (keys, text, mouse, macros), not what the Shell thread starts
/// (EJE-011, EJE-013).</item>
/// </list>
/// </remarks>
public static class ActivationPolicy
{
    /// <summary>Decides what an activation does.</summary>
    /// <param name="context">Everything the decision needs.</param>
    public static ActivationOutcome Decide(in ActivationContext context) =>
        Decide(context, EngineRules.Default);

    internal static ActivationOutcome Decide(in ActivationContext context, EngineRules rules)
    {
        ArgumentNullException.ThrowIfNull(context.Shortcut, nameof(context));
        var filter = context.Filter;
        var request = context.Request;
        var shortcut = context.Shortcut;
        var armedHere =
            context.Armed is { } armed
            && armed.Shortcut == shortcut.Id
            && request.At <= armed.Until;

        var verdict = TouchVerdict.Accepted;
        switch (request.Phase)
        {
            case ActivationPhase.ContactEnded when request.Contact is { } contact:
                verdict = TouchFilter.Evaluate(ref filter, contact, context.Touch, request.At);
                if (verdict == TouchVerdict.IgnoredDouble && armedHere)
                {
                    // EC-TAC-03: the debounce of the armed tile never refuses its confirmation tap.
                    verdict = TouchVerdict.Accepted;
                    filter.LastAccepted = request.At;
                }

                break;
            case ActivationPhase.ContactStarted:
                if (TouchFilter.CanStartHold(filter, context.Touch, request.At))
                {
                    filter.LastAccepted = request.At;
                }
                else
                {
                    verdict = TouchVerdict.IgnoredDouble;
                }

                break;
        }

        if (verdict != TouchVerdict.Accepted)
        {
            return new ActivationOutcome(
                context.TestMode && !context.EditMode
                    ? new ActivationDecision.TestMark(Accepted: false, verdict)
                    : new ActivationDecision.Ignored(verdict),
                filter
            );
        }

        if (context.EditMode)
        {
            return new ActivationOutcome(new ActivationDecision.OpenEditor(shortcut.Id), filter);
        }

        if (context.TestMode)
        {
            return new ActivationOutcome(
                new ActivationDecision.TestMark(Accepted: true, TouchVerdict.Accepted),
                filter
            );
        }

        if (context.Elevation == ElevationState.TargetElevated && Injects(shortcut.Action))
        {
            return new ActivationOutcome(new ActivationDecision.BlockedElevated(), filter);
        }

        if (rules.Completeness(shortcut.Action) != CompletenessIssue.None)
        {
            return new ActivationOutcome(
                new ActivationDecision.Refused(RefusalReason.Incomplete),
                filter
            );
        }

        if (HasBlockedCombo(shortcut.Action))
        {
            return new ActivationOutcome(
                new ActivationDecision.Refused(RefusalReason.BlockedCombo),
                filter
            );
        }

        if (shortcut.Options.Confirm && !armedHere)
        {
            return new ActivationOutcome(
                new ActivationDecision.Armed(
                    shortcut.Id,
                    request.At + Timings.Confirmation.ExecuteConfirmWindow
                ),
                filter
            );
        }

        return new ActivationOutcome(
            new ActivationDecision.Execute(shortcut, context.Injection),
            filter
        );
    }

    /// <summary>Whether the action injects input into the foreground app.</summary>
    /// <param name="action">The action.</param>
    internal static bool Injects(ShortcutAction action) =>
        action.Kind
            is ActionKind.Tap
                or ActionKind.Hold
                or ActionKind.Toggle
                or ActionKind.Text
                or ActionKind.Mouse
                or ActionKind.Macro;

    private static bool HasBlockedCombo(ShortcutAction action) =>
        action switch
        {
            TapAction tap => BlockedCombos.IsBlocked(tap.Chord)
                || tap.Variants.Items.Any(static v => BlockedCombos.IsBlocked(v.Chord)),
            HoldAction hold => BlockedCombos.IsBlocked(hold.Chord),
            ToggleAction toggle => BlockedCombos.IsBlocked(toggle.Chord),
            MacroAction macro => macro
                .Steps.Items.OfType<KeysStep>()
                .Any(static s => BlockedCombos.IsBlocked(s.Chord)),
            _ => false,
        };
}
