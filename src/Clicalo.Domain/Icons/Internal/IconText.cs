using System.Globalization;
using System.Text;

namespace Clicalo.Domain.Icons.Internal;

/// <summary>
/// How the icon picker compares words (EDI-004, EDI-005): lower case and without diacritics, so «titulo» finds the
/// tag «título» and «ACCIÓN» the tag «accion». Pure and culture-invariant.
/// </summary>
internal static class IconText
{
    /// <summary>The comparable form of <paramref name="text"/>; null counts as empty.</summary>
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

    /// <summary>The alphanumeric words of an already folded text, in order.</summary>
    public static IEnumerable<string> Words(string folded)
    {
        var start = -1;
        for (var i = 0; i <= folded.Length; i++)
        {
            var inWord = i < folded.Length && char.IsLetterOrDigit(folded[i]);
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                yield return folded[start..i];
                start = -1;
            }
        }
    }
}
