namespace Clicalo.Domain.Privacy;

/// <summary>
/// Marks a type whose values must never reach a log, a log interpolation or an exception message (CLC0003,
/// LOG-001). Inherited through base types and interfaces. The analyzer binds to it by metadata name
/// (<c>docs/guides/analyzers.md</c>): do not rename or move it.
/// </summary>
/// <param name="kind">What the values are.</param>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
    Inherited = true
)]
public sealed class SensitiveAttribute(RedactionKind kind) : Attribute
{
    /// <summary>What the values are.</summary>
    public RedactionKind Kind { get; } = kind;
}
