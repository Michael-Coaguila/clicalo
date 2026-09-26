using System.Collections.Immutable;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Clicalo.Analyzers.Timing;

/// <summary>
/// CLC0004 (NFR-020): in <c>Clicalo.Application*</c> and <c>Clicalo.Presentation*</c> every duration comes from
/// <c>Clicalo.Domain.Timings</c>. A numeric constant used as a duration (see <see cref="DurationApis"/>) is an
/// error whether it is passed to a call, assigned to a member or used to initialize a member or a local.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DurationLiteralAnalyzer : DiagnosticAnalyzer
{
    private const string ApplicationNamespace = "Clicalo.Application";
    private const string PresentationNamespace = "Clicalo.Presentation";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.DurationLiteral);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var scope = new NamespaceScope(ApplicationNamespace, PresentationNamespace);
        var apis = new DurationApis(context.Compilation);
        context.RegisterOperationBlockStartAction(block =>
        {
            if (!scope.Contains(block.OwningSymbol))
            {
                return;
            }

            block.RegisterOperationAction(
                c => AnalyzeCall(c, apis),
                OperationKind.Invocation,
                OperationKind.ObjectCreation
            );
            block.RegisterOperationAction(
                c => AnalyzeAssignment(c, apis),
                OperationKind.SimpleAssignment,
                OperationKind.CompoundAssignment
            );
            block.RegisterOperationAction(
                c => AnalyzeInitializer(c, apis),
                OperationKind.PropertyInitializer,
                OperationKind.FieldInitializer,
                OperationKind.VariableDeclarator
            );
        });
    }

    private static void AnalyzeCall(OperationAnalysisContext context, DurationApis apis)
    {
        var (method, arguments) = context.Operation switch
        {
            IInvocationOperation invocation => (invocation.TargetMethod, invocation.Arguments),
            IObjectCreationOperation creation => (creation.Constructor, creation.Arguments),
            _ => (null, ImmutableArray<IArgumentOperation>.Empty),
        };
        if (method is null)
        {
            return;
        }

        IOperation? offending = null;
        var count = 0;
        foreach (var argument in arguments)
        {
            if (
                argument.IsWritten()
                && argument.ArgumentKind != ArgumentKind.ParamArray
                && apis.IsDurationArgument(method, argument.Parameter)
                && apis.IsLiteralDuration(argument.Value)
            )
            {
                offending ??= argument.Value;
                count++;
            }
        }

        if (offending is not null)
        {
            // One diagnostic per call: `new TimeSpan(0, 0, 5)` is one mistake, not three.
            Report(
                context,
                count == 1 ? offending.Syntax : context.Operation.Syntax,
                DisplayName(method)
            );
        }
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context, DurationApis apis)
    {
        var assignment = (IAssignmentOperation)context.Operation;
        var member = assignment.Target switch
        {
            IPropertyReferenceOperation property => (ISymbol)property.Property,
            IFieldReferenceOperation field => field.Field,
            _ => null,
        };
        if (
            member is not null
            && IsDurationMember(member)
            && apis.IsLiteralDuration(assignment.Value)
        )
        {
            Report(context, assignment.Value.Syntax, member.Name);
        }
    }

    private static void AnalyzeInitializer(OperationAnalysisContext context, DurationApis apis)
    {
        switch (context.Operation)
        {
            case IPropertyInitializerOperation property:
                ReportInitializer(context, apis, property.InitializedProperties, property.Value);
                break;
            case IFieldInitializerOperation field:
                ReportInitializer(context, apis, field.InitializedFields, field.Value);
                break;
            case IVariableDeclaratorOperation { Initializer: { } initializer } declarator
                when DurationApis.IsDurationMember(declarator.Symbol.Name, declarator.Symbol.Type)
                    && apis.IsLiteralDuration(initializer.Value):
                Report(context, initializer.Value.Syntax, declarator.Symbol.Name);
                break;
        }
    }

    private static void ReportInitializer<TSymbol>(
        OperationAnalysisContext context,
        DurationApis apis,
        ImmutableArray<TSymbol> members,
        IOperation value
    )
        where TSymbol : ISymbol
    {
        foreach (var member in members)
        {
            if (IsDurationMember(member) && apis.IsLiteralDuration(value))
            {
                Report(context, value.Syntax, member.Name);
                return;
            }
        }
    }

    private static bool IsDurationMember(ISymbol member) =>
        member switch
        {
            IPropertySymbol property => DurationApis.IsDurationMember(property.Name, property.Type),
            IFieldSymbol field => DurationApis.IsDurationMember(field.Name, field.Type),
            _ => false,
        };

    private static string DisplayName(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Constructor
            ? method.ContainingType.Name
            : method.ContainingType.Name + "." + method.Name;

    private static void Report(OperationAnalysisContext context, SyntaxNode node, string target) =>
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.DurationLiteral,
                node.GetLocation(),
                node.ToString(),
                target
            )
        );
}
