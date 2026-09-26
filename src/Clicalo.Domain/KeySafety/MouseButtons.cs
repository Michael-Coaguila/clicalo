namespace Clicalo.Domain.KeySafety;

/// <summary>Mouse buttons Clícalo presses (a drag holds the left one, EJE-007).</summary>
[Flags]
public enum MouseButtons
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Left button.</summary>
    Left = 1 << 0,

    /// <summary>Right button.</summary>
    Right = 1 << 1,

    /// <summary>Middle button.</summary>
    Middle = 1 << 2,

    /// <summary>First extra button.</summary>
    X1 = 1 << 3,

    /// <summary>Second extra button.</summary>
    X2 = 1 << 4,
}
