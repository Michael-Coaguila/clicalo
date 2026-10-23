using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// What never reaches UI Automation (ACC-001, REG-06): an element of the visual tree with a name, a help text or a
/// live setting but no automation peer. WPF builds the control view from peers only, so a name put on a Border, a Grid
/// or a StackPanel is never read, and a live region without a peer never raises anything. Unlike the UIA rules, it walks
/// the visual tree, because what it looks for is exactly what a snapshot of the peers cannot see.
/// </summary>
public static class PeerlessPropertyRule
{
    /// <summary>The requirement the rule guards.</summary>
    public const string Id = "ACC-001";

    /// <summary>Every element under <paramref name="root"/> (itself included) that carries what no reader will hear.</summary>
    /// <param name="root">The root of the visual tree to walk.</param>
    /// <param name="liveRegionsOnly">
    /// Whether only a live setting counts, and not a name or a help text: for the views whose names on plain containers
    /// are still to be given a peer (the Control Center, D21 review).
    /// </param>
    public static IReadOnlyList<UiaViolation> Check(
        DependencyObject root,
        bool liveRegionsOnly = false
    )
    {
        ArgumentNullException.ThrowIfNull(root);
        var violations = new List<UiaViolation>();
        Walk(root);
        return violations;

        void Walk(DependencyObject node)
        {
            if (
                node is UIElement element
                && Carried(element, liveRegionsOnly) is { } carried
                && UIElementAutomationPeer.CreatePeerForElement(element) is null
            )
            {
                violations.Add(
                    new UiaViolation(
                        Id,
                        Label(element),
                        $"is a {element.GetType().Name} with {carried} but no automation peer: UI Automation never sees it."
                    )
                );
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                Walk(VisualTreeHelper.GetChild(node, i));
            }
        }
    }

    /// <summary>What <paramref name="element"/> says to UI Automation, or <see langword="null"/> when it says nothing.</summary>
    private static string? Carried(UIElement element, bool liveRegionsOnly)
    {
        var said = new List<string>();
        if (!liveRegionsOnly && AutomationProperties.GetName(element) is { Length: > 0 } name)
        {
            said.Add($"the name «{name}»");
        }

        if (!liveRegionsOnly && AutomationProperties.GetHelpText(element) is { Length: > 0 } help)
        {
            said.Add($"the help text «{help}»");
        }

        if (
            AutomationProperties.GetLiveSetting(element)
            is var live
                and not AutomationLiveSetting.Off
        )
        {
            said.Add($"LiveSetting {live}");
        }

        return said.Count == 0 ? null : string.Join(", ", said);
    }

    private static string Label(UIElement element) =>
        AutomationProperties.GetAutomationId(element) is { Length: > 0 } id ? id
        : AutomationProperties.GetName(element) is { Length: > 0 } name ? name
        : element.GetType().Name;
}
