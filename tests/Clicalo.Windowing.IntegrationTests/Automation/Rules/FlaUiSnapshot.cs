using System.Windows;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// Takes a <see cref="UiaNode"/> snapshot of a real window through a UI Automation client (FlaUI UIA3), walking the
/// control view as Voice access and Narrator see it. Never call it on the surface's UI thread.
/// </summary>
public static class FlaUiSnapshot
{
    /// <summary>The control view under <paramref name="element"/>.</summary>
    /// <param name="automation">The client.</param>
    /// <param name="element">The root, usually the surface window.</param>
    /// <param name="scale">Physical pixels per logical pixel of the window's monitor.</param>
    public static UiaNode Capture(
        AutomationBase automation,
        AutomationElement element,
        double scale
    )
    {
        ArgumentNullException.ThrowIfNull(automation);
        ArgumentNullException.ThrowIfNull(element);
        return Capture(automation.TreeWalkerFactory.GetControlViewWalker(), element, scale);
    }

    private static UiaNode Capture(ITreeWalker walker, AutomationElement element, double scale)
    {
        var properties = element.Properties;
        var patterns = element.Patterns;
        var bounds = properties.BoundingRectangle.ValueOrDefault;
        var children = new List<UiaNode>();
        for (
            var child = walker.GetFirstChild(element);
            child is not null;
            child = walker.GetNextSibling(child)
        )
        {
            children.Add(Capture(walker, child, scale));
        }

        return new UiaNode
        {
            AutomationId = properties.AutomationId.ValueOrDefault ?? string.Empty,
            Name = properties.Name.ValueOrDefault ?? string.Empty,
            ControlType = properties.ControlType.ValueOrDefault,
            Patterns =
                (patterns.Invoke.IsSupported ? UiaPatterns.Invoke : UiaPatterns.None)
                | (patterns.Toggle.IsSupported ? UiaPatterns.Toggle : UiaPatterns.None)
                | (
                    patterns.ExpandCollapse.IsSupported
                        ? UiaPatterns.ExpandCollapse
                        : UiaPatterns.None
                )
                | (
                    patterns.SelectionItem.IsSupported
                        ? UiaPatterns.SelectionItem
                        : UiaPatterns.None
                )
                | (patterns.RangeValue.IsSupported ? UiaPatterns.RangeValue : UiaPatterns.None)
                | (patterns.Value.IsSupported ? UiaPatterns.Value : UiaPatterns.None),
            ToggleState = patterns.Toggle.PatternOrDefault?.ToggleState.ValueOrDefault,
            ExpandCollapseState = patterns
                .ExpandCollapse
                .PatternOrDefault
                ?.ExpandCollapseState
                .ValueOrDefault,
            LiveSetting = properties.LiveSetting.ValueOrDefault,
            HelpText = properties.HelpText.ValueOrDefault ?? string.Empty,
            ItemStatus = properties.ItemStatus.ValueOrDefault ?? string.Empty,
            Bounds = bounds.IsEmpty
                ? Rect.Empty
                : new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            Scale = scale,
            IsEnabled = properties.IsEnabled.ValueOrDefault,
            IsOffscreen = properties.IsOffscreen.ValueOrDefault,
            IsKeyboardFocusable = properties.IsKeyboardFocusable.ValueOrDefault,
            HasKeyboardFocus = properties.HasKeyboardFocus.ValueOrDefault,
            Children = [.. children],
        };
    }
}
