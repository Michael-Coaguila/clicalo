using System.Collections.Generic;
using System.Collections.Immutable;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Clicalo.Analyzers.Presentation;

/// <summary>
/// CLC0006 (IDI-002, TEM-002): in <c>Clicalo.Presentation*</c> and <c>Clicalo.UI.Wpf*</c>, visible text comes from
/// data/i18n and colors from the theme tokens. In C#: literal text that reaches a visible member, parameter or attached
/// property (<see cref="PresentationApis"/>), hexadecimal color strings, <c>Colors.*</c>/<c>Brushes.*</c> and constant
/// <c>Color.From*</c>. In XAML passed as <c>AdditionalFiles</c>: see <see cref="XamlLiteralScanner"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PresentationLiteralAnalyzer : DiagnosticAnalyzer
{
    private const string PresentationNamespace = "Clicalo.Presentation";
    private const string UiWpfNamespace = "Clicalo.UI.Wpf";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.LiteralText, Descriptors.LiteralColor);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var scope = new NamespaceScope(PresentationNamespace, UiWpfNamespace);
        var apis = new PresentationApis(context.Compilation);
        context.RegisterOperationBlockStartAction(block =>
        {
            if (!scope.Contains(block.OwningSymbol))
            {
                return;
            }

            block.RegisterOperationAction(AnalyzeLiteral, OperationKind.Literal);
            block.RegisterOperationAction(
                c => AnalyzePropertyReference(c, apis),
                OperationKind.PropertyReference
            );
            block.RegisterOperationAction(
                c => AnalyzeInvocation(c, apis),
                OperationKind.Invocation
            );
            block.RegisterOperationAction(
                AnalyzeAssignment,
                OperationKind.SimpleAssignment,
                OperationKind.CompoundAssignment
            );
            block.RegisterOperationAction(
                AnalyzeInitializer,
                OperationKind.PropertyInitializer,
                OperationKind.FieldInitializer
            );
            block.RegisterOperationAction(AnalyzeReturn, OperationKind.Return);
            block.RegisterOperationAction(c => AnalyzeArgument(c, apis), OperationKind.Argument);
        });
        context.RegisterAdditionalFileAction(c => XamlLiteralScanner.Analyze(c, scope));
    }

    private static void AnalyzeLiteral(OperationAnalysisContext context)
    {
        if (
            context.Operation.ConstantValue is { HasValue: true, Value: string text }
            && ColorLiterals.IsColorLiteral(text.Trim())
        )
        {
            ReportColor(context, context.Operation.Syntax, text.Trim());
        }
    }

    private static void AnalyzePropertyReference(
        OperationAnalysisContext context,
        PresentationApis apis
    )
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        if (apis.IsNamedColor(reference.Property))
        {
            ReportColor(
                context,
                reference.Syntax,
                reference.Property.ContainingType.Name + "." + reference.Property.Name
            );
        }
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, PresentationApis apis)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (apis.IsConstantColor(invocation))
        {
            ReportColor(context, invocation.Syntax, invocation.Syntax.ToString());
        }
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        var assignment = (IAssignmentOperation)context.Operation;
        var name = assignment.Target switch
        {
            IPropertyReferenceOperation property => property.Property.Name,
            IFieldReferenceOperation field => field.Field.Name,
            _ => null,
        };
        if (name is not null && VisibleText.IsTextName(name))
        {
            ReportText(context, assignment.Value, name);
        }
    }

    private static void AnalyzeInitializer(OperationAnalysisContext context)
    {
        switch (context.Operation)
        {
            case IPropertyInitializerOperation initializer:
                foreach (var property in initializer.InitializedProperties)
                {
                    if (VisibleText.IsTextName(property.Name))
                    {
                        ReportText(context, initializer.Value, property.Name);
                        return;
                    }
                }

                break;
            case IFieldInitializerOperation initializer:
                // Also covers [ObservableProperty] fields, whose generated property has the same name.
                foreach (var field in initializer.InitializedFields)
                {
                    if (VisibleText.IsTextName(field.Name))
                    {
                        ReportText(context, initializer.Value, field.Name);
                        return;
                    }
                }

                break;
        }
    }

    private static void AnalyzeReturn(OperationAnalysisContext context)
    {
        // `public string Title => "Hola";` and `get { return "Hola"; }`, but not a return inside a lambda.
        var ret = (IReturnOperation)context.Operation;
        if (
            ret.ReturnedValue is { } value
            && context.ContainingSymbol
                is IMethodSymbol
                {
                    MethodKind: MethodKind.PropertyGet,
                    AssociatedSymbol: IPropertySymbol property,
                }
            && VisibleText.IsTextName(property.Name)
            && !IsInsideNestedFunction(ret)
        )
        {
            ReportText(context, value, property.Name);
        }
    }

    private static void AnalyzeArgument(OperationAnalysisContext context, PresentationApis apis)
    {
        var argument = (IArgumentOperation)context.Operation;
        if (argument.IsWritten() && apis.TextTargetOf(argument) is { } target)
        {
            ReportText(context, argument.Value, target);
        }
    }

    private static bool IsInsideNestedFunction(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportText(
        OperationAnalysisContext context,
        IOperation value,
        string target
    )
    {
        var found = new List<(IOperation Operation, string Text)>();
        LiteralTextFlow.Collect(value, found);
        foreach (var (operation, text) in found)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.LiteralText,
                    operation.Syntax.GetLocation(),
                    VisibleText.ForMessage(text),
                    target
                )
            );
        }
    }

    private static void ReportColor(
        OperationAnalysisContext context,
        SyntaxNode node,
        string color
    ) =>
        context.ReportDiagnostic(
            Diagnostic.Create(Descriptors.LiteralColor, node.GetLocation(), color)
        );
}
