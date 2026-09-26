using System;
using System.Collections.Immutable;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Clicalo.Analyzers.Windowing;

/// <summary>
/// CLC0001 (REG-01): a <c>NonActivatingWindow</c> is shown only with <c>ShowPassive()</c>. Calling <c>Activate()</c>,
/// <c>Show()</c>, <c>ShowDialog()</c> or <c>Focus()</c> on it, taking one of those methods as a delegate, setting
/// <c>ShowActivated</c> to anything but <c>false</c> or <c>Visibility</c> to anything but Hidden or Collapsed is an error.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonActivatingWindowAnalyzer : DiagnosticAnalyzer
{
    private const string ShowActivatedProperty = "ShowActivated";
    private const string VisibilityProperty = "Visibility";

    // System.Windows.Visibility: Visible = 0, Hidden = 1, Collapsed = 2.
    private const long VisibilityHidden = 1;
    private const long VisibilityCollapsed = 2;

    private static readonly ImmutableHashSet<string> ActivatingMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Activate",
        "Show",
        "ShowDialog",
        "Focus"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.ActivatingCall, Descriptors.ActivatingProperty);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var window = context.Compilation.GetTypeByMetadataName(KnownTypeNames.NonActivatingWindow);
        if (window is null)
        {
            return;
        }

        context.RegisterOperationAction(
            c => AnalyzeInvocation(c, window),
            OperationKind.Invocation
        );
        context.RegisterOperationAction(
            c => AnalyzeMethodReference(c, window),
            OperationKind.MethodReference
        );
        context.RegisterOperationAction(
            c => AnalyzeAssignment(c, window),
            OperationKind.SimpleAssignment
        );
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol window)
    {
        var invocation = (IInvocationOperation)context.Operation;
        ReportActivatingMethod(context, window, invocation.TargetMethod, invocation.Instance);
    }

    private static void AnalyzeMethodReference(
        OperationAnalysisContext context,
        INamedTypeSymbol window
    )
    {
        // `Action show = panel.Show;` defers the same activating call.
        var reference = (IMethodReferenceOperation)context.Operation;
        ReportActivatingMethod(context, window, reference.Method, reference.Instance);
    }

    private static void ReportActivatingMethod(
        OperationAnalysisContext context,
        INamedTypeSymbol window,
        IMethodSymbol method,
        IOperation? instance
    )
    {
        if (
            method.IsStatic
            || !method.Parameters.IsEmpty
            || !ActivatingMethods.Contains(method.Name)
        )
        {
            return;
        }

        var receiver = ReceiverType(instance, context.ContainingSymbol);
        if (receiver is not null && receiver.IsOrInheritsFrom(window))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.ActivatingCall,
                    context.Operation.Syntax.GetLocation(),
                    method.Name,
                    receiver.Name
                )
            );
        }
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context, INamedTypeSymbol window)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation target)
        {
            return;
        }

        var name = target.Property.Name;
        var activates = name switch
        {
            ShowActivatedProperty => !IsConstantFalse(assignment.Value),
            VisibilityProperty => !IsHidingVisibility(assignment.Value),
            _ => false,
        };
        if (!activates)
        {
            return;
        }

        var receiver = ReceiverType(target.Instance, context.ContainingSymbol);
        if (receiver is not null && receiver.IsOrInheritsFrom(window))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.ActivatingProperty,
                    assignment.Syntax.GetLocation(),
                    name,
                    receiver.Name
                )
            );
        }
    }

    /// <summary>
    /// The static type the member is invoked on. <c>base.Show()</c> and implicit calls resolve to the type being
    /// executed, and casts are looked through so <c>((Window)panel).Show()</c> is still judged on <c>panel</c>.
    /// </summary>
    private static ITypeSymbol? ReceiverType(IOperation? instance, ISymbol containingSymbol)
    {
        if (instance is null)
        {
            return null;
        }

        if (
            instance is IInstanceReferenceOperation
            {
                ReferenceKind: InstanceReferenceKind.ContainingTypeInstance
            }
        )
        {
            return containingSymbol as INamedTypeSymbol
                ?? containingSymbol.ContainingType
                ?? instance.Type;
        }

        return instance.WithoutConversions().Type;
    }

    private static bool IsConstantFalse(IOperation value) =>
        value.ConstantValue is { HasValue: true, Value: false };

    private static bool IsHidingVisibility(IOperation value)
    {
        var constant = value.WithoutConversions().ConstantValue;
        return constant.HasValue
            && ConstantValues.TryGetInteger(constant.Value, out var number)
            && number is VisibilityHidden or VisibilityCollapsed;
    }
}
