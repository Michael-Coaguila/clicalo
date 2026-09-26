using System;
using System.Collections.Generic;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Clicalo.Analyzers.Privacy;

/// <summary>
/// Finds the sensitive values that an argument carries into a sink: the value itself, the holes of an interpolated
/// string, the operands of a string concatenation or of <c>string.Format</c>/<c>Concat</c>/<c>Join</c>, the elements
/// of a <c>params</c> array, a collection or a tuple, both branches of <c>?:</c>, <c>??</c> and <c>switch</c>, the
/// members of an anonymous object, and any member of <c>Sensitive&lt;T&gt;</c> that unwraps the <c>T</c> it protects.
/// Opaque calls are not followed: whatever a method returns is judged by its own type.
/// </summary>
internal static class SensitiveFlow
{
    private const int MaxDepth = 32;

    public static void Collect(IOperation value, SensitiveTypes types, List<IOperation> found) =>
        Collect(value, types, found, 0);

    private static void Collect(
        IOperation? value,
        SensitiveTypes types,
        List<IOperation> found,
        int depth
    )
    {
        if (value is null || depth > MaxDepth)
        {
            return;
        }

        var operation = value.WithoutConversions();
        if (types.IsSensitive(operation.Type) || IsUnwrap(operation, types))
        {
            found.Add(operation);
            return;
        }

        foreach (var child in Carried(operation))
        {
            Collect(child, types, found, depth + 1);
        }
    }

    /// <summary>The operations whose value ends up, unchanged or formatted, inside <paramref name="operation"/>.</summary>
    private static IEnumerable<IOperation?> Carried(IOperation operation)
    {
        switch (operation)
        {
            case IInterpolatedStringOperation interpolated:
                foreach (var part in interpolated.Parts)
                {
                    switch (part)
                    {
                        case IInterpolationOperation hole:
                            yield return hole.Expression;
                            break;
                        case IInterpolatedStringAppendOperation
                        {
                            AppendCall: IInvocationOperation append
                        }:
                            foreach (var argument in append.Arguments)
                            {
                                yield return argument.Value;
                            }

                            break;
                    }
                }

                break;
            case IInterpolatedStringHandlerCreationOperation handler:
                yield return handler.Content;
                break;
            case IInterpolatedStringAdditionOperation addition:
                yield return addition.Left;
                yield return addition.Right;
                break;
            case IBinaryOperation { Type.SpecialType: SpecialType.System_String } concatenation:
                yield return concatenation.LeftOperand;
                yield return concatenation.RightOperand;
                break;
            case IInvocationOperation invocation when IsStringComposition(invocation.TargetMethod):
                foreach (var argument in invocation.Arguments)
                {
                    yield return argument.Value;
                }

                break;
            case IArrayCreationOperation { Initializer: { } initializer }:
                yield return initializer;
                break;
            case IArrayInitializerOperation initializer:
                foreach (var element in initializer.ElementValues)
                {
                    yield return element;
                }

                break;
            case ICollectionExpressionOperation collection:
                foreach (var element in collection.Elements)
                {
                    yield return element is ISpreadOperation spread ? spread.Operand : element;
                }

                break;
            case ITupleOperation tuple:
                foreach (var element in tuple.Elements)
                {
                    yield return element;
                }

                break;
            case IAnonymousObjectCreationOperation anonymous:
                foreach (var initializer in anonymous.Initializers)
                {
                    yield return initializer is ISimpleAssignmentOperation assignment
                        ? assignment.Value
                        : initializer;
                }

                break;
            case IConditionalOperation conditional:
                yield return conditional.WhenTrue;
                yield return conditional.WhenFalse;
                break;
            case ICoalesceOperation coalesce:
                yield return coalesce.Value;
                yield return coalesce.WhenNull;
                break;
            case ISwitchExpressionOperation switchExpression:
                foreach (var arm in switchExpression.Arms)
                {
                    yield return arm.Value;
                }

                break;
        }
    }

    private static bool IsStringComposition(IMethodSymbol method) =>
        method.ContainingType?.SpecialType == SpecialType.System_String
        && method.IsStatic
        && method.Name is "Format" or "Concat" or "Join";

    /// <summary>
    /// <c>title.Value</c>, <c>title.Reveal()</c> and similar: a member declared by <c>Sensitive&lt;T&gt;</c> (other
    /// than the redacting <c>ToString</c> and the object overrides) that hands out the protected <c>T</c>.
    /// </summary>
    private static bool IsUnwrap(IOperation operation, SensitiveTypes types)
    {
        var wrapper = types.SensitiveOfT;
        if (wrapper is null)
        {
            return false;
        }

        return operation switch
        {
            IPropertyReferenceOperation p => Unwraps(
                wrapper,
                p.Property,
                p.Instance,
                p.Property.Type
            ),
            IFieldReferenceOperation f => Unwraps(wrapper, f.Field, f.Instance, f.Field.Type),
            IInvocationOperation i => Unwraps(
                wrapper,
                i.TargetMethod,
                i.Instance,
                i.TargetMethod.ReturnType
            ),
            _ => false,
        };
    }

    private static bool Unwraps(
        INamedTypeSymbol wrapper,
        ISymbol member,
        IOperation? instance,
        ITypeSymbol resultType
    )
    {
        if (
            instance is null
            || member.IsOverride
            || string.Equals(member.Name, nameof(object.ToString), StringComparison.Ordinal)
        )
        {
            return false;
        }

        return instance.WithoutConversions().Type is INamedTypeSymbol receiver
            && SymbolEqualityComparer.Default.Equals(receiver.OriginalDefinition, wrapper)
            && SymbolEqualityComparer.Default.Equals(
                member.ContainingType?.OriginalDefinition,
                wrapper
            )
            && receiver.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(resultType, receiver.TypeArguments[0]);
    }
}
