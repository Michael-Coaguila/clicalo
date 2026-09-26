namespace Clicalo.Platform.Core.KeyLedger;

/// <summary>Mouse buttons down, as stored at <see cref="KeyLedgerLayout.MouseButtonsOffset"/>.</summary>
[Flags]
public enum LedgerMouseButtons : byte
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Left.</summary>
    Left = 1 << 0,

    /// <summary>Right.</summary>
    Right = 1 << 1,

    /// <summary>Middle.</summary>
    Middle = 1 << 2,

    /// <summary>First extra button.</summary>
    X1 = 1 << 3,

    /// <summary>Second extra button.</summary>
    X2 = 1 << 4,
}
