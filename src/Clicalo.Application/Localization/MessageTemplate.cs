using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Clicalo.Application.Localization;

/// <summary>
/// A text of <c>data/i18n</c> parsed once into literal and placeholder segments. Grammar (the same as the build-time
/// <c>TemplateSyntax</c> of the generator): named placeholders <c>{name}</c> with <c>name = [a-z][A-Za-z0-9]*</c>,
/// <c>{{</c> and <c>}}</c> for literal braces, and no other brace.
/// </summary>
internal sealed class MessageTemplate
{
    private MessageTemplate(string source, ImmutableArray<TemplateSegment> segments)
    {
        Source = source;
        Segments = segments;
        Placeholders =
        [
            .. segments
                .Where(static s => s.IsPlaceholder)
                .Select(static s => s.Text)
                .Distinct(StringComparer.Ordinal),
        ];
    }

    /// <summary>The text as written in the strings file.</summary>
    public string Source { get; }

    /// <summary>Literal and placeholder segments, in order.</summary>
    public ImmutableArray<TemplateSegment> Segments { get; }

    /// <summary>Distinct placeholder names, in order of first appearance.</summary>
    public ImmutableArray<string> Placeholders { get; }

    /// <summary>Parses a text; on failure <paramref name="error"/> says what and where.</summary>
    public static bool TryParse(
        string text,
        [NotNullWhen(true)] out MessageTemplate? template,
        [NotNullWhen(false)] out string? error
    )
    {
        ArgumentNullException.ThrowIfNull(text);
        var segments = ImmutableArray.CreateBuilder<TemplateSegment>();
        var literal = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if ((c == '{' || c == '}') && i + 1 < text.Length && text[i + 1] == c)
            {
                literal.Append(c);
                i += 2;
                continue;
            }

            if (c == '}')
            {
                return Fail(i, "'}' does not close a placeholder", out template, out error);
            }

            if (c != '{')
            {
                literal.Append(c);
                i++;
                continue;
            }

            var close = text.IndexOf('}', i + 1);
            var name = close < 0 ? string.Empty : text[(i + 1)..close];
            if (close < 0 || !IsValidName(name))
            {
                return Fail(i, "'{' does not open a valid placeholder", out template, out error);
            }

            if (literal.Length > 0)
            {
                segments.Add(new TemplateSegment(literal.ToString(), false));
                literal.Clear();
            }

            segments.Add(new TemplateSegment(name, true));
            i = close + 1;
        }

        if (literal.Length > 0)
        {
            segments.Add(new TemplateSegment(literal.ToString(), false));
        }

        template = new MessageTemplate(text, segments.ToImmutable());
        error = null;
        return true;
    }

    private static bool IsValidName(string name) =>
        name.Length > 0 && char.IsAsciiLetterLower(name[0]) && name.All(char.IsAsciiLetterOrDigit);

    private static bool Fail(
        int index,
        string reason,
        out MessageTemplate? template,
        out string? error
    )
    {
        template = null;
        error = string.Create(CultureInfo.InvariantCulture, $"{reason} (position {index + 1})");
        return false;
    }
}
