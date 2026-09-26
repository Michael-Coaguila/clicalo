using System;
using System.Collections.Concurrent;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Clicalo.Analyzers.Presentation;

/// <summary>
/// The C# shapes through which visible text or a color reaches the screen, bound to one compilation.
/// Text targets: members, parameters and attached properties whose name ends with a <see cref="VisibleText.TextWords"/>
/// word. Parameters count only on Clícalo and WPF APIs (<c>Clicalo.*</c>, <c>System.Windows.*</c>), and never on
/// exception constructors or <c>[LoggerMessage]</c> methods, whose text is for developers.
/// </summary>
internal sealed class PresentationApis
{
    private const string AttachedSetterPrefix = "Set";
    private const string DependencyPropertySuffix = "Property";
    private const string ClicaloNamespace = "Clicalo";
    private const string WpfNamespace = "System.Windows";

    private readonly INamedTypeSymbol? _colors;
    private readonly INamedTypeSymbol? _brushes;
    private readonly INamedTypeSymbol? _color;
    private readonly INamedTypeSymbol? _automationProperties;
    private readonly INamedTypeSymbol? _exception;
    private readonly INamedTypeSymbol? _loggerMessageAttribute;
    private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _wpfTypes = new(
        SymbolEqualityComparer.Default
    );
    private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _uiApiTypes = new(
        SymbolEqualityComparer.Default
    );

    public PresentationApis(Compilation compilation)
    {
        _colors = compilation.GetTypeByMetadataName(KnownTypeNames.WpfColors);
        _brushes = compilation.GetTypeByMetadataName(KnownTypeNames.WpfBrushes);
        _color = compilation.GetTypeByMetadataName(KnownTypeNames.WpfColor);
        _automationProperties = compilation.GetTypeByMetadataName(
            KnownTypeNames.AutomationProperties
        );
        _exception = compilation.GetTypeByMetadataName(KnownTypeNames.Exception);
        _loggerMessageAttribute = compilation.GetTypeByMetadataName(
            KnownTypeNames.LoggerMessageAttribute
        );
    }

    /// <summary><c>Colors.Red</c> or <c>Brushes.Red</c>: any named color except <c>Transparent</c>.</summary>
    public bool IsNamedColor(IPropertySymbol property) =>
        property.IsStatic
        && (Is(property.ContainingType, _colors) || Is(property.ContainingType, _brushes))
        && !ColorLiterals.IsTransparent(property.Name);

    /// <summary><c>Color.FromRgb(0x12, 0x34, 0x56)</c> and the other factories, when every channel is a constant.</summary>
    public bool IsConstantColor(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (
            !method.IsStatic
            || !Is(method.ContainingType, _color)
            || !method.Name.StartsWith("From", StringComparison.Ordinal)
            || invocation.Arguments.IsEmpty
        )
        {
            return false;
        }

        foreach (var argument in invocation.Arguments)
        {
            if (argument.IsWritten() && !argument.Value.ConstantValue.HasValue)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The visible property an argument sets, or null. Recognizes text parameters of our APIs and WPF,
    /// <c>AutomationProperties.SetName(element, "…")</c>-style attached setters and
    /// <c>SetValue(TextBlock.TextProperty, "…")</c>.
    /// </summary>
    public string? TextTargetOf(IArgumentOperation argument)
    {
        var method = argument.TargetOf();
        var parameter = argument.Parameter;
        if (method is null || parameter is null)
        {
            return null;
        }

        // Cheap name checks first: this runs for every argument in Presentation and UI.Wpf.
        var type = method.ContainingType;
        if (parameter.Ordinal == 1)
        {
            if (
                method.IsStatic
                && method.Name.StartsWith(AttachedSetterPrefix, StringComparison.Ordinal)
            )
            {
                var property = method.Name.Substring(AttachedSetterPrefix.Length);
                if (IsTextProperty(type, property) && IsWpfType(type))
                {
                    return type.Name + "." + property;
                }
            }
            else if (!method.IsStatic && method.Name is "SetValue" or "SetCurrentValue")
            {
                return DependencyPropertyTarget(argument);
            }
        }

        return
            VisibleText.IsTextName(parameter.Name) && IsUiApiType(type) && !IsDeveloperText(method)
            ? parameter.Name
            : null;
    }

    private bool IsWpfType(INamedTypeSymbol type) =>
        _wpfTypes.GetOrAdd(type.OriginalDefinition, static t => t.IsDeclaredIn(WpfNamespace));

    private bool IsUiApiType(INamedTypeSymbol type) =>
        _uiApiTypes.GetOrAdd(
            type.OriginalDefinition,
            static t => t.IsDeclaredIn(ClicaloNamespace) || t.IsDeclaredIn(WpfNamespace)
        );

    private string? DependencyPropertyTarget(IArgumentOperation argument)
    {
        if (argument.Parent is not IInvocationOperation invocation)
        {
            return null;
        }

        foreach (var sibling in invocation.Arguments)
        {
            if (
                sibling.Parameter?.Ordinal == 0
                && sibling.Value.WithoutConversions()
                    is IFieldReferenceOperation { Field: var field }
                && field.Name.EndsWith(DependencyPropertySuffix, StringComparison.Ordinal)
            )
            {
                var property = field.Name.Substring(
                    0,
                    field.Name.Length - DependencyPropertySuffix.Length
                );
                return IsTextProperty(field.ContainingType, property)
                    ? field.ContainingType.Name + "." + property
                    : null;
            }
        }

        return null;
    }

    private bool IsTextProperty(INamedTypeSymbol owner, string property) =>
        Is(owner, _automationProperties)
            ? property is "Name" or "HelpText" or "ItemStatus"
            : VisibleText.IsTextName(property);

    private bool IsDeveloperText(IMethodSymbol method)
    {
        if (
            method.MethodKind == MethodKind.Constructor
            && _exception is not null
            && method.ContainingType.IsOrInheritsFrom(_exception)
        )
        {
            return true;
        }

        if (_loggerMessageAttribute is null)
        {
            return false;
        }

        foreach (var attribute in method.OriginalDefinition.GetAttributes())
        {
            if (
                SymbolEqualityComparer.Default.Equals(
                    attribute.AttributeClass,
                    _loggerMessageAttribute
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool Is(INamedTypeSymbol? type, INamedTypeSymbol? candidate) =>
        type is not null
        && candidate is not null
        && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, candidate);
}
