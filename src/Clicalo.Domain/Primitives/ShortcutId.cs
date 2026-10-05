namespace Clicalo.Domain.Primitives;

/// <summary>
/// Opaque, stable identity of a shortcut, unique in the whole document (DAT-004, invariant I1). Frequents may keep a
/// dangling reference on purpose (FRE-005).
/// </summary>
/// <param name="Value">The identifier exactly as persisted.</param>
public readonly record struct ShortcutId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
