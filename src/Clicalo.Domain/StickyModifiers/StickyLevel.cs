namespace Clicalo.Domain.StickyModifiers;

/// <summary>The three states of a sticky modifier (FIJ-005): each tap moves 0 → 1 → 2 → 0.</summary>
public enum StickyLevel
{
    /// <summary>Released.</summary>
    Off,

    /// <summary>Once: added to the next key or mouse action, then released (FIJ-006).</summary>
    Once,

    /// <summary>Locked: added to every key or mouse action until the next tap releases it.</summary>
    Locked,
}
