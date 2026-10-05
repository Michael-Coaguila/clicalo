using Clicalo.Platform.Core.Injection;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// A <c>SendInput</c> over a <see cref="FakeKeyState"/>: it refuses everything (the secure desktop, or the state
/// unreadable) while <see cref="RefusalsLeft"/> lasts, may take only part of the next batch, and otherwise applies the
/// releases to the state.
/// </summary>
internal sealed class FakeSender(FakeKeyState state) : ILowLevelSender
{
    /// <summary>How many more calls are refused with <c>ERROR_ACCESS_DENIED</c>.</summary>
    public int RefusalsLeft { get; set; }

    /// <summary>How many events the next call takes; <see langword="null"/> for all.</summary>
    public int? TakeNext { get; set; }

    public List<LowLevelInput[]> Batches { get; } = [];

    /// <summary>Where every call is written, to check its order against the relaunch.</summary>
    public List<string>? Log { get; set; }

    public SendResult Send(ReadOnlySpan<LowLevelInput> inputs)
    {
        Log?.Add("send");
        Batches.Add(inputs.ToArray());
        if (RefusalsLeft > 0 || !state.CanRead)
        {
            RefusalsLeft = Math.Max(0, RefusalsLeft - 1);
            return new SendResult(0, SendResult.AccessDenied);
        }

        var take = Math.Min(TakeNext ?? inputs.Length, inputs.Length);
        TakeNext = null;
        foreach (var input in inputs[..take])
        {
            switch (input.Kind)
            {
                case LowLevelInputKind.KeyUp:
                    Lift(state, (byte)input.Key.Vk);
                    break;
                case LowLevelInputKind.MouseButtonUp:
                    state.Down.Remove(VirtualKeyOf(input.Button));
                    break;
            }
        }

        return new SendResult(take, take < inputs.Length ? 87 : 0);
    }

    /// <summary>
    /// A key goes up; as in Windows, the generic Shift, Ctrl or Alt (<c>0x10</c> to <c>0x12</c>) goes up with the last
    /// of its side keys.
    /// </summary>
    private static void Lift(FakeKeyState state, byte virtualKey)
    {
        state.Down.Remove(virtualKey);
        if (virtualKey is >= 0xA0 and <= 0xA5)
        {
            var left = (byte)(virtualKey & ~1);
            if (!state.Down.Contains(left) && !state.Down.Contains((byte)(left + 1)))
            {
                state.Down.Remove((byte)(0x10 + ((left - 0xA0) / 2)));
            }
        }
    }

    public static byte VirtualKeyOf(LowLevelMouseButtons button) =>
        button switch
        {
            LowLevelMouseButtons.Left => 0x01,
            LowLevelMouseButtons.Right => 0x02,
            LowLevelMouseButtons.Middle => 0x04,
            LowLevelMouseButtons.X1 => 0x05,
            _ => 0x06,
        };
}
