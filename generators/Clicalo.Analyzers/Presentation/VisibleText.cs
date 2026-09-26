using System.Collections.Generic;
using Clicalo.Analyzers.Common;

namespace Clicalo.Analyzers.Presentation;

/// <summary>What counts as visible text and which members, parameters and XAML attributes show it.</summary>
internal static class VisibleText
{
    // Longer literals are shortened in the message; the location already points at the full text.
    private const int MaxShownLength = 40;

    /// <summary>
    /// Names whose last word marks a visible text: <c>Label</c>, <c>Title</c>, <c>Message</c>, <c>Text</c>,
    /// <c>Tooltip</c>, <c>Description</c> (the rule's contract), plus <c>Caption</c>, <c>Header</c> and <c>Content</c>,
    /// which WPF uses for the same purpose.
    /// </summary>
    public static readonly IReadOnlyList<string> TextWords =
    [
        "Label",
        "Title",
        "Message",
        "Text",
        "Tooltip",
        "ToolTip",
        "Description",
        "Caption",
        "Header",
        "Content",
    ];

    /// <summary>True when a member, parameter or attached property called <paramref name="name"/> shows text.</summary>
    public static bool IsTextName(string name) => IdentifierNames.EndsWithAnyWord(name, TextWords);

    /// <summary>
    /// True when <paramref name="text"/> reads as language: it has at least one letter. Glyphs from the icon font,
    /// punctuation and separators such as <c>·</c> or <c>—</c> are not language and stay allowed.
    /// </summary>
    public static bool HasLetter(string text)
    {
        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The text as shown in a diagnostic message: trimmed and shortened.</summary>
    public static string ForMessage(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= MaxShownLength
            ? trimmed
            : trimmed.Substring(0, MaxShownLength) + "…";
    }
}
