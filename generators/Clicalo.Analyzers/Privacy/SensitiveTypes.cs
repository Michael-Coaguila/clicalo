using Microsoft.CodeAnalysis;

namespace Clicalo.Analyzers.Privacy;

/// <summary>
/// Decides whether a type holds sensitive data (D14): <c>Sensitive&lt;T&gt;</c>, <c>SecretText</c>, any type that carries
/// <c>[Sensitive]</c> (directly, on a base type or on an implemented interface), and any type built from them:
/// arrays, <c>Nullable&lt;T&gt;</c>, tuples, collections and type parameters constrained to them. Delegate types are
/// not data and never count as sensitive.
/// </summary>
internal sealed class SensitiveTypes
{
    // Type arguments and constraints nest shallowly; the limit also breaks cycles such as T : IComparable<T>.
    private const int MaxDepth = 8;

    private readonly INamedTypeSymbol? _attribute;
    private readonly INamedTypeSymbol? _secretText;

    private SensitiveTypes(
        INamedTypeSymbol? attribute,
        INamedTypeSymbol? sensitiveOfT,
        INamedTypeSymbol? secretText
    )
    {
        _attribute = attribute;
        SensitiveOfT = sensitiveOfT;
        _secretText = secretText;
    }

    /// <summary>The <c>Sensitive&lt;T&gt;</c> wrapper, when the compilation has it.</summary>
    public INamedTypeSymbol? SensitiveOfT { get; }

    /// <summary>Binds the privacy contracts of <paramref name="compilation"/>; null when none of them is referenced.</summary>
    public static SensitiveTypes? Create(Compilation compilation)
    {
        var attribute = compilation.GetTypeByMetadataName(KnownTypeNames.SensitiveAttribute);
        var sensitiveOfT = compilation.GetTypeByMetadataName(KnownTypeNames.SensitiveOfT);
        var secretText = compilation.GetTypeByMetadataName(KnownTypeNames.SecretText);
        return attribute is null && sensitiveOfT is null && secretText is null
            ? null
            : new SensitiveTypes(attribute, sensitiveOfT, secretText);
    }

    public bool IsSensitive(ITypeSymbol? type) => type is not null && IsSensitive(type, 0);

    private bool IsSensitive(ITypeSymbol type, int depth)
    {
        if (depth > MaxDepth)
        {
            return false;
        }

        switch (type)
        {
            case IArrayTypeSymbol array:
                return IsSensitive(array.ElementType, depth + 1);
            case IPointerTypeSymbol pointer:
                return IsSensitive(pointer.PointedAtType, depth + 1);
            case ITypeParameterSymbol parameter:
                foreach (var constraint in parameter.ConstraintTypes)
                {
                    if (IsSensitive(constraint, depth + 1))
                    {
                        return true;
                    }
                }

                return false;
            case INamedTypeSymbol named:
                if (Is(named, SensitiveOfT) || Is(named, _secretText) || CarriesAttribute(named))
                {
                    return true;
                }

                // A delegate is behavior, not data: a log formatter Func<SecretText, Exception, string> carries nothing.
                if (named.TypeKind == TypeKind.Delegate)
                {
                    return false;
                }

                foreach (var argument in named.TypeArguments)
                {
                    if (IsSensitive(argument, depth + 1))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    private bool CarriesAttribute(INamedTypeSymbol type)
    {
        if (_attribute is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (HasAttribute(current))
            {
                return true;
            }
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (HasAttribute(implemented))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAttribute(INamedTypeSymbol type)
    {
        foreach (var attribute in type.OriginalDefinition.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _attribute))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Is(INamedTypeSymbol type, INamedTypeSymbol? candidate) =>
        candidate is not null
        && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, candidate);
}
