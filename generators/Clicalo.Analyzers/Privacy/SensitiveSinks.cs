using System.Collections.Concurrent;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;

namespace Clicalo.Analyzers.Privacy;

/// <summary>
/// The places a sensitive value must never reach (LOG-001):
/// <list type="bullet">
/// <item>every method and constructor declared in <c>Microsoft.Extensions.Logging</c> or <c>Serilog</c> (and nested namespaces);</item>
/// <item>methods of types that implement <c>Microsoft.Extensions.Logging.ILogger</c>;</item>
/// <item>methods marked <c>[LoggerMessage]</c> and delegates whose first parameter is an <c>ILogger</c> (<c>LoggerMessage.Define</c>);</item>
/// <item><c>System.Diagnostics.Debug</c>, <c>System.Diagnostics.Trace</c> and every <c>EventSource</c> (ETW);</item>
/// <item>constructors of <c>System.Exception</c> and its derived types.</item>
/// </list>
/// Create one per compilation: it caches the per-type answer.
/// </summary>
internal sealed class SensitiveSinks
{
    private const string LoggingNamespace = "Microsoft.Extensions.Logging";
    private const string SerilogNamespace = "Serilog";

    private readonly INamedTypeSymbol? _logger;
    private readonly INamedTypeSymbol? _loggerMessageAttribute;
    private readonly INamedTypeSymbol? _exception;
    private readonly INamedTypeSymbol? _eventSource;
    private readonly INamedTypeSymbol? _debug;
    private readonly INamedTypeSymbol? _trace;
    private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _sinkTypes = new(
        SymbolEqualityComparer.Default
    );

    public SensitiveSinks(Compilation compilation)
    {
        _logger = compilation.GetTypeByMetadataName(KnownTypeNames.Logger);
        _loggerMessageAttribute = compilation.GetTypeByMetadataName(
            KnownTypeNames.LoggerMessageAttribute
        );
        _exception = compilation.GetTypeByMetadataName(KnownTypeNames.Exception);
        _eventSource = compilation.GetTypeByMetadataName(KnownTypeNames.EventSource);
        _debug = compilation.GetTypeByMetadataName(KnownTypeNames.Debug);
        _trace = compilation.GetTypeByMetadataName(KnownTypeNames.Trace);
    }

    /// <summary>True when passing a value to <paramref name="method"/> writes it to a log, a trace or an exception.</summary>
    public bool IsSink(IMethodSymbol method)
    {
        var type = method.ContainingType;
        if (type is null)
        {
            return false;
        }

        if (method.MethodKind == MethodKind.Constructor)
        {
            return (_exception is not null && type.IsOrInheritsFrom(_exception))
                || IsSinkType(type);
        }

        if (method.MethodKind == MethodKind.DelegateInvoke)
        {
            return _logger is not null
                && !method.Parameters.IsEmpty
                && method.Parameters[0].Type.IsOrImplements(_logger);
        }

        return IsLoggerMessage(method) || IsSinkType(type);
    }

    private bool IsSinkType(INamedTypeSymbol type) =>
        _sinkTypes.GetOrAdd(type.OriginalDefinition, ComputeSinkType);

    private bool ComputeSinkType(INamedTypeSymbol type) =>
        type.IsDeclaredIn(LoggingNamespace)
        || type.IsDeclaredIn(SerilogNamespace)
        || (_logger is not null && type.IsOrImplements(_logger))
        || (_eventSource is not null && type.IsOrInheritsFrom(_eventSource))
        || SymbolEqualityComparer.Default.Equals(type, _debug)
        || SymbolEqualityComparer.Default.Equals(type, _trace);

    private bool IsLoggerMessage(IMethodSymbol method)
    {
        if (_loggerMessageAttribute is null)
        {
            return false;
        }

        // The attribute sits on the partial definition; the call may bind to either part.
        return HasLoggerMessage(method.OriginalDefinition)
            || (method.PartialDefinitionPart is { } definition && HasLoggerMessage(definition))
            || (
                method.PartialImplementationPart is { } implementation
                && HasLoggerMessage(implementation)
            );
    }

    private bool HasLoggerMessage(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (
                SymbolEqualityComparer.Default.Equals(
                    attribute.AttributeClass,
                    _loggerMessageAttribute
                )
            )
            {
                return true;
            }
        }

        return false;
    }
}
