using Microsoft.CodeAnalysis;

namespace Clicalo.Analyzers.Common;

/// <summary>Type relationships the rules need, with type parameters judged by their constraints.</summary>
internal static class SymbolExtensions
{
    // Constraint chains are shallow in practice; the limit only protects against pathological declarations.
    private const int MaxConstraintDepth = 8;

    /// <summary>True when <paramref name="type"/> is <paramref name="baseType"/> or derives from it.</summary>
    public static bool IsOrInheritsFrom(this ITypeSymbol? type, INamedTypeSymbol baseType) =>
        IsOrInheritsFrom(type, baseType, 0);

    /// <summary>True when <paramref name="type"/> is the interface <paramref name="interfaceType"/> or implements it.</summary>
    public static bool IsOrImplements(this ITypeSymbol? type, INamedTypeSymbol interfaceType) =>
        IsOrImplements(type, interfaceType, 0);

    /// <summary>True when <paramref name="symbol"/> is <paramref name="type"/>, a member of it or a type nested in it.</summary>
    public static bool IsWithin(this ISymbol? symbol, INamedTypeSymbol type)
    {
        for (
            var current = symbol as INamedTypeSymbol ?? symbol?.ContainingType;
            current is not null;
            current = current.ContainingType
        )
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the type is declared in <paramref name="namespaceName"/> or in one of its nested namespaces.</summary>
    public static bool IsDeclaredIn(this ITypeSymbol type, string namespaceName)
    {
        var name = type.ContainingNamespace?.ToDisplayString();
        return name is not null && NamespaceNames.IsSameOrChild(name, namespaceName);
    }

    private static bool IsOrInheritsFrom(ITypeSymbol? type, INamedTypeSymbol baseType, int depth)
    {
        if (type is null || depth > MaxConstraintDepth)
        {
            return false;
        }

        if (type is ITypeParameterSymbol parameter)
        {
            foreach (var constraint in parameter.ConstraintTypes)
            {
                if (IsOrInheritsFrom(constraint, baseType, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOrImplements(ITypeSymbol? type, INamedTypeSymbol interfaceType, int depth)
    {
        if (type is null || depth > MaxConstraintDepth)
        {
            return false;
        }

        if (type is ITypeParameterSymbol parameter)
        {
            foreach (var constraint in parameter.ConstraintTypes)
            {
                if (IsOrImplements(constraint, interfaceType, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, interfaceType))
        {
            return true;
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (
                SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, interfaceType)
            )
            {
                return true;
            }
        }

        return false;
    }
}
