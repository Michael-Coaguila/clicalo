using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using FlaUI.Core.Definitions;
using WpfExpandCollapseState = System.Windows.Automation.ExpandCollapseState;
using WpfLiveSetting = System.Windows.Automation.AutomationLiveSetting;
using WpfToggleState = System.Windows.Automation.ToggleState;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// Takes a <see cref="UiaNode"/> snapshot from the WPF automation peers, in process and headless (every PR). Run it
/// on the WPF thread. Elements that are not on a screen report their logical render size times
/// <c>scale</c> as bounds, so UIA005 still applies.
/// </summary>
public static class PeerSnapshot
{
    /// <summary>A synthetic root pane named <paramref name="name"/> over the peers of <paramref name="elements"/>.</summary>
    public static UiaNode CaptureElements(
        string name,
        IEnumerable<UIElement> elements,
        double scale = 1
    )
    {
        ArgumentNullException.ThrowIfNull(elements);
        return new UiaNode
        {
            Name = name,
            ControlType = ControlType.Pane,
            Scale = scale,
            Children =
            [
                .. elements
                    .Select(UIElementAutomationPeer.CreatePeerForElement)
                    .OfType<AutomationPeer>()
                    .Select(peer => Capture(peer, scale)),
            ],
        };
    }

    /// <summary>The control view under <paramref name="peer"/>.</summary>
    public static UiaNode Capture(AutomationPeer peer, double scale = 1)
    {
        ArgumentNullException.ThrowIfNull(peer);
        var toggle = peer.GetPattern(PatternInterface.Toggle) as IToggleProvider;
        var expand = peer.GetPattern(PatternInterface.ExpandCollapse) as IExpandCollapseProvider;
        return new UiaNode
        {
            AutomationId = peer.GetAutomationId() ?? string.Empty,
            Name = peer.GetName() ?? string.Empty,
            ControlType = Enum.Parse<ControlType>(peer.GetAutomationControlType().ToString()),
            Patterns = PatternsOf(peer),
            ToggleState = toggle is null ? null : Map(toggle.ToggleState),
            ExpandCollapseState = expand is null ? null : Map(expand.ExpandCollapseState),
            LiveSetting = Map(peer.GetLiveSetting()),
            HelpText = peer.GetHelpText() ?? string.Empty,
            ItemStatus = peer.GetItemStatus() ?? string.Empty,
            Bounds = BoundsOf(peer, scale),
            Scale = scale,
            IsEnabled = peer.IsEnabled(),
            IsOffscreen = peer.IsOffscreen(),
            IsKeyboardFocusable = peer.IsKeyboardFocusable(),
            HasKeyboardFocus = peer.HasKeyboardFocus(),
            Children = [.. ControlChildren(peer).Select(child => Capture(child, scale))],
        };
    }

    private static IEnumerable<AutomationPeer> ControlChildren(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren() ?? [])
        {
            if (child.IsControlElement())
            {
                yield return child;
            }
            else
            {
                foreach (var nested in ControlChildren(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private static UiaPatterns PatternsOf(AutomationPeer peer)
    {
        var patterns = UiaPatterns.None;
        foreach (
            var (pattern, flag) in new[]
            {
                (PatternInterface.Invoke, UiaPatterns.Invoke),
                (PatternInterface.Toggle, UiaPatterns.Toggle),
                (PatternInterface.ExpandCollapse, UiaPatterns.ExpandCollapse),
                (PatternInterface.SelectionItem, UiaPatterns.SelectionItem),
                (PatternInterface.RangeValue, UiaPatterns.RangeValue),
                (PatternInterface.Value, UiaPatterns.Value),
            }
        )
        {
            if (peer.GetPattern(pattern) is not null)
            {
                patterns |= flag;
            }
        }

        return patterns;
    }

    private static Rect BoundsOf(AutomationPeer peer, double scale)
    {
        var bounds = peer.GetBoundingRectangle();
        if (!bounds.IsEmpty && bounds.Width > 0)
        {
            return bounds;
        }

        return peer is UIElementAutomationPeer { Owner: { } owner }
            ? new Rect(0, 0, owner.RenderSize.Width * scale, owner.RenderSize.Height * scale)
            : Rect.Empty;
    }

    private static ToggleState Map(WpfToggleState state) =>
        Enum.Parse<ToggleState>(state.ToString());

    private static ExpandCollapseState Map(WpfExpandCollapseState state) =>
        Enum.Parse<ExpandCollapseState>(state.ToString());

    private static LiveSetting Map(WpfLiveSetting setting) =>
        Enum.Parse<LiveSetting>(setting.ToString());
}
