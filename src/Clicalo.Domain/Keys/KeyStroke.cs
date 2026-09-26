namespace Clicalo.Domain.Keys;

/// <summary>
/// One key of a combination: a canonical key plus the side of a modifier (EDI-009, a single side mechanism). A sided
/// catalog key (<c>lctrl</c>, <c>altgr</c>) is normalized into its base key and a side by <see cref="KeyChord.Create"/>.
/// </summary>
/// <param name="Key">The canonical key.</param>
/// <param name="Side">Side of a modifier; <see cref="KeySide.Any"/> for every other key.</param>
public readonly record struct KeyStroke(KeyId Key, KeySide Side = KeySide.Any);
