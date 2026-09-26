using System;
using System.Collections.Generic;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Clicalo.Analyzers.Presentation;

/// <summary>
/// Finds the literal text that a value carries: string literals with letters, the literal parts of interpolated
/// strings, both sides of a concatenation, the format and arguments of <c>string.Format</c>/<c>Concat</c>/<c>Join</c>
/// and every branch of <c>?:</c>, <c>??</c> and <c>switch</c>. Other calls are not followed, so <c>localizer["key"]</c> is
/// fine: the literal there is a key, not a visible text.
/// </summary>
internal static class LiteralTextFlow
{
    private const int MaxDepth = 32;

    public static void Collect(IOperation value, List<(IOperation Operation, string Text)> found) =>
        Collect(value, found, 0);

    private static void Collect(
        IOperation? value,
        List<(IOperation Operation, string Text)> found,
        int depth
    )
    {
        if (value is null || depth > MaxDepth)
        {
            return;
        }

        var operation = value.WithoutConversions();
        switch (operation)
        {
            case ILiteralOperation
            {
                ConstantValue: { HasValue: true, Value: string text }
            } literal:
                // Hexadecimal colors are reported as colors, not as text.
                if (VisibleText.HasLetter(text) && !ColorLiterals.IsColorLiteral(text.Trim()))
                {
                    found.Add((literal, text));
                }

                break;
            case IInterpolatedStringOperation interpolated:
                foreach (var part in interpolated.Parts)
                {
                    switch (part)
                    {
                        case IInterpolatedStringTextOperation text:
                            Collect(text.Text, found, depth + 1);
                            break;
                        case IInterpolationOperation hole:
                            Collect(hole.Expression, found, depth + 1);
                            break;
                        case IInterpolatedStringAppendOperation
                        {
                            AppendCall: IInvocationOperation append
                        }:
                            foreach (var argument in append.Arguments)
                            {
                                Collect(argument.Value, found, depth + 1);
                            }

                            break;
                    }
                }

                break;
            case IInterpolatedStringHandlerCreationOperation handler:
                Collect(handler.Content, found, depth + 1);
                break;
            case IInterpolatedStringAdditionOperation addition:
                Collect(addition.Left, found, depth + 1);
                Collect(addition.Right, found, depth + 1);
                break;
            case IBinaryOperation { Type.SpecialType: SpecialType.System_String } concatenation:
                Collect(concatenation.LeftOperand, found, depth + 1);
                Collect(concatenation.RightOperand, found, depth + 1);
                break;
            case IConditionalOperation conditional:
                Collect(conditional.WhenTrue, found, depth + 1);
                Collect(conditional.WhenFalse, found, depth + 1);
                break;
            case ICoalesceOperation coalesce:
                Collect(coalesce.Value, found, depth + 1);
                Collect(coalesce.WhenNull, found, depth + 1);
                break;
            case ISwitchExpressionOperation switchExpression:
                foreach (var arm in switchExpression.Arms)
                {
                    Collect(arm.Value, found, depth + 1);
                }

                break;
            case IInvocationOperation invocation
                when StringComposition.IsComposition(invocation.TargetMethod):
                foreach (var argument in invocation.Arguments)
                {
                    if (
                        string.Equals(
                            argument.Parameter?.Name,
                            StringComposition.FormatParameter,
                            StringComparison.Ordinal
                        )
                    )
                    {
                        CollectFormat(argument.Value, found, depth + 1);
                    }
                    else
                    {
                        Collect(argument.Value, found, depth + 1);
                    }
                }

                break;
            case IArrayCreationOperation { Initializer: { } initializer }:
                // The implicit `params object[]` of string.Format and string.Concat.
                foreach (var element in initializer.ElementValues)
                {
                    Collect(element, found, depth + 1);
                }

                break;
        }
    }

    /// <summary>A composite format string is visible text when what remains around its format items has a letter.</summary>
    private static void CollectFormat(
        IOperation value,
        List<(IOperation Operation, string Text)> found,
        int depth
    )
    {
        var operation = value.WithoutConversions();
        if (
            operation is ILiteralOperation
            {
                ConstantValue: { HasValue: true, Value: string format }
            }
        )
        {
            if (VisibleText.HasLetter(StringComposition.VisiblePartOfFormat(format)))
            {
                found.Add((operation, format));
            }

            return;
        }

        Collect(operation, found, depth);
    }
}
