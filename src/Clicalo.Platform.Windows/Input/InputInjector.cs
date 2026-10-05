using System.Buffers;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Platform.Core.Injection;
using Windows.Win32;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// The only <see cref="IInputInjector"/> (blueprint §4.4, ADR-0023), over the product's single <c>SendInput</c>
/// (<see cref="LowLevelInjector"/>). Called only from the engine thread; it keeps no state.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Text goes as Unicode, a line break as Enter (EJE-008); the rented buffer is wiped after the send.</item>
/// <item>A mouse action moves to the point first and acts there (EJE-009, §7.11); without a point it acts at the
/// centre of the foreground window's client area. A drag only moves: its button goes through <see cref="Send"/>,
/// held by the engine.</item>
/// <item>A batch that releases everything it presses (text, a click, an internal chord) and that <c>SendInput</c>
/// takes only in part is balanced at once: what went down and not up goes up in a second batch, Alt and Win with the
/// menu mask (an extra release is harmless).</item>
/// <item><see cref="InjectionStatus.Blocked"/> is <c>SendInput</c> refusing everything with
/// <c>ERROR_ACCESS_DENIED</c>, which the secure desktop (a locked session) does.</item>
/// </list>
/// </remarks>
/// <param name="sender">The real <see cref="LowLevelInjector"/>, or the tests' sender.</param>
/// <param name="keys">What Windows reports down: the chords skip keys already down, and <see cref="ReleasePressed"/>.</param>
public sealed class InputInjector(ILowLevelSender sender, IKeyStateReader keys) : IInputInjector
{
    private const int StackInputs = 32;
    private const int WheelStep = 120;

