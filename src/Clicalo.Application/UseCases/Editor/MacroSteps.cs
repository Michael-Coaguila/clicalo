using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The edits of the steps of a macro (EDI-013): add, move, and change a wait, a text, a mouse action or the keys of a
/// step. Deleting a step is the destructive command <c>DeleteMacroStep</c> (two taps and undo). Every step keeps a
/// valid kind and every wait stays in <c>Timings.Macro.MacroWaitRange</c> (invariant I6). Pure.
/// </summary>
public static class MacroSteps
{
    /// <summary>A new step of <paramref name="kind"/> at the end.</summary>
    /// <param name="macro">The macro.</param>
    /// <param name="kind">The button tapped.</param>
    public static MacroAction Add(MacroAction macro, MacroStepKind kind)
    {
        ArgumentNullException.ThrowIfNull(macro);
        MacroStep step = kind switch
        {
            MacroStepKind.Keys => new KeysStep(KeyChord.Empty),
            MacroStepKind.Wait => new WaitStep(Timings.Macro.MacroWaitDefault),
            MacroStepKind.Text => new TextStep(SecretText.Empty),
            _ => new MouseStep(MouseOp.RightClick),
        };
        return new MacroAction(new(macro.Steps.Items.Add(step)));
    }

    /// <summary>↑ or ↓: the step at <paramref name="index"/> moves one place; nothing at the ends.</summary>
    /// <param name="macro">The macro.</param>
    /// <param name="index">The zero-based step.</param>
    /// <param name="offset">-1 for ↑, 1 for ↓.</param>
    public static MacroAction Move(MacroAction macro, int index, int offset)
    {
        ArgumentNullException.ThrowIfNull(macro);
        var target = index + offset;
        if (!InRange(macro, index) || !InRange(macro, target))
        {
            return macro;
        }

        var steps = macro.Steps.Items;
        return new MacroAction(
            new(steps.SetItem(index, steps[target]).SetItem(target, steps[index]))
        );
    }

    /// <summary>− or + of a wait: one <c>MacroWaitRange.Step</c> (100 ms) less or more, inside the range.</summary>
    /// <param name="macro">The macro.</param>
    /// <param name="index">The zero-based step, a wait.</param>
    /// <param name="steps">How many steps of 100 ms (negative for −).</param>
    public static MacroAction Nudge(MacroAction macro, int index, int steps)
    {
        ArgumentNullException.ThrowIfNull(macro);
        if (!InRange(macro, index) || macro.Steps[index] is not WaitStep wait)
        {
            return macro;
        }

        var range = Timings.Macro.MacroWaitRange;
        var ticks = wait.Duration.Ticks + (steps * range.Step.Ticks);
        var snapped = Math.Round((double)ticks / range.Step.Ticks) * range.Step.Ticks;
        var duration = TimeSpan.FromTicks(
            Math.Clamp((long)snapped, range.Min.Ticks, range.Max.Ticks)
        );
        return Replace(macro, index, new WaitStep(duration));
    }

    /// <summary>The text of a text step.</summary>
    /// <param name="macro">The macro.</param>
    /// <param name="index">The zero-based step, a text.</param>
    /// <param name="text">The new text.</param>
    public static MacroAction SetText(MacroAction macro, int index, SecretText text) =>
        InRange(macro, index) && macro.Steps[index] is TextStep
            ? Replace(macro, index, new TextStep(text))
            : macro;

    /// <summary>The action of a mouse step.</summary>
    /// <param name="macro">The macro.</param>
    /// <param name="index">The zero-based step, a mouse action.</param>
    /// <param name="op">The mouse action.</param>
    public static MacroAction SetMouse(MacroAction macro, int index, MouseOp op) =>
        InRange(macro, index) && macro.Steps[index] is MouseStep
            ? Replace(macro, index, new MouseStep(op))
            : macro;

    /// <summary>The combination of a keys step (edited with the combination box, EDI-007).</summary>
    /// <param name="macro">The macro.</param>
    /// <param name="index">The zero-based step, a keys step.</param>
    /// <param name="edit">The change of its combination.</param>
    public static MacroAction EditKeys(MacroAction macro, int index, Func<KeyChord, KeyChord> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        return InRange(macro, index) && macro.Steps[index] is KeysStep keys
            ? Replace(macro, index, new KeysStep(edit(keys.Chord)))
            : macro;
    }

    /// <summary>The combination of the keys step at <paramref name="index"/>, or null.</summary>
    /// <param name="macro">The macro.</param>
    /// <param name="index">The zero-based step.</param>
    public static KeyChord? KeysOf(MacroAction macro, int index) =>
        InRange(macro, index) && macro.Steps[index] is KeysStep keys ? keys.Chord : null;

    private static bool InRange(MacroAction macro, int index) =>
        macro is not null && index >= 0 && index < macro.Steps.Count;

    private static MacroAction Replace(MacroAction macro, int index, MacroStep step) =>
        new(new(macro.Steps.Items.SetItem(index, step)));
}
