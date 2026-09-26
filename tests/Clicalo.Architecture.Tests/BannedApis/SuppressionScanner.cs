using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Clicalo.Architecture.Tests.BannedApis;

/// <summary>
/// Finds the RS0030 suppressions of a C# source file on its syntax tree, so the layout of the code does not matter: an
/// attribute split over several lines by CSharpier, a qualified attribute name or a pragma with several ids are all
/// seen. Strings and comments that only mention RS0030 are not suppressions.
/// </summary>
internal static partial class SuppressionScanner
{
    private const int MinimumJustificationLength = 10;

    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.Preview,
        DocumentationMode.None
    );

    private static readonly string[] SuppressAttributes =
    [
        "SuppressMessage",
        "SuppressMessageAttribute",
        "UnconditionalSuppressMessage",
        "UnconditionalSuppressMessageAttribute",
    ];

    public static IEnumerable<SuppressionFinding> Scan(string file, string source)
    {
        var root = CSharpSyntaxTree.ParseText(source, ParseOptions).GetRoot();
        var pragmas = root.DescendantTrivia(descendIntoTrivia: true)
            .Select(trivia => trivia.GetStructure())
            .OfType<PragmaWarningDirectiveTriviaSyntax>()
            .ToList();
        var disables = pragmas
            .Where(pragma => pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            .Where(CoversRs0030)
            .Select(pragma => new SuppressionFinding(
                file,
                LineOf(pragma),
                Classify(pragma, pragmas)
            ));
        var attributes = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attribute => IsSuppressAttribute(attribute) && MentionsRs0030(attribute))
            .Select(attribute => new SuppressionFinding(
                file,
                LineOf(attribute),
                Classify(attribute)
            ));

        return [.. disables.Concat(attributes).OrderBy(finding => finding.Line)];
    }

    private static SuppressionKind Classify(
        PragmaWarningDirectiveTriviaSyntax disable,
        List<PragmaWarningDirectiveTriviaSyntax> pragmas
    )
    {
        if (disable.ErrorCodes.Count == 0)
        {
            return SuppressionKind.Blanket;
        }

        var justification = string.Concat(
            disable
                .DescendantTrivia()
                .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                .Select(trivia => trivia.ToString().TrimStart('/').Trim())
        );
        if (justification.Length < MinimumJustificationLength)
        {
            return SuppressionKind.Unjustified;
        }

        var restored = pragmas.Any(pragma =>
            pragma.SpanStart > disable.SpanStart
            && pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.RestoreKeyword)
            && CoversRs0030(pragma)
        );
        return restored ? SuppressionKind.Scoped : SuppressionKind.Unrestored;
    }

    private static SuppressionKind Classify(AttributeSyntax attribute)
    {
        if (
            attribute.Parent is AttributeListSyntax { Target.Identifier: var target }
            && (
                target.IsKind(SyntaxKind.AssemblyKeyword) || target.IsKind(SyntaxKind.ModuleKeyword)
            )
        )
        {
            return SuppressionKind.Global;
        }

        var justified = attribute.ArgumentList!.Arguments.Any(argument =>
            string.Equals(
                argument.NameEquals?.Name.Identifier.ValueText,
                "Justification",
                StringComparison.Ordinal
            )
            && argument.Expression is LiteralExpressionSyntax literal
            && literal.Token.ValueText.Trim().Length >= MinimumJustificationLength
        );
        return justified ? SuppressionKind.Scoped : SuppressionKind.Unjustified;
    }

    /// <summary>A pragma that names RS0030, or names nothing and so covers every warning.</summary>
    private static bool CoversRs0030(PragmaWarningDirectiveTriviaSyntax pragma) =>
        pragma.ErrorCodes.Count == 0
        || pragma.ErrorCodes.Any(code => Rs0030().IsMatch(code.ToString()));

    private static bool IsSuppressAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => string.Empty,
        };
        return SuppressAttributes.Contains(name, StringComparer.Ordinal);
    }

    private static bool MentionsRs0030(AttributeSyntax attribute) =>
        attribute.ArgumentList is { } arguments
        && arguments.Arguments.Any(argument =>
            argument.NameEquals is null
            && argument.Expression is LiteralExpressionSyntax literal
            && Rs0030().IsMatch(literal.Token.ValueText)
        );

    private static int LineOf(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    [GeneratedRegex(@"\bRS0030\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Rs0030();
}
