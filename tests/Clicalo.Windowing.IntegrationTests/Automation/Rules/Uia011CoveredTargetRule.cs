using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// UIA011: a layer covers for every input, not only for the eye (D21). Every enabled actionable element that UI
/// Automation reports on screen (<c>IsOffscreen</c> false) must be what is drawn at the centre of the part of it that is
/// in view: the element itself or something inside it. When something else is on top there (an opaque layer, a card),
/// the finger, the Tab key, Narrator and the numbers of voice access would reach a control that cannot be seen. The rule
/// reads the real view (its peers and its visual tree), not a snapshot, because UI Automation does not say what is
/// drawn over what.
/// </summary>
public static class Uia011CoveredTargetRule
{
    /// <summary>The identifier of the rule.</summary>
    public const string Id = "UIA011";

    /// <summary>
    /// Less than this in view is not drawn: a cell that ends on the edge of its scroll area keeps a box of no height
    /// there, which UI Automation still counts as on screen.
    /// </summary>
    private const double Sliver = 1;

    private static readonly PatternInterface[] Actionable =
    [
        PatternInterface.Invoke,
        PatternInterface.Toggle,
        PatternInterface.ExpandCollapse,
        PatternInterface.SelectionItem,
        PatternInterface.RangeValue,
        PatternInterface.Value,
    ];

    /// <summary>The covered targets of the view under <paramref name="root"/>.</summary>
    /// <param name="root">The root of a view hosted on a presentation source, laid out.</param>
    public static IReadOnlyList<UiaViolation> Verify(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var violations = new List<UiaViolation>();
        if (UIElementAutomationPeer.CreatePeerForElement(root) is { } rootPeer)
        {
            Walk(rootPeer);
        }

        return violations;

        void Walk(AutomationPeer peer)
        {
            peer.ResetChildrenCache();
            if (
                peer
                    is UIElementAutomationPeer
                    {
                        Owner: FrameworkElement { IsVisible: true, IsEnabled: true } element
                    }
                && peer.IsControlElement()
                && !peer.IsOffscreen()
                && Actionable.Any(pattern => peer.GetPattern(pattern) is not null)
                && InView(element, root)
                    is { IsEmpty: false, Width: >= Sliver, Height: >= Sliver } box
            )
            {
                var centre = new Point(box.X + (box.Width / 2), box.Y + (box.Height / 2));
                if (
                    VisualTreeHelper.HitTest(root, centre)?.VisualHit is DependencyObject hit
                    && !IsSameOrInside(hit, element)
                    && !IsSameOrInside(element, hit)
                )
                {
                    violations.Add(
                        new UiaViolation(
                            Id,
                            Label(peer),
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"is on screen for UI Automation but under {Describe(hit)} at {centre.X:0}, {centre.Y:0}: whatever covers it must hide it."
                            )
                        )
                    );
                }
            }

            foreach (var child in peer.GetChildren() ?? [])
            {
                Walk(child);
            }
        }
    }

    /// <summary>Whether <paramref name="node"/> is <paramref name="ancestor"/> or inside it, in the visual tree.</summary>
    private static bool IsSameOrInside(DependencyObject node, DependencyObject ancestor)
    {
        for (DependencyObject? current = node; current is not null; current = Parent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? Parent(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);

    /// <summary>
    /// The part of <paramref name="element"/> in view, in the coordinates of <paramref name="root"/>: its box after the
    /// clips of all its containers, scroll areas included.
    /// </summary>
    private static Rect InView(FrameworkElement element, FrameworkElement root)
    {
        var box = new Rect(element.RenderSize);
        Visual current = element;
        while (!ReferenceEquals(current, root))
        {
            if (
                current is FrameworkElement framework
                && LayoutInformation.GetLayoutClip(framework) is { } layoutClip
            )
            {
                box.Intersect(layoutClip.Bounds);
            }

            if (current is UIElement { Clip: { } clip })
            {
                box.Intersect(clip.Bounds);
            }

            if (box.IsEmpty || VisualTreeHelper.GetParent(current) is not Visual parent)
            {
                return Rect.Empty;
            }

            box = current.TransformToAncestor(parent).TransformBounds(box);
            current = parent;
        }

        box.Intersect(new Rect(root.RenderSize));
        return box;
    }

    private static string Describe(DependencyObject hit)
    {
        for (DependencyObject? current = hit; current is not null; current = Parent(current))
        {
            if (
                current is UIElement element
                && UIElementAutomationPeer.CreatePeerForElement(element) is { } peer
                && peer.IsControlElement()
                && peer.GetName() is { Length: > 0 } name
            )
            {
                return "«" + name + "»";
            }
        }

        return hit.GetType().Name;
    }

    private static string Label(AutomationPeer peer) =>
        peer.GetAutomationId() is { Length: > 0 } id ? id : peer.GetName() ?? string.Empty;
}
