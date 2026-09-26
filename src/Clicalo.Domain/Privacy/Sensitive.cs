namespace Clicalo.Domain.Privacy;

/// <summary>
/// Wraps a value that must never be logged (blueprint §9.4): its <see cref="ToString"/> is redacted and CLC0003 stops
/// it at compile time before it reaches a logger or an exception.
/// </summary>
/// <typeparam name="T">Type of the wrapped value.</typeparam>
/// <param name="Value">The value. Read it only to use it, never to format it.</param>
/// <param name="Kind">What the value is.</param>
public readonly record struct Sensitive<T>(T Value, RedactionKind Kind)
{
    /// <summary>A redacted text that names only the kind of value.</summary>
    public override string ToString() => "[redacted " + Kind.ToString() + "]";
}
