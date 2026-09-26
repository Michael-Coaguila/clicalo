using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Clicalo.Domain.Privacy;
using Serilog.Core;
using Serilog.Events;

namespace Clicalo.Infrastructure.Logging;

/// <summary>
/// Redaction by type in the log (blueprint §9.4, ADR-0008, LOG-001): a value of a type marked
/// <see cref="SensitiveAttribute"/> (<see cref="SecretText"/> among them) or a <see cref="Sensitive{T}"/> is always
/// written as its redacted <c>ToString()</c>, even when a template asks to destructure it (<c>{@Value}</c>) and even
/// nested inside another object; a <see cref="Uri"/> keeps only its scheme and host. CLC0003 stops this at compile time;
/// this is the second line.
/// </summary>
internal sealed class SensitiveDestructuringPolicy : IDestructuringPolicy
{
    /// <inheritdoc />
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is Uri uri)
        {
            result = new ScalarValue(
                uri.IsAbsoluteUri ? uri.Scheme + "://" + uri.Host + "/…" : "[url]"
            );
            return true;
        }

        if (IsSensitive(value.GetType()))
        {
            result = new ScalarValue(value.ToString());
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>Whether values of <paramref name="type"/> must be redacted.</summary>
    /// <param name="type">A type.</param>
    internal static bool IsSensitive(Type type) =>
        type.GetCustomAttribute<SensitiveAttribute>(inherit: true) is not null
        || type.GetInterfaces().Any(i => i.GetCustomAttribute<SensitiveAttribute>() is not null)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Sensitive<>));
}
