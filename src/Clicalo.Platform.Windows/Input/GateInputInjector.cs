using System.Buffers;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Windows.Win32;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// The only <see cref="IInputInjector"/> (blueprint §4.4, ADR-0004): every call goes through
/// <see cref="InjectionGate"/> with the caller's generation, so the ledger records each press before it is sent and a
/// fenced engine sends nothing. Called only from the engine thread.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Text goes as Unicode, a line break as Enter (EJE-008); the rented buffer is wiped after the send.</item>
/// <item>A mouse action moves to the point first and acts there (EJE-009, §7.11); without a point it acts at the
/// centre of the foreground window's client area. A drag only moves: its button goes through <see cref="Send"/>,
/// held by the ledger.</item>
/// <item><see cref="InjectionStatus.Blocked"/> is <c>SendInput</c> refusing everything with
/// <c>ERROR_ACCESS_DENIED</c>, which the secure desktop (a locked session) does; the gate marks the releases pending.</item>
/// </list>
/// </remarks>
/// <param name="gate">The gate.</param>
public sealed class GateInputInjector(InjectionGate gate) : IInputInjector
{
    private const int StackInputs = 32;
    private const int WheelStep = 120;

    /// <inheritdoc />
    public InjectionResult Send(EngineGeneration generation, ReadOnlySpan<InjectedEvent> events)
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
            return Result(gate.TryInject(generation.Value, inputs), count);
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
    public InjectionResult TypeText(EngineGeneration generation, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return new InjectionResult(InjectionStatus.Sent, 0, 0);
        }

        var rented = ArrayPool<LowLevelInput>.Shared.Rent(text.Length * 2);
        try
        {
            var count = InputMapping.FillText(text, rented);
            return SendBalanced(generation, rented.AsSpan(0, count));
        }
        finally
        {
            // The inputs hold the text's characters: wipe them before the buffer goes back to the pool.
            ArrayPool<LowLevelInput>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc />
    public InjectionResult Mouse(
        EngineGeneration generation,
        MouseOp operation,
        PhysicalPoint? target
    )
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
                inputs[count++] = LowLevelInput.ButtonDown(LedgerMouseButtons.Right);
                inputs[count++] = LowLevelInput.ButtonUp(LedgerMouseButtons.Right);
                break;
            case MouseOp.MiddleClick:
                inputs[count++] = LowLevelInput.ButtonDown(LedgerMouseButtons.Middle);
                inputs[count++] = LowLevelInput.ButtonUp(LedgerMouseButtons.Middle);
                break;
            case MouseOp.DoubleClick:
                inputs[count++] = LowLevelInput.ButtonDown(LedgerMouseButtons.Left);
                inputs[count++] = LowLevelInput.ButtonUp(LedgerMouseButtons.Left);
                inputs[count++] = LowLevelInput.ButtonDown(LedgerMouseButtons.Left);
                inputs[count++] = LowLevelInput.ButtonUp(LedgerMouseButtons.Left);
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
                // Only the move: the left button is held by the ledger through Send (EJE-007).
                break;
        }

        return SendBalanced(generation, inputs[..count]);
    }

    /// <inheritdoc />
    public InjectionResult ReleasePending(EngineGeneration generation)
    {
        var outcome = gate.TryReleasePending(generation.Value, out var count);
        return Result(outcome, count);
    }

    /// <summary>The injection result of a gate outcome for a batch of <paramref name="count"/> inputs.</summary>
    /// <param name="outcome">The gate's outcome.</param>
    /// <param name="count">How many inputs the batch had.</param>
    public static InjectionResult Result(GateOutcome outcome, int count)
    {
        if (outcome.Result == GateResult.Fenced)
        {
            return new InjectionResult(InjectionStatus.Fenced, 0, 0);
        }

        var sent = outcome.Send.Sent;
        if (sent >= count)
        {
            return new InjectionResult(InjectionStatus.Sent, sent, 0);
        }

        return sent == 0 && outcome.Send.LastError == InjectionGate.AccessDenied
            ? new InjectionResult(InjectionStatus.Blocked, 0, outcome.Send.LastError)
            : new InjectionResult(InjectionStatus.Failed, sent, outcome.Send.LastError);
    }

    /// <summary>
    /// Sends a batch that releases everything it presses (a text's Enter, a click). When <c>SendInput</c> takes only
    /// part of it, a key or button whose press went but whose release did not would stay down with no holder in the
    /// engine to release it, so its release goes at once in a second batch (an extra release is harmless).
    /// </summary>
    private InjectionResult SendBalanced(
        EngineGeneration generation,
        ReadOnlySpan<LowLevelInput> inputs
    )
    {
        var outcome = gate.TryInject(generation.Value, inputs);
        var sent = Math.Clamp(outcome.Send.Sent, 0, inputs.Length);
        if (outcome.Result == GateResult.Ran && sent > 0 && sent < inputs.Length)
        {
            ReleaseLeftovers(generation, inputs[..sent]);
        }

        return Result(outcome, inputs.Length);
    }

    private void ReleaseLeftovers(EngineGeneration generation, ReadOnlySpan<LowLevelInput> sent)
    {
        var keys = new List<PhysicalKey>();
        var buttons = new List<LedgerMouseButtons>();
        foreach (var input in sent)
        {
            switch (input.Kind)
            {
                case LowLevelInputKind.KeyDown:
                    keys.Add(input.Key);
                    break;
                case LowLevelInputKind.KeyUp:
                    keys.Remove(input.Key);
                    break;
                case LowLevelInputKind.MouseButtonDown:
                    buttons.Add(input.Button);
                    break;
                case LowLevelInputKind.MouseButtonUp:
                    buttons.Remove(input.Button);
                    break;
            }
        }

        if (keys.Count == 0 && buttons.Count == 0)
        {
            return;
        }

        var releases = new List<LowLevelInput>(keys.Count + buttons.Count);
        for (var i = buttons.Count - 1; i >= 0; i--)
        {
            releases.Add(LowLevelInput.ButtonUp(buttons[i]));
        }

        for (var i = keys.Count - 1; i >= 0; i--)
        {
            releases.Add(LowLevelInput.KeyUp(keys[i]));
        }

        gate.TryInject(generation.Value, [.. releases]);
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
