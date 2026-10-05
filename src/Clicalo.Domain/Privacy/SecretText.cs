using System.Buffers;
using System.Globalization;

namespace Clicalo.Domain.Privacy;

/// <summary>
/// The text of a Text action or a macro text step (blueprint §6.2, LOG-003). It has no member that returns a
/// <see cref="string"/>: users get a <see cref="ReadOnlySpan{T}"/> through <see cref="WithRevealed{TState}"/>, which
/// only Execution, the engine, the Control Center editor and the persistence mappers may call (ArchUnit). The content
/// lives in a private <see cref="char"/> array shared between document versions, so it is not wiped; the engine copies
/// it to rented buffers and wipes those after sending. Encryption on disk belongs to persistence (DPAPI, ADR-0008).
/// </summary>
[Sensitive(RedactionKind.Secret)]
public sealed class SecretText : IEquatable<SecretText>
{
    private readonly char[] _chars;

    private SecretText(char[] chars, bool isAvailable)
    {
        _chars = chars;
        IsAvailable = isAvailable;
    }

    /// <summary>
    /// A text that could not be decrypted (another machine or user, COP-005): the shortcut becomes incomplete until
    /// the user types it again.
    /// </summary>
    public static SecretText Unavailable { get; } = new([], isAvailable: false);

    /// <summary>The empty text.</summary>
    public static SecretText Empty { get; } = new([], isAvailable: true);

    /// <summary>Whether the text is usable (see <see cref="Unavailable"/>).</summary>
    public bool IsAvailable { get; }

    /// <summary>Number of UTF-16 code units.</summary>
    public int Length => _chars.Length;

    /// <summary>Creates a text with a private copy of <paramref name="text"/>.</summary>
    /// <param name="text">The content; the caller wipes its own buffer.</param>
    public static SecretText From(ReadOnlySpan<char> text) =>
        text.IsEmpty ? Empty : new SecretText(text.ToArray(), isAvailable: true);

    /// <summary>Lends the content to <paramref name="use"/> as a span; the span must not escape the call.</summary>
    /// <typeparam name="TState">Caller state, to avoid closures.</typeparam>
    /// <param name="state">Passed to <paramref name="use"/>.</param>
    /// <param name="use">Receives the content.</param>
    public void WithRevealed<TState>(TState state, ReadOnlySpanAction<char, TState> use)
    {
        ArgumentNullException.ThrowIfNull(use);
        use(_chars, state);
    }

    /// <summary>A redacted description with the length only (blueprint §9.4).</summary>
    public override string ToString() =>
        IsAvailable
            ? "[hidden · " + Length.ToString(CultureInfo.InvariantCulture) + " chars]"
            : "[unavailable]";

    /// <inheritdoc />
    public bool Equals(SecretText? other) =>
        other is not null
        && IsAvailable == other.IsAvailable
        && _chars.AsSpan().SequenceEqual(other._chars);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as SecretText);

    /// <summary>Depends on the length and availability only, never on the content.</summary>
    public override int GetHashCode() => HashCode.Combine(IsAvailable, Length);
}
