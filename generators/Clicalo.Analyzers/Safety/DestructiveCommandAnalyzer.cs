using System.Collections.Immutable;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Clicalo.Analyzers.Safety;

/// <summary>
/// CLC0010 (REG-04): destructive commands travel with a <c>ConfirmationToken</c>, and only <c>TwoStepConfirm</c>
/// issues tokens.
/// <list type="number">
/// <item>A call (method or constructor) that passes a value implementing <c>IDestructiveCommand</c> to a parameter that
/// accepts commands must also be given a <c>ConfirmationToken</c> argument. Calls into <c>TwoStepConfirm</c>, where a
/// command waits for its second tap, are exempt.</item>
/// <item>Outside <c>TwoStepConfirm</c>, nothing may produce a token: <c>new ConfirmationToken(…)</c> (or of a derived
/// type), a <c>: base(…)</c> call into its constructor, <c>with</c> on a token, <c>default</c> of a struct token, or
/// <c>default</c>/<c>null</c> passed where a token is expected.</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DestructiveCommandAnalyzer : DiagnosticAnalyzer
{
    private static readonly SymbolDisplayFormat ShortFormat =
        SymbolDisplayFormat.CSharpShortErrorMessageFormat;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            Descriptors.MissingConfirmationToken,
            Descriptors.ForgedConfirmationToken
        );

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var token = context.Compilation.GetTypeByMetadataName(KnownTypeNames.ConfirmationToken);
        if (token is null)
        {
            // Without the token type there is neither a correct overload to demand nor a token to forge.
            return;
        }

        var contracts = new Contracts(
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.DestructiveCommand),
            token,
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.TwoStepConfirm)
        );
        context.RegisterOperationAction(
            c => AnalyzeInvocation(c, contracts),
            OperationKind.Invocation
        );
        context.RegisterOperationAction(
            c => AnalyzeObjectCreation(c, contracts),
            OperationKind.ObjectCreation
        );
        context.RegisterOperationAction(
            c => AnalyzeForgery(c, contracts),
            OperationKind.With,
            OperationKind.DefaultValue
        );
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, Contracts contracts)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (
            method.MethodKind == MethodKind.Constructor
            && method.ContainingType.IsOrInheritsFrom(contracts.Token)
            && !contracts.IsIssuer(context.ContainingSymbol)
            && !IsChainingWithinSameType(method, context.ContainingSymbol)
        )
        {
            // `: base(...)` from a type that derives from ConfirmationToken.
            ReportForged(context, invocation.Syntax);
        }

        AnalyzeDispatch(context, contracts, method, invocation.Arguments);
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context, Contracts contracts)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (
            creation.Type.IsOrInheritsFrom(contracts.Token)
            && !contracts.IsIssuer(context.ContainingSymbol)
        )
        {
            ReportForged(context, creation.Syntax);
        }

        if (creation.Constructor is { } constructor)
        {
            AnalyzeDispatch(context, contracts, constructor, creation.Arguments);
        }
    }

    private static void AnalyzeForgery(OperationAnalysisContext context, Contracts contracts)
    {
        if (contracts.IsIssuer(context.ContainingSymbol))
        {
            return;
        }

        switch (context.Operation)
        {
            case IWithOperation with when with.Type.IsOrInheritsFrom(contracts.Token):
                ReportForged(context, with.Syntax);
                break;
            case IDefaultValueOperation { Type: { IsValueType: true } type } defaultValue
                when type.IsOrInheritsFrom(contracts.Token):
                // A default struct token is a token nobody confirmed. `ConfirmationToken? pending = null` (no token
                // yet) stays allowed: its type is Nullable<ConfirmationToken>.
                ReportForged(context, defaultValue.Syntax);
                break;
        }
    }

    private static void AnalyzeDispatch(
        OperationAnalysisContext context,
        Contracts contracts,
        IMethodSymbol method,
        ImmutableArray<IArgumentOperation> arguments
    )
    {
        // TwoStepConfirm holds commands until the second tap; a command's own members (record Equals, a composite
        // command built from others) do not dispatch anything.
        if (
            contracts.Destructive is null
            || contracts.IsIssuer(method)
            || method.ContainingType.IsOrImplements(contracts.Destructive)
        )
        {
            return;
        }

        IOperation? command = null;
        var hasToken = false;
        foreach (var argument in arguments)
        {
            if (!argument.IsWritten() || argument.Parameter is not { } parameter)
            {
                continue;
            }

            if (contracts.IsTokenType(parameter.Type))
            {
                hasToken = true;
                if (
                    !contracts.IsIssuer(context.ContainingSymbol)
                    && IsAbsentToken(argument.Value, contracts)
                )
                {
                    ReportForged(context, argument.Value.Syntax);
                }
            }
            else if (command is null)
            {
                command = contracts.DestructiveCommandIn(argument);
            }
        }

        if (command is not null && !hasToken)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.MissingConfirmationToken,
                    command.Syntax.GetLocation(),
                    command.WithoutConversions().Type?.ToDisplayString(ShortFormat) ?? "?",
                    method.MethodKind == MethodKind.Constructor
                        ? method.ContainingType.Name
                        : method.ContainingType.Name + "." + method.Name
                )
            );
        }
    }

    /// <summary><c>: this(...)</c> between constructors of the token type itself is not a new token.</summary>
    private static bool IsChainingWithinSameType(
        IMethodSymbol constructor,
        ISymbol containingSymbol
    ) =>
        containingSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor } caller
        && SymbolEqualityComparer.Default.Equals(caller.ContainingType, constructor.ContainingType);

    /// <summary>
    /// <c>null</c> or <c>default</c> handed over as the token. A <c>default</c> of the struct token itself is already
    /// reported where it is written (<see cref="AnalyzeForgery"/>), so it is not reported twice.
    /// </summary>
    private static bool IsAbsentToken(IOperation value, Contracts contracts)
    {
        var operation = value.WithoutConversions();
        if (operation is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            return true;
        }

        return operation is IDefaultValueOperation { Type: var type }
            && !(type is { IsValueType: true } && type.IsOrInheritsFrom(contracts.Token));
    }

    private static void ReportForged(OperationAnalysisContext context, SyntaxNode node)
    {
        // `: base(7)` is reported from the keyword, without the colon.
        if (node is ConstructorInitializerSyntax initializer)
        {
            var span = TextSpan.FromBounds(
                initializer.ThisOrBaseKeyword.SpanStart,
                initializer.Span.End
            );
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.ForgedConfirmationToken,
                    Location.Create(initializer.SyntaxTree, span),
                    initializer.ThisOrBaseKeyword.Text + initializer.ArgumentList
                )
            );
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.ForgedConfirmationToken,
                node.GetLocation(),
                node.ToString()
            )
        );
    }
}
