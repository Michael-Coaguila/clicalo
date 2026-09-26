namespace Clicalo.Platform.Core.KeyLedger;

/// <summary>How a key was pressed, so it is released the same way (INV-12).</summary>
[Flags]
public enum LedgerKeyAttributes : byte
{
    /// <summary>A normal virtual-key press.</summary>
    None = 0,

    /// <summary>Sent with <c>KEYEVENTF_EXTENDEDKEY</c>.</summary>
    Extended = 1 << 0,

    /// <summary>Sent in scan code mode (<c>KEYEVENTF_SCANCODE</c>, compatible mode).</summary>
    ScanCodeMode = 1 << 1,
}
