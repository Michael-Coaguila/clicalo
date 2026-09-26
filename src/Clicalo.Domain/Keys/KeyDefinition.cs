namespace Clicalo.Domain.Keys;

/// <summary>
/// Metadata of a catalog key, generated into <see cref="KeyDefinitions"/> from <c>data/catalogs/keys.json</c>.
/// Visible labels are product text and are loaded from the catalog at run time, never compiled in.
/// </summary>
/// <param name="Id">Canonical key identity.</param>
/// <param name="Group">Picker group (EDI-008).</param>
/// <param name="Modifier">Modifier family, or <see langword="null"/> for keys that are not modifiers.</param>
/// <param name="Side">Side of a sided modifier; <see cref="KeySide.Any"/> for every other key.</param>
/// <param name="BaseKey">
/// For a sided modifier, the any-side key it specializes (<c>lctrl</c> → <c>ctrl</c>); otherwise <see langword="null"/>.
/// </param>
public sealed record KeyDefinition(
    KeyId Id,
    KeyGroup Group,
    ModifierKind? Modifier,
    KeySide Side,
    KeyId? BaseKey
)
{
    /// <summary>Whether the key is a modifier (compared as a set in canonical combinations, REP-001).</summary>
    public bool IsModifier => Modifier is not null;
}
