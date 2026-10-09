using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// Text (EJE-008): Unicode character by character (the default, independent of the layout), or through the clipboard
/// followed by Ctrl+V once the SysEvents thread has it ready. The text itself never enters the state: the effect carries
/// the <see cref="Privacy.SecretText"/> and the host reveals it only to send it. Only the notice of a shortcut that is not
/// private shows its first characters, as EJE-008 asks.
/// </summary>
internal static class TextPlanner
{
    private const int PreviewLength = 16;

    /// <summary>Ctrl+V, the paste chord of every layout (by virtual key).</summary>
    public static ValueList<KeyStroke> PasteChord { get; } =
    [new KeyStroke(KeyIds.Ctrl), new KeyStroke(KeyIds.V)];

    public static void Plan(
        EngineStep step,
        ExecutionOrigin origin,
        Shortcut shortcut,
        TextAction text
    )
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

        step.Notice(TypedNotice(shortcut, text.Text));
        step.CountUsage(origin);
    }

    /// <summary>
    /// The notice of a typed text (EJE-008): «Escrito: «its first 16 characters…»», with line breaks as spaces, or
    /// «Texto escrito» when the shortcut is private. The text is never kept: the preview lives only in the notice.
    /// </summary>
    public static Message TypedNotice(Shortcut shortcut, Privacy.SecretText text)
    {
        if (shortcut.Options.IsPrivate || !text.IsAvailable)
        {
            return L.TextTypedPrivate;
        }

        var preview = new char[Math.Min(text.Length, PreviewLength)];
        text.WithRevealed(preview, static (chars, copy) => chars[..copy.Length].CopyTo(copy));
        for (var i = 0; i < preview.Length; i++)
        {
            if (preview[i] is '\r' or '\n' or '\t')
            {
                preview[i] = ' ';
            }
        }

        var shown = new string(preview) + (text.Length > PreviewLength ? "…" : string.Empty);
        Array.Clear(preview);
        return L.TextTyped(name: shown);
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
