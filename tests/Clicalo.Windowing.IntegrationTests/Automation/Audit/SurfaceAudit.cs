using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;

namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>
/// The accessibility audit of one real view in one state (REG-02, REG-06): the UIA rules of
/// <see cref="UiaVerifier"/> on its control view (name, role, patterns, states, 44 × 44 bounds, no glyph as a name, a
/// dictation button next to every free text field) and, besides, that every touch target really answers on 44 × 44,
/// which UI Automation's rectangle alone does not tell (<see cref="TouchInput"/>):
/// <list type="bullet">
/// <item>in the windows that take WPF input (Control Center, welcome), a target is hit only where it is drawn: its
/// box, after the clips of its containers, must keep 44 × 44 (a row lower than its button leaves less to
/// touch);</item>
/// <item>in the surfaces of the panel, the pointer layer gives every target its bounds grown around their center to
/// 44 × 44 and resolves overlaps by the nearest center (REG-02), but only inside the window: the grown bounds that
/// fall inside it must keep 44 × 44 (a small button against the edge would lose its margin).</item>
/// </list>
/// </summary>
internal static class SurfaceAudit
{
    private const string TouchRule = "REG-02";
    private const double Rounding = 0.5;

    private static readonly PatternInterface[] Actionable =
    [
        PatternInterface.Invoke,
        PatternInterface.Toggle,
        PatternInterface.ExpandCollapse,
        PatternInterface.SelectionItem,
        PatternInterface.RangeValue,
    ];

    /// <summary>
    /// What every view is held to without a model of its own: the names of the dictation and paste buttons in the two
    /// languages ([searchDictate], [dictName] next to a name field, [keyPaste]; UIA010) and the 44 logical pixels of
    /// REG-02.
    /// </summary>
    public static UiaExpectations Expectations { get; } =
        new()
        {
            DictationNames =
            [
                "Dictar",
                "Dictate",
                "Dictar nombre",
                "Dictate name",
                "Pegar",
                "Paste",
            ],
        };

    /// <summary>Every broken rule of the view hosted in <paramref name="host"/>.</summary>
    public static IReadOnlyList<UiaViolation> Verify(AuditHost host, string name, TouchInput input)
    {
        ArgumentNullException.ThrowIfNull(host);
        return
        [
            .. UiaVerifier.Verify(host.Snapshot(name), Expectations),
            .. SmallTargets(host.Root, Expectations.MinimumTargetSize, input),
        ];
    }

    /// <summary>
    /// Fails with every broken rule, one per line, and the tree. <paramref name="atLeast"/> is the number of touch
    /// targets the state must show, so a view that did not build is not taken for a clean one.
    /// </summary>
    public static void ShouldPass(AuditHost host, string name, int atLeast, TouchInput input)
    {
        ArgumentNullException.ThrowIfNull(host);
        var tree = host.Snapshot(name);
        var targets = tree.DescendantsAndSelf().Count(static node => node.IsActionable);
        var violations = Verify(host, name, input);
        if (violations.Count == 0 && targets >= atLeast)
        {
            return;
        }

        var message = new StringBuilder();
        message.Append(
            CultureInfo.InvariantCulture,
            $"«{name}» shows {targets} touch targets (at least {atLeast} expected) and breaks {violations.Count} rules:"
        );
        foreach (var violation in violations)
        {
            message.AppendLine().Append("  ").Append(violation);
        }

        message.AppendLine().AppendLine("Tree:").Append(UiaTreeText.Format(tree));
        throw new Xunit.Sdk.XunitException(message.ToString());
    }

    /// <summary>
    /// The touch targets of the control view under <paramref name="root"/> that answer on less than
    /// <paramref name="minimum"/> × <paramref name="minimum"/> with the kind of input of their window.
    /// </summary>
    public static IReadOnlyList<UiaViolation> SmallTargets(
        FrameworkElement root,
        double minimum,
        TouchInput input
    )
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
            if (
                peer
                    is UIElementAutomationPeer
                    {
                        Owner: { IsVisible: true, IsEnabled: true } element
                    }
                && peer.IsControlElement()
                && Actionable.Any(pattern => peer.GetPattern(pattern) is not null)
            )
            {
                var box =
                    input == TouchInput.PointerLayer
                        ? ReachableBox(element, root, minimum)
                        : VisibleBox(element, root);
                if (box.Width + Rounding < minimum || box.Height + Rounding < minimum)
                {
                    violations.Add(
                        new UiaViolation(
                            TouchRule,
                            Label(peer),
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"answers on {box.Width:0.#} × {box.Height:0.#} ({What(input)}); the touch target is {minimum:0.#} × {minimum:0.#}."
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

    private static string What(TouchInput input) =>
        input == TouchInput.PointerLayer
            ? "its bounds grown to the minimum, inside the window"
            : "its box after the clips of its containers";

    private static string Label(AutomationPeer peer) =>
        peer.GetAutomationId() is { Length: > 0 } id ? id : peer.GetName() ?? string.Empty;

    /// <summary>
    /// What the pointer layer of a surface gives a target (<c>TouchBounds.Of</c> with its margin): its bounds grown
    /// around their center to the minimum, as far as they are inside the window. A target of a scroll area that is not
    /// whole in view keeps its grown bounds: where it scrolls to is not a defect.
    /// </summary>
    private static Rect ReachableBox(UIElement element, FrameworkElement root, double minimum)
    {
        var bounds = element
            .TransformToAncestor(root)
            .TransformBounds(new Rect(element.RenderSize));
        var width = Math.Max(bounds.Width, minimum);
        var height = Math.Max(bounds.Height, minimum);
        var grown = new Rect(
            bounds.X - ((width - bounds.Width) / 2),
            bounds.Y - ((height - bounds.Height) / 2),
            width,
            height
        );
        var window = new Rect(root.RenderSize);
        if (!window.Contains(bounds))
        {
            return grown;
        }

        grown.Intersect(window);
        return grown;
    }

    private static Rect VisibleBox(UIElement element, FrameworkElement root)
    {
        var box = new Rect(element.RenderSize);
        Visual current = element;
        while (!box.IsEmpty)
        {
            if (current is FrameworkElement framework)
            {
                if (LayoutInformation.GetLayoutClip(framework) is { } layoutClip)
                {
                    box.Intersect(layoutClip.Bounds);
                }

                if (framework.Clip is { } clip)
                {
                    box.Intersect(clip.Bounds);
                }
            }

            if (
                ReferenceEquals(current, root)
                || VisualTreeHelper.GetParent(current) is not Visual parent
                || parent is ScrollContentPresenter
                || box.IsEmpty
            )
            {
                break;
            }

            box = current.TransformToAncestor(parent).TransformBounds(box);
            current = parent;
        }

        return box.IsEmpty ? new Rect(0, 0, 0, 0) : box;
    }
}
