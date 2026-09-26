namespace Clicalo.Domain.Keys;

/// <summary>
/// Canonical, persisted identity of a key: a lower-case name (<c>ctrl</c>, <c>a</c>, <c>num.add</c>) or a
/// layout-dependent character (<c>char:ñ</c>). The physical key (virtual key, scan code, extended flag) is resolved
/// only when sending, against the keyboard layout of the foreground window (blueprint §6.1, PQ-49).
/// The catalog keys are generated as <see cref="KeyIds"/> from <c>data/catalogs/keys.json</c>.
/// </summary>
/// <param name="Value">Canonical identifier, exactly as stored in documents and catalogs.</param>
public readonly record struct KeyId(string Value)
{
    /// <summary>Prefix of the keys that are a character of the foreground keyboard layout.</summary>
    public const string CharacterPrefix = "char:";

    /// <summary>
    /// Whether the key is a character (<c>char:ñ</c>) whose virtual key depends on the foreground keyboard layout.
    /// </summary>
    public bool IsCharacter =>
        Value is not null && Value.StartsWith(CharacterPrefix, StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
