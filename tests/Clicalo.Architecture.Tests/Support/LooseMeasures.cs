using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>
/// Finds the loose measures of a source file of <c>Clicalo.UI.Wpf</c> (D12, direction C): numeric literals written
/// where a size, a margin, a radius or a type size goes, instead of a token of <c>data/tokens/design-system.json</c>
/// (<c>Space</c>, <c>ControlHeight</c>, <c>Radii</c>, <c>IconBox</c>, <c>Stroke</c>, <c>ComponentSize</c>,
/// <c>TextStyles</c>). Zero is never a measure, nor a star weight of a grid.
/// </summary>
/// <remarks>
/// A literal counts in: <c>new Thickness</c>, <c>CornerRadius</c>, <c>GridLength</c> and <c>Size</c>; an assignment or
/// a named argument whose target is a measure (<c>Width</c>, <c>Height</c>, <c>MinWidth</c>, <c>FontSize</c>,
/// <c>IconSize</c>, <c>radius</c>, <c>gap</c>, <c>px</c>…); an argument of the builders that take a measure
/// (<c>Ui.Text</c>, <c>Ui.Icon</c>, <c>Ui.Column</c>, <c>ThemeKeys.TextSize</c>…); and a <c>double</c> constant or
/// field initialized with a literal, which is a named loose measure.
/// </remarks>
internal static class LooseMeasures
{
    /// <summary>Types whose constructor takes measures.</summary>
    private static readonly HashSet<string> MeasureTypes = new(StringComparer.Ordinal)
    {
        "Thickness",
        "CornerRadius",
        "GridLength",
        "Size",
    };

    /// <summary>Properties and parameters that hold a measure.</summary>
    private static readonly HashSet<string> MeasureNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Width",
        "Height",
        "MinWidth",
        "MinHeight",
        "MaxWidth",
        "MaxHeight",
        "FontSize",
        "IconSize",
        "Size",
        "LineHeight",
        "StrokeThickness",
        "RadiusX",
        "RadiusY",
        "Radius",
        "Gap",
        "Px",
        "DesignPx",
        "IconPx",
        "Spacing",
        "ItemHeight",
        "MinItemWidth",
        "VisualWidth",
        "VisualHeight",
        "CaptionHeight",
    };

    /// <summary>Methods whose numeric arguments are measures (the builders of the views).</summary>
    private static readonly HashSet<string> MeasureMethods = new(StringComparer.Ordinal)
    {
        "Text",
        "Icon",
        "CategoryIcon",
        "Separator",
        "Column",
        "Row",
        "Wrap",
        "Columns",
        "Card",
        "IconLabel",
        "Line",
        "TextSize",
        "ScaledTextSize",
        "SmallButton",
        "Labeled",
        "Stack",
        "Chrome",
    };

    /// <summary>Each loose measure of <paramref name="source"/>: its line and the literal.</summary>
    public static IReadOnlyList<(int Line, string Literal)> In(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var found = new List<(int, string)>();
        foreach (var literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (!literal.IsKind(SyntaxKind.NumericLiteralExpression) || IsZero(literal))
            {
                continue;
            }

            if (IsMeasure(literal))
            {
                var line = literal.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                found.Add((line, Signed(literal)));
            }
        }

        return found;
    }

    private static bool IsZero(LiteralExpressionSyntax literal) =>
        literal.Token.Value is IConvertible value
        && Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture) == 0;

    private static string Signed(LiteralExpressionSyntax literal) =>
        literal.Parent
            is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression }
            ? "-" + literal.Token.Text
            : literal.Token.Text;

    private static bool IsMeasure(LiteralExpressionSyntax literal)
    {
        // A literal that scales or compares a named value (Space.S1 / 2, lines * 3, count > 1) derives from it: what
        // counts is a measure written as a number.
        SyntaxNode operand = literal;
        while (operand.Parent is PrefixUnaryExpressionSyntax or ParenthesizedExpressionSyntax)
        {
            operand = operand.Parent;
        }

        if (
            operand.Parent is BinaryExpressionSyntax binary
            && (ReferenceEquals(binary.Left, operand) ? binary.Right : binary.Left)
                .DescendantNodesAndSelf()
                .Any(static n => n is IdentifierNameSyntax)
        )
        {
            return false;
        }

        for (SyntaxNode? node = literal.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case ArgumentSyntax { NameColon: { } name }
                    when MeasureNames.Contains(name.Name.Identifier.Text):
                    return true;
                case BaseObjectCreationExpressionSyntax creation:
                    return IsMeasureCreation(creation, literal);
                case AssignmentExpressionSyntax assignment:
                    return MeasureNames.Contains(TargetName(assignment.Left));
                case InvocationExpressionSyntax invocation:
                    return MeasureMethods.Contains(MethodName(invocation.Expression));
                case VariableDeclarationSyntax declaration:
                    return declaration.Type is PredefinedTypeSyntax { Keyword.Text: "double" }
                        && declaration.Parent is FieldDeclarationSyntax;
                case StatementSyntax or MemberDeclarationSyntax or LambdaExpressionSyntax:
                    return false;
                default:
                    break;
            }
        }

        return false;
    }

    private static bool IsMeasureCreation(
        BaseObjectCreationExpressionSyntax creation,
        LiteralExpressionSyntax literal
    )
    {
        var type = creation switch
        {
            ObjectCreationExpressionSyntax explicitType => TypeName(explicitType.Type),
            ImplicitObjectCreationExpressionSyntax => ImplicitType(creation),
            _ => string.Empty,
        };
        if (!MeasureTypes.Contains(type))
        {
            // A literal in an object initializer is judged by the property it sets.
            return creation.Initializer is { } initializer
                && initializer.Span.Contains(literal.Span)
                && literal.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault()
                    is { } set
                && MeasureNames.Contains(TargetName(set.Left));
        }

        // A star weight of a grid (new GridLength(1, GridUnitType.Star)) is no measure.
        return !(
            string.Equals(type, "GridLength", StringComparison.Ordinal)
            && creation.ArgumentList?.Arguments.Count == 2
            && creation
                .ArgumentList.Arguments[1]
                .ToString()
                .EndsWith("Star", StringComparison.Ordinal)
        );
    }

    private static string ImplicitType(SyntaxNode creation) =>
        creation.Parent switch
        {
            EqualsValueClauseSyntax
            {
                Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration }
            } => TypeName(declaration.Type),
            AssignmentExpressionSyntax assignment => TargetName(assignment.Left) switch
            {
                "Margin" or "Padding" or "BorderThickness" => "Thickness",
                "CornerRadius" => "CornerRadius",
                _ => string.Empty,
            },
            _ => string.Empty,
        };

    private static string TypeName(TypeSyntax type) =>
        type switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            SimpleNameSyntax simple => simple.Identifier.Text,
            _ => type.ToString(),
        };

    private static string TargetName(ExpressionSyntax target) =>
        target switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => string.Empty,
        };

    private static string MethodName(ExpressionSyntax expression) =>
        expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => string.Empty,
        };
}
