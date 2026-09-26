using System;

namespace Clicalo.Analyzers.Common;

/// <summary>Comparison of dotted namespace names.</summary>
internal static class NamespaceNames
{
    /// <summary>
    /// True when <paramref name="name"/> is <paramref name="root"/> or lies inside it: <c>Clicalo.Application.Engine</c>
    /// is inside <c>Clicalo.Application</c>; <c>Clicalo.ApplicationTools</c> is not.
    /// </summary>
    public static bool IsSameOrChild(string name, string root) =>
        name.StartsWith(root, StringComparison.Ordinal)
        && (name.Length == root.Length || name[root.Length] == '.');
}
