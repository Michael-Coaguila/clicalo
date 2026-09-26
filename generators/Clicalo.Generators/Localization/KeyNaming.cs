using System;
using System.Collections.Immutable;

namespace Clicalo.Generators.Localization;

/// <summary>
/// Key grammar and C# member naming. A physical key is <c>base</c> or <c>base_category</c>, where
/// <c>base = [A-Za-z][A-Za-z0-9]*</c> (the original handoff names) and <c>category</c> is a CLDR plural category.
/// </summary>
internal static class KeyNaming
{
    /// <summary>Name of the generated static class with one member per key.</summary>
    public const string MessagesClass = "L";

    /// <summary>
    /// Member names a key must not produce inside the generated static class: the class itself and the members
    /// every class inherits from <see cref="object"/>, which would be hidden.
    /// </summary>
    public static readonly ImmutableArray<string> ReservedMemberNames =
    [
        MessagesClass,
        "Equals",
        "Finalize",
        "GetHashCode",
        "GetType",
        "MemberwiseClone",
        "ReferenceEquals",
        "ToString",
    ];

    /// <summary>True for a valid base key (without plural suffix).</summary>
    public static bool IsValidBaseKey(string key)
    {
        if (key.Length == 0 || !IsLetter(key[0]))
        {
            return false;
        }

        foreach (var c in key)
        {
            if (!IsLetter(c) && c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Splits a physical key into its base and optional plural category. Returns <c>false</c> when the key does
    /// not follow the grammar.
    /// </summary>
    public static bool TrySplit(string key, out string baseKey, out string? category)
    {
        baseKey = key;
        category = null;
        var separator = key.IndexOf('_');
        if (separator < 0)
        {
            return IsValidBaseKey(key);
        }

        baseKey = key.Substring(0, separator);
        category = key.Substring(separator + 1);
        return IsValidBaseKey(baseKey) && PluralCategories.IsCategory(category);
    }

    /// <summary>C# member name of a base key: the key with its first letter in upper case (<c>migT</c> → <c>MigT</c>).</summary>
    public static string ToMemberName(string baseKey) =>
        baseKey.Length == 0 ? baseKey : char.ToUpperInvariant(baseKey[0]) + baseKey.Substring(1);

    /// <summary>True when the member name would clash with the generated class or with inherited members.</summary>
    public static bool IsReserved(string memberName) =>
        ReservedMemberNames.Contains(memberName, StringComparer.Ordinal);

    private static bool IsLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
}
