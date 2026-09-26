using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace Clicalo.Analyzers.Common;

/// <summary>
/// Root namespaces (for example <c>Clicalo.Application</c>) that a rule applies to, nested namespaces included.
/// Create one per compilation: it caches the answer per namespace symbol.
/// </summary>
internal sealed class NamespaceScope
{
    private readonly string[] _roots;
    private readonly ConcurrentDictionary<INamespaceSymbol, bool> _cache = new(
        SymbolEqualityComparer.Default
    );

    public NamespaceScope(params string[] roots)
    {
        _roots = roots;
    }

    /// <summary>True when <paramref name="symbol"/> (or the namespace that contains it) is inside the scope.</summary>
    public bool Contains(ISymbol? symbol)
    {
        var ns = symbol as INamespaceSymbol ?? symbol?.ContainingNamespace;
        if (ns is null || ns.IsGlobalNamespace)
        {
            return false;
        }

        return _cache.GetOrAdd(ns, key => Contains(key.ToDisplayString()));
    }

    /// <summary>True when the fully qualified namespace or type name is inside the scope.</summary>
    public bool Contains(string qualifiedName)
    {
        foreach (var root in _roots)
        {
            if (NamespaceNames.IsSameOrChild(qualifiedName, root))
            {
                return true;
            }
        }

        return false;
    }
}
