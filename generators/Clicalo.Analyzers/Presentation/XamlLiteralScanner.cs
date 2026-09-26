using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Clicalo.Analyzers.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Clicalo.Analyzers.Presentation;

/// <summary>
/// CLC0006 over the <c>*.xaml</c> files a project passes as <c>AdditionalFiles</c>. Reports literal text in text
/// attributes (<c>Text</c>, <c>Content</c>, <c>Header</c>, <c>ToolTip</c>, <c>Title</c>, <c>AutomationProperties.Name</c>…),
/// in the content of text elements (<c>&lt;TextBlock&gt;Hola&lt;/TextBlock&gt;</c>), in <c>Setter</c> values and in the
/// <c>StringFormat</c>/<c>FallbackValue</c>/<c>TargetNullValue</c> of bindings; and literal colors anywhere.
/// Markup extensions (<c>{Binding}</c>, <c>{x:Static}</c>, <c>{StaticResource}</c>, <c>{DynamicResource}</c>…) are not literals.
/// </summary>
internal static class XamlLiteralScanner
{
    private const string XamlLanguageNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string DesignNamespace = "http://schemas.microsoft.com/expression/blend/2008";
    private const string CompatibilityNamespace =
        "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private const string SuppressionPrefix = "CLC0006:";
    private const string AutomationPropertiesOwner = "AutomationProperties";
    private const string StringFormatWord = "StringFormat";

    /// <summary>Elements whose direct text content is shown to the user.</summary>
    private static readonly HashSet<string> TextElements = new(StringComparer.Ordinal)
    {
        "AccessText",
        "Bold",
        "Button",
        "CheckBox",
        "ComboBoxItem",
        "ContentControl",
        "Expander",
        "GroupBox",
        "HeaderedContentControl",
        "Hyperlink",
        "Italic",
        "Label",
        "ListBoxItem",
        "MenuItem",
        "Paragraph",
        "RadioButton",
        "RepeatButton",
        "Run",
        "Span",
        "String",
        "TabItem",
        "TextBlock",
        "TextBox",
        "ToggleButton",
        "ToolTip",
        "Underline",
    };

