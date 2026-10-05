using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Duplicates;

/// <summary>
/// Name comparisons of the repeated-shortcut rules. Names are compared in Spanish <b>and</b> in English, so the result
/// never depends on the interface language (REP-002); a missing language falls back to the other one. Surrounding
/// spaces and letter case are ignored (ordinal, culture-independent).
/// </summary>
public static class ShortcutNames
{
    /// <summary>Whether two names are the same in Spanish and in English (REP-002, rule c).</summary>
    /// <param name="left">First name.</param>
    /// <param name="right">Second name.</param>
    public static bool AreSame(LocalizedText left, LocalizedText right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return Same(left, right, LangCode.Es, LangCode.En)
            && Same(left, right, LangCode.En, LangCode.Es);
    }

    /// <summary>Whether two names are the same in Spanish or in English («[moveAlways]», REP-005).</summary>
    /// <param name="left">First name.</param>
    /// <param name="right">Second name.</param>
    public static bool ShareAnyLanguage(LocalizedText left, LocalizedText right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return Same(left, right, LangCode.Es, LangCode.En)
            || Same(left, right, LangCode.En, LangCode.Es);
    }

    /// <summary>A key equal for names that <see cref="AreSame"/> considers the same.</summary>
    /// <param name="name">The name.</param>
    internal static (string Spanish, string English) KeyOf(LocalizedText name) =>
        (
            Normalize(name.Get(LangCode.Es, LangCode.En)),
            Normalize(name.Get(LangCode.En, LangCode.Es))
        );

    private static bool Same(
        LocalizedText left,
        LocalizedText right,
        LangCode language,
        LangCode fallback
    ) =>
        string.Equals(
            left.Get(language, fallback).Trim(),
            right.Get(language, fallback).Trim(),
            StringComparison.OrdinalIgnoreCase
        );

    private static string Normalize(string text) => text.Trim().ToUpperInvariant();
}
