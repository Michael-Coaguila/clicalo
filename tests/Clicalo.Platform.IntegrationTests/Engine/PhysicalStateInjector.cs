using Clicalo.Platform.Core.Injection;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The model of what the system has down (<c>D</c> of blueprint §7.5), as both the <c>SendInput</c> and the key state
/// of the real <c>InputInjector</c> (ADR-0022). It never injects: it applies each batch to its own state. It can answer
/// like a partial <c>SendInput</c> or like the secure desktop.
/// </summary>
internal sealed class PhysicalStateInjector : ILowLevelSender, IKeyStateReader
{
    private readonly HashSet<PhysicalKey> _keys = [];

    public LowLevelMouseButtons Buttons { get; private set; }

    public IReadOnlySet<PhysicalKey> Keys => _keys;

    public bool IsEmpty => _keys.Count == 0 && Buttons == LowLevelMouseButtons.None;

    public List<LowLevelInput[]> Batches { get; } = [];

    /// <summary>How many events the next call takes (then back to all); <see langword="null"/> for all.</summary>
    public int? TakeNext { get; set; }

    /// <summary>Whether the secure desktop has the input: every call is refused and the state unreadable.</summary>
    public bool SecureDesktop { get; set; }

    /// <inheritdoc />
    public bool CanRead => !SecureDesktop;

    /// <summary>Presses a key outside Clícalo's batches (another holder, or what a dead process left down).</summary>
    public void Press(PhysicalKey key) => _keys.Add(key);

    /// <inheritdoc />
    public bool IsDown(byte virtualKey) =>
        CanRead
        && virtualKey switch
        {
            0x01 => Buttons.HasFlag(LowLevelMouseButtons.Left),
            0x02 => Buttons.HasFlag(LowLevelMouseButtons.Right),
            0x04 => Buttons.HasFlag(LowLevelMouseButtons.Middle),
            0x05 => Buttons.HasFlag(LowLevelMouseButtons.X1),
            0x06 => Buttons.HasFlag(LowLevelMouseButtons.X2),
            _ => _keys.Any(key => key.Vk == virtualKey),
        };

    /// <inheritdoc />
    public ushort ScanCode(byte virtualKey) =>
        _keys.FirstOrDefault(key => key.Vk == virtualKey) is { Vk: not 0 } key
            ? (ushort)(
                (
                    (key.Attributes & PhysicalKeyAttributes.Extended) != PhysicalKeyAttributes.None
                        ? 0xE000
                        : 0
                ) | key.Scan
            )
            : (ushort)0;

    /// <inheritdoc />
    public SendResult Send(ReadOnlySpan<LowLevelInput> inputs)
    {
        var batch = inputs.ToArray();
        Batches.Add(batch);
        if (SecureDesktop)
        {
            return new SendResult(0, SendResult.AccessDenied);
        }

        var take = TakeNext is { } limit ? Math.Clamp(limit, 0, batch.Length) : batch.Length;
        TakeNext = null;
        foreach (var input in batch.AsSpan(0, take))
        {
            switch (input.Kind)
            {
                case LowLevelInputKind.KeyDown:
                    _keys.Add(input.Key);
                    break;
                case LowLevelInputKind.KeyUp:
                    _keys.RemoveWhere(key => key.Vk == input.Key.Vk && key.Scan == input.Key.Scan);
                    break;
                case LowLevelInputKind.MouseButtonDown:
                    Buttons |= input.Button;
                    break;
                case LowLevelInputKind.MouseButtonUp:
                    Buttons &= ~input.Button;
                    break;
            }
        }

        return take == batch.Length ? new SendResult(take, 0) : new SendResult(take, 87);
    }
}
