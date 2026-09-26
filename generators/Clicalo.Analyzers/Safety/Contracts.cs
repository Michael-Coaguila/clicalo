using System.Collections.Concurrent;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Clicalo.Analyzers.Safety;

/// <summary>
/// The REG-04 contracts of one compilation: the destructive marker, the token and its only issuer.
/// Create one per compilation: it caches per-type answers.
/// </summary>
internal sealed class Contracts
{
    private readonly ConcurrentDictionary<ITypeSymbol, bool> _acceptsCommands = new(
        SymbolEqualityComparer.Default
    );

    public Contracts(
        INamedTypeSymbol? destructive,
        INamedTypeSymbol token,
        INamedTypeSymbol? issuer
    )
    {
        Destructive = destructive;
        Token = token;
        Issuer = issuer;
    }

    /// <summary><c>Clicalo.Domain.Commands.IDestructiveCommand</c>, when the compilation references it.</summary>
    public INamedTypeSymbol? Destructive { get; }

    /// <summary><c>Clicalo.Application.Confirmation.ConfirmationToken</c>.</summary>
    public INamedTypeSymbol Token { get; }

    /// <summary><c>Clicalo.Application.Confirmation.TwoStepConfirm</c>, the only type allowed to issue tokens.</summary>
    public INamedTypeSymbol? Issuer { get; }

    /// <summary>True when <paramref name="symbol"/> belongs to <c>TwoStepConfirm</c> (members and nested types included).</summary>
    public bool IsIssuer(ISymbol? symbol) => Issuer is not null && symbol.IsWithin(Issuer);

    /// <summary>True for <c>ConfirmationToken</c>, a type derived from it, or its <c>Nullable&lt;T&gt;</c>.</summary>
    public bool IsTokenType(ITypeSymbol type)
    {
        if (
            type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
            } nullable
        )
        {
            type = nullable.TypeArguments[0];
        }

        return type.IsOrInheritsFrom(Token);
    }

    /// <summary>
    /// The destructive command carried by <paramref name="argument"/> into a parameter that accepts commands, or null.
    /// A parameter accepts commands when its declared type (before generic substitution) is <c>IDestructiveCommand</c>,
    /// a command type, or one of the interfaces <c>IDestructiveCommand</c> extends (such as <c>IDocumentCommand</c>);
    /// <c>object</c> and unconstrained generics (<c>ThrowIfNull</c>, collections) do not.
    /// </summary>
    public IOperation? DestructiveCommandIn(IArgumentOperation argument)
    {
        if (Destructive is null || argument.Parameter is not { } parameter)
        {
            return null;
        }

        var declared = parameter.OriginalDefinition.Type;
        if (argument.ArgumentKind == ArgumentKind.ParamArray)
        {
            if (
                declared is IArrayTypeSymbol array
                && AcceptsCommands(array.ElementType)
                && argument.Value is IArrayCreationOperation { Initializer: { } initializer }
            )
            {
                foreach (var element in initializer.ElementValues)
                {
                    if (IsDestructive(element))
                    {
                        return element;
                    }
                }
            }

            return null;
        }

        return AcceptsCommands(declared) && IsDestructive(argument.Value) ? argument.Value : null;
    }

    private bool IsDestructive(IOperation value) =>
        value.WithoutConversions().Type.IsOrImplements(Destructive!);

    // Runs for every argument of every call in src/, so the answer is cached per declared parameter type.
    private bool AcceptsCommands(ITypeSymbol declared) =>
        _acceptsCommands.GetOrAdd(declared, ComputeAcceptsCommands);

    private bool ComputeAcceptsCommands(ITypeSymbol declared)
    {
        switch (declared)
        {
            case ITypeParameterSymbol parameter:
                foreach (var constraint in parameter.ConstraintTypes)
                {
                    if (AcceptsCommands(constraint))
                    {
                        return true;
                    }
                }

                return false;
            case INamedTypeSymbol named when named.SpecialType is SpecialType.None:
                if (named.IsOrImplements(Destructive!))
                {
                    return true;
                }

                foreach (var extended in Destructive!.AllInterfaces)
                {
                    if (SymbolEqualityComparer.Default.Equals(extended, named.OriginalDefinition))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }
}
