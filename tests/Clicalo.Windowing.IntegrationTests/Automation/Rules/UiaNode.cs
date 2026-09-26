using System.Collections.Immutable;
using System.Windows;
using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// A snapshot of one element of the UI Automation control view, taken either in process from the WPF peers
/// (<see cref="PeerSnapshot"/>) or by a UIA client on another thread (<see cref="FlaUiSnapshot"/>), so the same rules
/// check both.
/// </summary>
public sealed record UiaNode
{
    /// <summary>AutomationId.</summary>
    public string AutomationId { get; init; } = string.Empty;

    /// <summary>Name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>ControlType.</summary>
    public ControlType ControlType { get; init; } = ControlType.Custom;

    /// <summary>Supported patterns among <see cref="UiaPatterns"/>.</summary>
    public UiaPatterns Patterns { get; init; }

    /// <summary>ToggleState, when the Toggle pattern is supported.</summary>
    public ToggleState? ToggleState { get; init; }

    /// <summary>ExpandCollapseState, when the ExpandCollapse pattern is supported.</summary>
    public ExpandCollapseState? ExpandCollapseState { get; init; }

    /// <summary>LiveSetting.</summary>
    public LiveSetting LiveSetting { get; init; }

    /// <summary>HelpText.</summary>
    public string HelpText { get; init; } = string.Empty;

    /// <summary>ItemStatus.</summary>
    public string ItemStatus { get; init; } = string.Empty;

    /// <summary>BoundingRectangle in physical pixels.</summary>
    public Rect Bounds { get; init; } = Rect.Empty;

    /// <summary>Physical pixels per logical pixel of the element's monitor.</summary>
    public double Scale { get; init; } = 1;

    /// <summary>IsEnabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>IsOffscreen.</summary>
    public bool IsOffscreen { get; init; }

    /// <summary>IsKeyboardFocusable.</summary>
    public bool IsKeyboardFocusable { get; init; }

    /// <summary>HasKeyboardFocus.</summary>
    public bool HasKeyboardFocus { get; init; }

    /// <summary>Children in the control view.</summary>
    public ImmutableArray<UiaNode> Children { get; init; } = [];

    /// <summary>True when the element supports a pattern that voice or a switch can act on.</summary>
    public bool IsActionable => (Patterns & UiaPatterns.Actionable) != UiaPatterns.None;

    /// <summary>The AutomationId, or the name when there is none, for messages.</summary>
    public string Label => AutomationId.Length > 0 ? AutomationId : Name;

    /// <summary>This node and every descendant, depth first.</summary>
    public IEnumerable<UiaNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var node in child.DescendantsAndSelf())
            {
                yield return node;
            }
        }
    }

    /// <summary>Every parent and child pair below this node, for rules about siblings.</summary>
    public IEnumerable<(UiaNode Parent, UiaNode Child)> Edges()
    {
        foreach (var child in Children)
        {
            yield return (this, child);
            foreach (var edge in child.Edges())
            {
                yield return edge;
            }
        }
    }
}
