using System.Globalization;
using System.Text;

namespace Clicalo.Domain.Search;

/// <summary>
/// How the panel search compares text (BUS-004): without case and without accents, so «numero» finds «Número» and
/// «NEGR» finds «Negrita». Pure and culture-invariant.
/// </summary>
public static class SearchText
{
    /// <summary>
    /// The comparable form of <paramref name="text"/>: decomposed, without combining marks, in lower case. Surrounding
    /// spaces are kept: a query is trimmed by <see cref="ShortcutSearch"/>, not here.
    /// </summary>
    /// <param name="text">Any text; null counts as empty.</param>
    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Whether <paramref name="text"/> contains <paramref name="foldedQuery"/> once folded.</summary>
    /// <param name="text">The text to look in (a name or a combination).</param>
    /// <param name="foldedQuery">A query already passed through <see cref="Fold"/>; never empty.</param>
    public static bool Contains(string? text, string foldedQuery) =>
        Fold(text).Contains(foldedQuery, StringComparison.Ordinal);
}