    /// <summary>Scans one additional file when it is XAML from a project or type inside <paramref name="scope"/>.</summary>
    public static void Analyze(AdditionalFileAnalysisContext context, NamespaceScope scope)
    {
        var file = context.AdditionalFile;
        if (!file.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var text = file.GetText(context.CancellationToken);
        var root = text is null ? null : Parse(text.ToString())?.Root;
        if (
            text is null
            || root is null
            || !IsInScope(root, context.Compilation.AssemblyName, scope)
        )
        {
            return;
        }

        var scanner = new Scan(context, new XamlSource(file.Path, text));
        scanner.Visit(root);
    }

    private static XDocument? Parse(string text)
    {
        // Parse errors are the XAML compiler's to report; a broken file is simply skipped here.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), settings);
            return XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>A view (with <c>x:Class</c>) is judged by its class; a resource dictionary by its assembly.</summary>
    private static bool IsInScope(XElement root, string? assemblyName, NamespaceScope scope)
    {
        var xamlClass = root.Attribute(XName.Get("Class", XamlLanguageNamespace))?.Value;
        if (!string.IsNullOrWhiteSpace(xamlClass))
        {
            return scope.Contains(xamlClass!.Trim());
        }

        return assemblyName is not null && scope.Contains(assemblyName);
    }

    /// <summary>
    /// <c>&lt;!-- CLC0006: reason --&gt;</c> right before an element exempts that element and its content. The reason is
    /// mandatory: a bare <c>CLC0006:</c> suppresses nothing.
    /// </summary>
    private static bool IsSuppressed(XElement element)
    {
        for (var node = element.PreviousNode; node is not null; node = node.PreviousNode)
        {
            if (node is XText text && string.IsNullOrWhiteSpace(text.Value))
            {
                continue;
            }

            if (node is not XComment comment)
            {
                return false;
            }

            var value = comment.Value.Trim();
            return value.StartsWith(SuppressionPrefix, StringComparison.Ordinal)
                && VisibleText.HasLetter(value.Substring(SuppressionPrefix.Length));
        }

        return false;
    }

    private static bool IsMarkupOnlyNamespace(string namespaceName) =>
        namespaceName is XamlLanguageNamespace or DesignNamespace or CompatibilityNamespace;

    /// <summary>Splits <c>AutomationProperties.Name</c> or <c>TextBlock.Text</c> into owner and property.</summary>
    private static (string Owner, string Property) Split(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot < 0 ? (string.Empty, name) : (name.Substring(0, dot), name.Substring(dot + 1));
    }

    private static bool IsTextProperty(string owner, string property) =>
        string.Equals(owner, AutomationPropertiesOwner, StringComparison.Ordinal)
            ? property is "Name" or "HelpText" or "ItemStatus"
            : VisibleText.IsTextName(property);

    private sealed class Scan
    {
        private readonly AdditionalFileAnalysisContext _context;
        private readonly XamlSource _source;
        private readonly CancellationToken _cancellationToken;

        public Scan(AdditionalFileAnalysisContext context, XamlSource source)
        {
            _context = context;
            _source = source;
            _cancellationToken = context.CancellationToken;
        }

        public void Visit(XElement element)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (IsSuppressed(element) || element.Name.NamespaceName is DesignNamespace)
            {
                return;
            }

            foreach (var attribute in element.Attributes())
            {
                if (
                    !attribute.IsNamespaceDeclaration
                    && !IsMarkupOnlyNamespace(attribute.Name.NamespaceName)
                )
                {
                    VisitAttribute(element, attribute);
                }
            }

            foreach (var node in element.Nodes())
            {
                switch (node)
                {
                    case XText text:
                        VisitText(element, text);
                        break;
                    case XElement child:
                        Visit(child);
                        break;
                }
            }
        }

        private void VisitAttribute(XElement element, XAttribute attribute)
        {
            // <Setter Property="Text" Value="Hola"/> sets Text: judge the value by the property it targets.
            var (owner, property) =
                string.Equals(element.Name.LocalName, "Setter", StringComparison.Ordinal)
                && string.Equals(attribute.Name.LocalName, "Value", StringComparison.Ordinal)
                    ? Split(element.Attribute("Property")?.Value.Trim() ?? string.Empty)
                    : Split(attribute.Name.LocalName);
            var target = element.Name.LocalName + "." + attribute.Name.LocalName;
            var raw = attribute.Value;

            if (MarkupExtensionText.IsExtension(raw))
            {
                foreach (var pair in MarkupExtensionText.NamedLiterals(raw))
                {
                    var isFormat = pair.Key.EndsWith(StringFormatWord, StringComparison.Ordinal);
                    if (isFormat || pair.Key is "FallbackValue" or "TargetNullValue")
                    {
                        Classify(owner, property, pair.Value, isFormat, target, attribute);
                    }
                }

                return;
            }

            var isFormatAttribute = property.EndsWith(StringFormatWord, StringComparison.Ordinal);
            Classify(
                owner,
                property,
                MarkupExtensionText.Unescape(raw),
                isFormatAttribute,
                target,
                attribute
            );
        }

        private void VisitText(XElement element, XText node)
        {
            var value = node.Value.Trim();
            if (value.Length == 0)
            {
                return;
            }

            var name = element.Name.LocalName;
            var (owner, property) = Split(name);
            var isPropertyElement = owner.Length > 0;
            var location = _source.ContentOf(node);
            if (
                ColorLiterals.IsColorLiteral(value)
                || (ColorLiterals.IsColorName(property) && ColorLiterals.IsNamedColor(value))
            )
            {
                ReportColor(value, location);
            }
            else if (
                (isPropertyElement ? IsTextProperty(owner, property) : TextElements.Contains(name))
                && VisibleText.HasLetter(value)
            )
            {
                ReportText(value, name, location);
            }
        }

        private void Classify(
            string owner,
            string property,
            string literal,
            bool isFormat,
            string target,
            XAttribute attribute
        )
        {
            var value = literal.Trim();
            if (value.Length == 0)
            {
                return;
            }

            if (
                ColorLiterals.IsColorLiteral(value)
                || (ColorLiterals.IsColorName(property) && ColorLiterals.IsNamedColor(value))
            )
            {
                ReportColor(value, _source.ValueOf(attribute, value));
                return;
            }

            var visible = isFormat ? MarkupExtensionText.VisiblePartOfFormat(value) : value;
            if ((isFormat || IsTextProperty(owner, property)) && VisibleText.HasLetter(visible))
            {
                ReportText(value, target, _source.ValueOf(attribute, value));
            }
        }

        private void ReportText(string text, string target, Location location) =>
            _context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.LiteralText,
                    location,
                    VisibleText.ForMessage(text),
                    target
                )
            );

        private void ReportColor(string color, Location location) =>
            _context.ReportDiagnostic(Diagnostic.Create(Descriptors.LiteralColor, location, color));
    }
}
