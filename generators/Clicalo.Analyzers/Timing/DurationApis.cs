using System;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Clicalo.Analyzers.Timing;

/// <summary>
/// Knows which values are durations and which constants are allowed to express them. A value is a duration when it is
/// <list type="bullet">
/// <item>an argument of a <c>TimeSpan</c> constructor or of a <c>TimeSpan.From*</c> factory;</item>
/// <item>the argument of <c>Add{Days…Ticks}</c> on <c>DateTime</c>, <c>DateTimeOffset</c> or <c>TimeOnly</c>;</item>
/// <item>a numeric parameter, property or field whose name ends with a time word (<see cref="TimeWords"/>), which
/// covers <c>Task.Delay(int millisecondsDelay)</c>, <c>Thread.Sleep(int millisecondsTimeout)</c>,
/// <c>new CancellationTokenSource(int millisecondsDelay)</c>, <c>Timer(…, int dueTime, int period)</c> and our own APIs.</item>
/// </list>
/// A duration is allowed only when it is not a compile-time constant, or when its constant expression references a
/// member of <c>Clicalo.Domain.Timings</c> or <c>System.Threading.Timeout</c>.
/// </summary>
internal sealed class DurationApis
{
    private static readonly string[] TimeWords =
    [
        "Milliseconds",
        "Ms",
        "Seconds",
        "Minutes",
        "Hours",
        "Days",
        "Delay",
        "Timeout",
        "Interval",
        "Period",
        "DueTime",
    ];

    private static readonly string[] AddMethods =
    [
        "AddDays",
        "AddHours",
        "AddMinutes",
        "AddSeconds",
        "AddMilliseconds",
        "AddMicroseconds",
        "AddTicks",
    ];

    private readonly INamedTypeSymbol? _timeSpan;
    private readonly INamedTypeSymbol? _dateTime;
    private readonly INamedTypeSymbol? _dateTimeOffset;
    private readonly INamedTypeSymbol? _timeOnly;
    private readonly INamedTypeSymbol? _timings;
    private readonly INamedTypeSymbol? _timeout;

    public DurationApis(Compilation compilation)
    {
        _timeSpan = compilation.GetTypeByMetadataName(KnownTypeNames.TimeSpan);
        _dateTime = compilation.GetTypeByMetadataName(KnownTypeNames.DateTime);
        _dateTimeOffset = compilation.GetTypeByMetadataName(KnownTypeNames.DateTimeOffset);
        _timeOnly = compilation.GetTypeByMetadataName(KnownTypeNames.TimeOnly);
        _timings = compilation.GetTypeByMetadataName(KnownTypeNames.Timings);
        _timeout = compilation.GetTypeByMetadataName(KnownTypeNames.Timeout);
    }

    /// <summary>True when the argument bound to <paramref name="parameter"/> of <paramref name="method"/> is a duration.</summary>
    public bool IsDurationArgument(IMethodSymbol method, IParameterSymbol? parameter)
    {
        var type = method.ContainingType?.OriginalDefinition;
        if (Is(type, _timeSpan))
        {
            return method.MethodKind == MethodKind.Constructor
                || (method.IsStatic && method.Name.StartsWith("From", StringComparison.Ordinal));
        }

        if (
            (Is(type, _dateTime) || Is(type, _dateTimeOffset) || Is(type, _timeOnly))
            && IsAddMethod(method)
        )
        {
            return parameter is not null && parameter.Ordinal == 0;
        }

        return parameter is not null && IsDurationMember(parameter.Name, parameter.Type);
    }

    /// <summary>True when a member or parameter called <paramref name="name"/> of type <paramref name="type"/> holds a duration.</summary>
    public static bool IsDurationMember(string name, ITypeSymbol type) =>
        IsNumeric(type) && IdentifierNames.EndsWithAnyWord(name, TimeWords);

    /// <summary>
    /// True when <paramref name="value"/> is a numeric compile-time constant that does not come from
    /// <c>Timings</c> or <c>Timeout</c>: a literal, literal arithmetic, or a constant declared anywhere else.
    /// </summary>
    public bool IsLiteralDuration(IOperation value)
    {
        var constant = value.ConstantValue;
        if (!constant.HasValue || !ConstantValues.IsNumber(constant.Value))
        {
            return false;
        }

        foreach (var node in value.DescendantsAndSelf())
        {
            if (
                node is IFieldReferenceOperation reference
                && (IsWithin(reference.Field, _timings) || IsWithin(reference.Field, _timeout))
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAddMethod(IMethodSymbol method) =>
        !method.IsStatic && Array.IndexOf(AddMethods, method.Name) >= 0;

    private static bool IsNumeric(ITypeSymbol type)
    {
        if (
            type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
            } nullable
        )
        {
            type = nullable.TypeArguments[0];
        }

        return type.SpecialType
            is SpecialType.System_SByte
                or SpecialType.System_Byte
                or SpecialType.System_Int16
                or SpecialType.System_UInt16
                or SpecialType.System_Int32
                or SpecialType.System_UInt32
                or SpecialType.System_Int64
                or SpecialType.System_UInt64
                or SpecialType.System_Single
                or SpecialType.System_Double
                or SpecialType.System_Decimal;
    }

    private static bool IsWithin(ISymbol member, INamedTypeSymbol? type) =>
        type is not null && member.IsWithin(type);

    private static bool Is(INamedTypeSymbol? type, INamedTypeSymbol? candidate) =>
        type is not null
        && candidate is not null
        && SymbolEqualityComparer.Default.Equals(type, candidate);
}
