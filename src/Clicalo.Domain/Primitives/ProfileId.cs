namespace Clicalo.Domain.Primitives;

/// <summary>
/// Opaque, stable identity of a profile (DAT-004): it never depends on the name or the time, renaming keeps it and
/// duplicating, installing or importing always creates a new one (<see cref="IIdGenerator"/>).
/// </summary>
/// <param name="Value">The identifier exactly as persisted.</param>
public readonly record struct ProfileId(string Value)
{
    /// <summary>The General profile: it always exists, cannot be deleted and never has a process (I3, I4).</summary>
    public static ProfileId General { get; } = new("general");

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
