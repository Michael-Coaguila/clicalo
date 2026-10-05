namespace Clicalo.Domain.Primitives;

/// <summary>
/// The executable name a profile binds to (<c>winword.exe</c>), without path. Compared without distinguishing case
/// (PER-002, invariant I5); the value keeps the spelling it was created with.
/// </summary>
public readonly record struct ProcessName
{
    /// <summary>Creates a process name.</summary>
    /// <param name="value">Executable name without path; surrounding spaces are removed.</param>
    public ProcessName(string value) => Value = (value ?? string.Empty).Trim();

    /// <summary>The executable name.</summary>
    public string Value { get; }

    /// <summary>Whether there is no name (a manual profile, and General).</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>Equality without distinguishing case (PER-002).</summary>
    /// <param name="other">The other name.</param>
    public bool Equals(ProcessName other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