    /// <inheritdoc />
    public InjectionResult Send(ReadOnlySpan<InjectedEvent> events)
    {
        if (events.IsEmpty)
        {
            return new InjectionResult(InjectionStatus.Sent, 0, 0);
        }

        var count = InputMapping.CountOf(events);
        LowLevelInput[]? rented = null;
        var inputs =
            count <= StackInputs
                ? stackalloc LowLevelInput[count]
                : (rented = ArrayPool<LowLevelInput>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            InputMapping.Fill(events, inputs);
            return Result(sender.Send(inputs), count);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<LowLevelInput>.Shared.Return(rented, clearArray: true);
            }
        }
    }

    /// <inheritdoc />
    public InjectionResult TypeText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return new InjectionResult(InjectionStatus.Sent, 0, 0);
        }

        var rented = ArrayPool<LowLevelInput>.Shared.Rent(text.Length * 2);
        try
        {
            var count = InputMapping.FillText(text, rented);
            return SendBalanced(rented.AsSpan(0, count));
        }
        finally
        {
            // The inputs hold the text's characters: wipe them before the buffer goes back to the pool.
            ArrayPool<LowLevelInput>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc />
    public InjectionResult Mouse(MouseOp operation, PhysicalPoint? target)
    {
        var point = target ?? ForegroundClientCentre();
        if (point is not { } at)
        {
            return new InjectionResult(InjectionStatus.Failed, 0, 0);
        }

        Span<LowLevelInput> inputs = stackalloc LowLevelInput[5];
        var count = 0;
        inputs[count++] = LowLevelInput.MoveTo(at.X, at.Y);
        switch (operation)
        {
            case MouseOp.RightClick:
                inputs[count++] = LowLevelInput.ButtonDown(LowLevelMouseButtons.Right);
                inputs[count++] = LowLevelInput.ButtonUp(LowLevelMouseButtons.Right);
                break;
            case MouseOp.MiddleClick:
                inputs[count++] = LowLevelInput.ButtonDown(LowLevelMouseButtons.Middle);
                inputs[count++] = LowLevelInput.ButtonUp(LowLevelMouseButtons.Middle);
                break;
            case MouseOp.DoubleClick:
                inputs[count++] = LowLevelInput.ButtonDown(LowLevelMouseButtons.Left);
                inputs[count++] = LowLevelInput.ButtonUp(LowLevelMouseButtons.Left);
                inputs[count++] = LowLevelInput.ButtonDown(LowLevelMouseButtons.Left);
                inputs[count++] = LowLevelInput.ButtonUp(LowLevelMouseButtons.Left);
                break;
            case MouseOp.ScrollUp:
                inputs[count++] = LowLevelInput.Wheel(WheelStep);
                break;
            case MouseOp.ScrollDown:
                inputs[count++] = LowLevelInput.Wheel(-WheelStep);
                break;
            case MouseOp.ScrollLeft:
                inputs[count++] = LowLevelInput.HorizontalWheel(-WheelStep);
                break;
            case MouseOp.ScrollRight:
                inputs[count++] = LowLevelInput.HorizontalWheel(WheelStep);
                break;
            case MouseOp.Drag:
                // Only the move: the left button is held by the engine through Send (EJE-007).
                break;
        }

        return SendBalanced(inputs[..count]);
    }

    /// <inheritdoc />
    public InjectionResult SendChord(InternalChord chord)
    {
        var pressed = new List<PhysicalKey>();
        foreach (var key in InternalChords.KeysOf(chord))
        {
            if (!keys.IsDown((byte)key.Vk) && !pressed.Contains(key))
            {
                pressed.Add(key);
            }
        }

        var batch = new LowLevelInput[pressed.Count * 2];
        for (var i = 0; i < pressed.Count; i++)
        {
            batch[i] = LowLevelInput.KeyDown(pressed[i]);
            batch[batch.Length - 1 - i] = LowLevelInput.KeyUp(pressed[i]);
        }

        return SendBalanced(batch);
    }

    /// <inheritdoc />
    public InjectionResult ReleasePressed()
    {
        var outcome = PressedInputRelease.ReleaseOnce(keys, sender);
        return outcome.Readable
            ? Result(outcome.Send, outcome.Events)
            : new InjectionResult(InjectionStatus.Blocked, 0, SendResult.AccessDenied);
    }

    /// <summary>The injection result of a <c>SendInput</c> call for a batch of <paramref name="count"/> inputs.</summary>
    /// <param name="send">What <c>SendInput</c> returned.</param>
    /// <param name="count">How many inputs the batch had.</param>
    public static InjectionResult Result(SendResult send, int count)
    {
        if (send.Sent >= count)
        {
            return new InjectionResult(InjectionStatus.Sent, count, 0);
        }

        return send.IsSecureDesktopRefusal
            ? new InjectionResult(InjectionStatus.Blocked, 0, send.LastError)
            : new InjectionResult(InjectionStatus.Failed, send.Sent, send.LastError);
    }

    /// <summary>
    /// The releases of what <paramref name="sent"/> pressed and did not release: buttons first, then keys in reverse
    /// order, with the menu mask before Alt or Win (a lone Alt or Win release would open a menu).
    /// </summary>
    /// <param name="sent">The part of a batch <c>SendInput</c> accepted.</param>
    public static LowLevelInput[] Leftovers(ReadOnlySpan<LowLevelInput> sent)
    {
        var down = new List<PhysicalKey>();
        var buttons = new List<LowLevelMouseButtons>();
        foreach (var input in sent)
        {
            switch (input.Kind)
            {
                case LowLevelInputKind.KeyDown:
                    down.Add(input.Key);
                    break;
                case LowLevelInputKind.KeyUp:
                    _ = down.Remove(input.Key);
                    break;
                case LowLevelInputKind.MouseButtonDown:
                    buttons.Add(input.Button);
                    break;
                case LowLevelInputKind.MouseButtonUp:
                    _ = buttons.Remove(input.Button);
                    break;
            }
        }

        var releases = new List<LowLevelInput>((down.Count * 3) + buttons.Count);
        for (var i = buttons.Count - 1; i >= 0; i--)
        {
            releases.Add(LowLevelInput.ButtonUp(buttons[i]));
        }

        for (var i = down.Count - 1; i >= 0; i--)
        {
            if (PhysicalKeyKinds.IsAltOrWin(down[i]))
            {
                releases.Add(LowLevelInput.KeyDown(PressedInputRelease.MenuMask));
                releases.Add(LowLevelInput.KeyUp(PressedInputRelease.MenuMask));
            }

            releases.Add(LowLevelInput.KeyUp(down[i]));
        }

        return [.. releases];
    }

    /// <summary>
    /// Sends a batch that releases everything it presses (a text's Enter, a click, a chord); when <c>SendInput</c>
    /// takes only part of it, what went down and did not go up goes up at once.
    /// </summary>
    private InjectionResult SendBalanced(ReadOnlySpan<LowLevelInput> inputs)
    {
        if (inputs.IsEmpty)
        {
            return new InjectionResult(InjectionStatus.Sent, 0, 0);
        }

        var result = sender.Send(inputs);
        var sent = Math.Clamp(result.Sent, 0, inputs.Length);
        if (sent > 0 && sent < inputs.Length)
        {
            var leftovers = Leftovers(inputs[..sent]);
            if (leftovers.Length > 0)
            {
                _ = sender.Send(leftovers);
            }
        }

        return Result(result, inputs.Length);
    }

    private static unsafe PhysicalPoint? ForegroundClientCentre()
    {
        var window = PInvoke.GetForegroundWindow();
        if (window.IsNull || !PInvoke.GetClientRect(window, out var rect))
        {
            return null;
        }

        var centre = new System.Drawing.Point(
            (rect.left + rect.right) / 2,
            (rect.top + rect.bottom) / 2
        );
        return PInvoke.ClientToScreen(window, ref centre)
            ? new PhysicalPoint(centre.X, centre.Y)
            : null;
    }
}
