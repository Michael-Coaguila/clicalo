using System.Globalization;
using System.Text;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// A stable text form of a UI Automation tree (blueprint §10.2, «instantánea de texto del árbol UIA»): one line per
/// element with its role, name, id, patterns, states, help text, item status and live setting. Bounds are left out,
/// because they depend on the monitor.
/// </summary>
public static class UiaTreeText
{
    /// <summary>The tree under <paramref name="root"/>, indented two spaces per level.</summary>
    public static string Format(UiaNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var text = new StringBuilder();
        Append(text, root, 0);
        return text.ToString();
    }

    private static void Append(StringBuilder text, UiaNode node, int depth)
    {
        text.Append(' ', depth * 2)
            .Append(node.ControlType)
            .Append(CultureInfo.InvariantCulture, $" «{node.Name}»");
        if (node.AutomationId.Length > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $" #{node.AutomationId}");
        }

        if (node.Patterns != UiaPatterns.None)
        {
            text.Append(CultureInfo.InvariantCulture, $" [{node.Patterns}]");
        }

        if (node.ToggleState is { } toggle)
        {
            text.Append(CultureInfo.InvariantCulture, $" toggle={toggle}");
        }

        if (node.ExpandCollapseState is { } expand)
        {
            text.Append(CultureInfo.InvariantCulture, $" expand={expand}");
        }

        if (node.HelpText.Length > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $" help=«{node.HelpText}»");
        }

        if (node.ItemStatus.Length > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $" status=«{node.ItemStatus}»");
        }

        if (node.LiveSetting != FlaUI.Core.Definitions.LiveSetting.Off)
        {
            text.Append(CultureInfo.InvariantCulture, $" live={node.LiveSetting}");
        }

        if (node.IsKeyboardFocusable)
        {
            text.Append(" focusable");
        }

        text.Append('\n');
        foreach (var child in node.Children)
        {
            Append(text, child, depth + 1);
        }
    }
}
