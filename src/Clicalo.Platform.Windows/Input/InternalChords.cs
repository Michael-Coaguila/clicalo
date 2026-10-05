using Clicalo.Domain.Execution;
using Clicalo.Platform.Core.Injection;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// The keys of the chords Clícalo injects for itself (blueprint §3.6, D-14): the reserved rights chord
/// (Ctrl+Alt+Shift+F24, left modifiers) and Win+H for dictation. The engine sends them itself
/// (<c>InputInjector.SendChord</c>, D-22). Never AltGr or right Ctrl.
/// </summary>
public static class InternalChords
{
    /// <summary>Ctrl+Alt+Shift+F24 with the left modifiers.</summary>
    public static IReadOnlyList<PhysicalKey> Rights { get; } =
    [
        new(0xA2, 0x1D, PhysicalKeyAttributes.None),
        new(0xA4, 0x38, PhysicalKeyAttributes.None),
        new(0xA0, 0x2A, PhysicalKeyAttributes.None),
        new(0x87, 0x76, PhysicalKeyAttributes.None),
    ];

    /// <summary>Win+H (Windows dictation, BUS-003).</summary>
    public static IReadOnlyList<PhysicalKey> Dictation { get; } =
    [new(0x5B, 0x5B, PhysicalKeyAttributes.Extended), new(0x48, 0x23, PhysicalKeyAttributes.None)];

    /// <summary>The keys of <paramref name="chord"/>, in press order.</summary>
    /// <param name="chord">The chord.</param>
    public static IReadOnlyList<PhysicalKey> KeysOf(InternalChord chord) =>
        chord switch
        {
            InternalChord.Rights => Rights,
            InternalChord.Dictation => Dictation,
            _ => throw new ArgumentOutOfRangeException(nameof(chord), chord, null),
        };
}
