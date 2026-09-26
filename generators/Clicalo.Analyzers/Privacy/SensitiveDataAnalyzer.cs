using System.Collections.Generic;
using System.Collections.Immutable;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Clicalo.Analyzers.Privacy;

/// <summary>
/// CLC0003 (LOG-001, D14): a value whose type is sensitive (<see cref="SensitiveTypes"/>) never reaches a log,
/// trace or exception sink (<see cref="SensitiveSinks"/>), neither directly nor interpolated or concatenated.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SensitiveDataAnalyzer : DiagnosticAnalyzer
{
    private static readonly SymbolDisplayFormat ShortFormat =
        SymbolDisplayFormat.CSharpShortErrorMessageFormat;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.SensitiveData);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var types = SensitiveTypes.Create(context.Compilation);
        if (types is null)
        {
            return;
        }

        var sinks = new SensitiveSinks(context.Compilation);
        context.RegisterOperationAction(
            c => AnalyzeInvocation(c, types, sinks),
            OperationKind.Invocation
        );
        context.RegisterOperationAction(
            c => AnalyzeObjectCreation(c, types, sinks),
            OperationKind.ObjectCreation
        );
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        SensitiveTypes types,
        SensitiveSinks sinks
    )
    {
        // Constructor initializers (`: base(message)`) are invocations too.
        var invocation = (IInvocationOperation)context.Operation;
        if (sinks.IsSink(invocation.TargetMethod))
        {
            ReportArguments(
                context,
                types,
                invocation.TargetMethod,
                invocation.Arguments,
                initializer: null
            );
        }
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        SensitiveTypes types,
        SensitiveSinks sinks
    )
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Constructor is { } constructor && sinks.IsSink(constructor))
        {
            ReportArguments(context, types, constructor, creation.Arguments, creation.Initializer);
        }
    }

    private static void ReportArguments(
        OperationAnalysisContext context,
        SensitiveTypes types,
        IMethodSymbol sink,
        ImmutableArray<IArgumentOperation> arguments,
        IObjectOrCollectionInitializerOperation? initializer
    )
    {
        var found = new List<IOperation>();
        foreach (var argument in arguments)
        {
            SensitiveFlow.Collect(argument.Value, types, found);
        }

        // `new AppException(...) { Detail = secret }` puts the value inside the exception as well.
        if (initializer is not null)
        {
            foreach (var member in initializer.Initializers)
            {
                if (member is ISimpleAssignmentOperation assignment)
                {
                    SensitiveFlow.Collect(assignment.Value, types, found);
                }
            }
        }

        if (found.Count == 0)
        {
            return;
        }

        var sinkName =
            sink.MethodKind == MethodKind.Constructor
                ? sink.ContainingType.ToDisplayString(ShortFormat)
                : sink.ContainingType.Name + "." + sink.Name;
        foreach (var value in found)
        {
            var type = types.IsSensitive(value.Type) ? value.Type : UnwrappedFrom(value);
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.SensitiveData,
                    value.Syntax.GetLocation(),
                    value.Syntax.ToString(),
                    type?.ToDisplayString(ShortFormat) ?? "?",
                    sinkName
                )
            );
        }
    }

    /// <summary>For <c>title.Value</c>, the <c>Sensitive&lt;T&gt;</c> the value was unwrapped from.</summary>
    private static ITypeSymbol? UnwrappedFrom(IOperation value) =>
        value switch
        {
            IPropertyReferenceOperation { Instance: { } instance } => instance
                .WithoutConversions()
                .Type,
            IFieldReferenceOperation { Instance: { } instance } => instance
                .WithoutConversions()
                .Type,
            IInvocationOperation { Instance: { } instance } => instance.WithoutConversions().Type,
            _ => value.Type,
        };
}
