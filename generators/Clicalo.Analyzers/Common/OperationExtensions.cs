using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Clicalo.Analyzers.Common;

/// <summary>Small helpers over the IOperation tree shared by the rules.</summary>
internal static class OperationExtensions
{
    /// <summary>Removes implicit and explicit conversions, so <c>(object)secret</c> is judged as <c>secret</c>.</summary>
    public static IOperation WithoutConversions(this IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    /// <summary>True when the caller wrote the argument (it was not filled in from an optional parameter).</summary>
    public static bool IsWritten(this IArgumentOperation argument) =>
        argument.ArgumentKind != ArgumentKind.DefaultValue;

    /// <summary>The method, constructor or indexer getter an argument is passed to.</summary>
    public static IMethodSymbol? TargetOf(this IArgumentOperation argument) =>
        argument.Parent switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation creation => creation.Constructor,
            IPropertyReferenceOperation reference => reference.Property.SetMethod
                ?? reference.Property.GetMethod,
            _ => null,
        };
}
