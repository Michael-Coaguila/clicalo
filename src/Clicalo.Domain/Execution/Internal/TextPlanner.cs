using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// Text (EJE-008): Unicode character by character (the default, independent of the layout), or through the clipboard
/// followed by Ctrl+V once the SysEvents thread has it ready. The text itself never enters the state or a notice: the
/// effect carries the <see cref="Privacy.SecretText"/> and the host reveals it only to send it.
/// </summary>
internal static class TextPlanner
{
    /// <summary>Ctrl+V, the paste chord of every layout (by virtual key).</summary>
    public static ValueList<KeyStroke> PasteChord { get; } =
    [new KeyStroke(KeyIds.Ctrl), new KeyStroke(KeyIds.V)];

    public static void Plan(EngineStep step, ExecutionOrigin origin, TextAction text)
    {
        if (!step.CanPress(origin))
        {
            return;
        }

        var effect = new EffectId(step.NextSequence());
        if (text.Method == TextMethod.Paste)
        {
            step.State = step.State with
            {
                PendingExternal = step.State.PendingExternal.SetItem(
                    effect,
                    new PendingExternal(effect, origin.Shortcut, step.Now)
                    {
                        Kind = PendingKind.Paste,
                        Origin = origin,
                    }
                ),
            };
            step.Emit(new EngineEffect.ClipboardPaste(effect, text.Text, origin.Epoch));
        }
        else
        {
            step.Emit(
                new EngineEffect.TypeText(
                    effect,
                    text.Text,
                    origin.Epoch,
                    origin.RequiredForeground
                )
            );
        }

        step.CountUsage(origin);
    }

    /// <summary>The clipboard holds the text: send Ctrl+V as a Tap, if the paste is still wanted (INV-6, INV-7).</summary>
    public static void ClipboardReady(EngineStep step, EffectId effect)
    {
        if (
            !step.State.PendingExternal.TryGetValue(effect, out var pending)
            || pending.Kind != PendingKind.Paste
        )
        {
            return;
        }

        step.State = step.State with
        {
            PendingExternal = step.State.PendingExternal.Remove(effect),
        };
        if (pending.Origin is { } origin && step.CanPress(origin))
        {
            KeyPlanner.Tap(step, origin, PasteChord, countsUsage: false);
        }
    }
}
