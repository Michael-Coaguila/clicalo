using System.Collections.Immutable;
using Clicalo.Domain.Library;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The «Ver» animation of the «Probar» card by kind (PRB-002), as a script of frames with the times of
/// <c>Timings.TryAnimation</c>. Pure: the card plays it with a <see cref="TimeProvider"/> timer, cancels it when it
/// plays again or closes (PRB-005), and with reduce motion shows only the last frame.
/// </summary>
public static class TryPlayback
{
    /// <summary>The frames of <paramref name="action"/>, in time order; the last one is where it rests.</summary>
    /// <param name="action">The action of the shortcut.</param>
    /// <param name="reduceMotion">Whether to jump to the end.</param>
    public static ImmutableArray<PlaybackFrame> Script(ShortcutAction action, bool reduceMotion)
    {
        ArgumentNullException.ThrowIfNull(action);
        var frames = Frames(action);
        return reduceMotion ? [frames[^1] with { At = TimeSpan.Zero }] : frames;
    }

    private static ImmutableArray<PlaybackFrame> Frames(ShortcutAction action)
    {
        var builder = ImmutableArray.CreateBuilder<PlaybackFrame>();
        var t = TimeSpan.Zero;
        switch (action)
        {
            case TapAction tap:
            {
                var n = tap.Chord.Strokes.Count;
                KeysDown(builder, ref t, n, PlaybackPhase.Pressing);
                t += Timings.TryAnimation.TapReleasedAfter;
                builder.Add(new(t, 0, PlaybackPhase.ReleasedAll, -1));
                break;
            }

            case HoldAction hold:
            {
                var n = hold.Chord.Strokes.Count;
                builder.Add(new(t, 0, PlaybackPhase.Touch, -1));
                KeysDown(builder, ref t, n, PlaybackPhase.Touch);
                t += Timings.TryAnimation.HoldHoldingAfter;
                builder.Add(new(t, n, PlaybackPhase.Holding, -1));
                t += Timings.TryAnimation.HoldLiftAfter;
                builder.Add(new(t, 0, PlaybackPhase.Lift, -1));
                t += Timings.TryAnimation.HoldDoneAfter;
                builder.Add(new(t, 0, PlaybackPhase.Done, -1));
                break;
            }

            case ToggleAction toggle:
            {
                var n = toggle.Chord.Strokes.Count;
                builder.Add(new(t, 0, PlaybackPhase.FirstTap, -1));
                KeysDown(builder, ref t, n, PlaybackPhase.FirstTap);
                t += Timings.TryAnimation.ToggleLatchedAfter;
                builder.Add(new(t, n, PlaybackPhase.Latched, -1));
                t += Timings.TryAnimation.ToggleSecondTapAfter;
                builder.Add(new(t, n, PlaybackPhase.SecondTap, -1));
                t += Timings.TryAnimation.ToggleReleasedAfter;
                builder.Add(new(t, 0, PlaybackPhase.Released, -1));
                break;
            }

            case MacroAction macro:
            {
                for (var i = 0; i < macro.Steps.Count; i++)
                {
                    if (i > 0)
                    {
                        t += Timings.TryAnimation.MacroStepInterval;
                    }

                    builder.Add(new(t, i + 1, PlaybackPhase.Step, i));
                }

                t += macro.Steps.IsEmpty ? TimeSpan.Zero : Timings.TryAnimation.MacroDoneAfter;
                builder.Add(new(t, macro.Steps.Count, PlaybackPhase.Done, -1));
                break;
            }

            default:
                builder.Add(new(t, 1, PlaybackPhase.Sentence, -1));
                t += Timings.TryAnimation.OtherDoneAfter;
                builder.Add(new(t, 1, PlaybackPhase.Done, -1));
                break;
        }

        return builder.ToImmutable();
    }

    /// <summary>The keys going down one by one, <c>PlaybackKeyInterval</c> apart.</summary>
    private static void KeysDown(
        ImmutableArray<PlaybackFrame>.Builder frames,
        ref TimeSpan at,
        int count,
        PlaybackPhase phase
    )
    {
        for (var i = 1; i <= count; i++)
        {
            if (i > 1)
            {
                at += Timings.TryAnimation.PlaybackKeyInterval;
            }

            frames.Add(new(at, i, phase, -1));
        }
    }
}
