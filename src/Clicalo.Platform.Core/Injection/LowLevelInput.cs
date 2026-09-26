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

    /// <summary>A UTF-16 unit typed as Unicode (down and up).</summary>
    /// <param name="character">The unit.</param>
    public static LowLevelInput Unicode(char character) =>
        new(LowLevelInputKind.Unicode, default, character, 0, 0, LedgerMouseButtons.None, 0);

    /// <summary>An absolute move to a point of the virtual desktop, in physical pixels.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    public static LowLevelInput MoveTo(int x, int y) =>
        new(LowLevelInputKind.MouseMove, default, '\0', x, y, LedgerMouseButtons.None, 0);

    /// <summary>A mouse button down.</summary>
    /// <param name="button">One button.</param>
    public static LowLevelInput ButtonDown(LedgerMouseButtons button) =>
        new(LowLevelInputKind.MouseButtonDown, default, '\0', 0, 0, button, 0);

    /// <summary>A mouse button up.</summary>
    /// <param name="button">One button.</param>
    public static LowLevelInput ButtonUp(LedgerMouseButtons button) =>
        new(LowLevelInputKind.MouseButtonUp, default, '\0', 0, 0, button, 0);

    /// <summary>One vertical wheel step (positive: away from the user, scroll up).</summary>
    /// <param name="delta">Multiples of 120.</param>
    public static LowLevelInput Wheel(int delta) =>
        new(LowLevelInputKind.Wheel, default, '\0', 0, 0, LedgerMouseButtons.None, delta);

    /// <summary>One horizontal wheel step (positive: to the right).</summary>
    /// <param name="delta">Multiples of 120.</param>
    public static LowLevelInput HorizontalWheel(int delta) =>
        new(LowLevelInputKind.HorizontalWheel, default, '\0', 0, 0, LedgerMouseButtons.None, delta);
}
