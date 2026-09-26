using System.Runtime.InteropServices;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.Core.Injection;

/// <summary>One event for <c>SendInput</c>, independent of the Domain (Platform.Core depends on the BCL only).</summary>
/// <param name="Kind">What it does.</param>
/// <param name="Key">The key of a key event.</param>
/// <param name="Character">The UTF-16 unit of a Unicode event.</param>
/// <param name="X">Absolute x of a move, in physical pixels of the virtual desktop.</param>
/// <param name="Y">Absolute y of a move.</param>
/// <param name="Button">The button of a mouse button event.</param>
/// <param name="WheelDelta">The delta of a wheel event (multiples of 120).</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct LowLevelInput(
    LowLevelInputKind Kind,
    PhysicalKey Key,
    char Character,
    int X,
    int Y,
    LedgerMouseButtons Button,
    int WheelDelta
)
{
    /// <summary>A key down.</summary>
    /// <param name="key">The key.</param>
    public static LowLevelInput KeyDown(PhysicalKey key) =>
        new(LowLevelInputKind.KeyDown, key, '\0', 0, 0, LedgerMouseButtons.None, 0);

    /// <summary>A key up.</summary>
    /// <param name="key">The key, with the attributes of its press.</param>
    public static LowLevelInput KeyUp(PhysicalKey key) =>
        new(LowLevelInputKind.KeyUp, key, '\0', 0, 0, LedgerMouseButtons.None, 0);
}
