using System;
using System.Collections.Generic;

namespace Clicalo.Analyzers.Common;

/// <summary>Matching of identifiers by their last camel-case word (<c>WindowTitle</c> ends with the word <c>Title</c>).</summary>
internal static class IdentifierNames
{
    /// <summary>
    /// True when <paramref name="name"/>, ignoring leading underscores, is <paramref name="word"/> in any case or
    /// ends with it as a Pascal-case word: <c>SearchText</c> and <c>_text</c> match <c>Text</c>; <c>Context</c> does not.
    /// </summary>
    public static bool EndsWithWord(string name, string word)
    {
        var trimmed = name.TrimStart('_');
        if (string.Equals(trimmed, word, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return trimmed.Length > word.Length && trimmed.EndsWith(word, StringComparison.Ordinal);
    }

    /// <summary>True when <paramref name="name"/> ends with any of <paramref name="words"/> (see <see cref="EndsWithWord"/>).</summary>
    public static bool EndsWithAnyWord(string name, IReadOnlyList<string> words)
    {
        foreach (var word in words)
        {
            if (EndsWithWord(name, word))
            {
                return true;
            }
        }

        return false;
    }
}
