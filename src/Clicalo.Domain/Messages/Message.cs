using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Clicalo.Domain.Messages;

/// <summary>
/// A text to show, kept as a key plus named arguments and localized only when painted (blueprint §8.2), so a
/// language change re-localizes it (IDI-001). Build it with the generated <c>L</c> members, which check the
/// arguments at compile time. Immutable, with value equality.
/// </summary>
public sealed class Message : IEquatable<Message>
{
    /// <summary>Creates a message.</summary>
    /// <param name="key">Key in <c>data/i18n</c>.</param>
    /// <param name="arguments">Named arguments; names must be distinct.</param>
    /// <exception cref="ArgumentException">Two arguments share a name, or a name is empty.</exception>
    public Message(MessageKey key, params ReadOnlySpan<MessageArgument> arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(key.Value, nameof(key));
        for (var i = 0; i < arguments.Length; i++)
        {
            ArgumentException.ThrowIfNullOrEmpty(arguments[i].Name, nameof(arguments));
            for (var j = 0; j < i; j++)
            {
                if (string.Equals(arguments[i].Name, arguments[j].Name, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "The argument '" + arguments[i].Name + "' is given twice.",
                        nameof(arguments)
                    );
                }
            }
        }

        Key = key;
        Arguments = [.. arguments];
    }

    /// <summary>Key in <c>data/i18n</c>.</summary>
    public MessageKey Key { get; }

    /// <summary>Named arguments, in the order given.</summary>
    public ImmutableArray<MessageArgument> Arguments { get; }

    /// <summary>Value equality.</summary>
    public static bool operator ==(Message? left, Message? right) => Equals(left, right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(Message? left, Message? right) => !Equals(left, right);

    /// <summary>Finds an argument by name (ordinal).</summary>
    public bool TryGetArgument(string name, out MessageValue value)
    {
        foreach (var argument in Arguments)
        {
            if (string.Equals(argument.Name, name, StringComparison.Ordinal))
            {
                value = argument.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public bool Equals([NotNullWhen(true)] Message? other) =>
        other is not null
        && (
            ReferenceEquals(this, other)
            || (Key == other.Key && Arguments.SequenceEqual(other.Arguments))
        );

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Message);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        foreach (var argument in Arguments)
        {
            hash.Add(argument);
        }

        return hash.ToHashCode();
    }

    /// <summary>Diagnostic form (<c>comboN(count=3)</c>); never shown to the user.</summary>
    public override string ToString()
    {
        if (Arguments.IsEmpty)
        {
            return Key.Value;
        }

        var sb = new StringBuilder(Key.Value).Append('(');
        for (var i = 0; i < Arguments.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var (name, value) = Arguments[i];
            sb.Append(name).Append('=');
            if (value.TryGetText(out var text))
            {
                sb.Append('"').Append(text).Append('"');
            }
            else if (value.TryGetMessage(out var nested))
            {
                sb.Append(nested);
            }
            else if (value.TryGetWholeNumber(out var integer))
            {
                sb.Append(integer.ToString(CultureInfo.InvariantCulture));
            }
            else if (value.TryGetDecimalNumber(out var number))
            {
                sb.Append(number.ToString(CultureInfo.InvariantCulture));
            }
        }

        return sb.Append(')').ToString();
    }
}
