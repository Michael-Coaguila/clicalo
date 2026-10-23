using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.Windowing.IntegrationTests.Controls;

/// <summary>
/// Reads the look a sealed template gives under a mouse or a touchpad. <c>IsMouseOver</c> is set only by the input
/// system, so a headless test reads the triggers on it: what they paint and whether the states come after them (the
/// later trigger of a template wins).
/// </summary>
internal static class PointerHover
{
    /// <summary>The triggers of <paramref name="template"/> that need the pointer over the control.</summary>
    public static IReadOnlyList<TriggerBase> Of(ControlTemplate template) =>
        [
            .. template.Triggers.Where(static trigger =>
                trigger switch
                {
                    Trigger single => single.Property == UIElement.IsMouseOverProperty,
                    MultiTrigger multi => multi.Conditions.Any(static condition =>
                        condition.Property == UIElement.IsMouseOverProperty
                    ),
                    _ => false,
                }
            ),
        ];

    /// <summary>What <paramref name="trigger"/> paints: the part, the property and the color token of each setter.</summary>
    public static IReadOnlyList<(
        string? Part,
        DependencyProperty Property,
        ColorToken? Token
    )> Paints(TriggerBase trigger) =>
        [
            .. Setters(trigger)
                .OfType<Setter>()
                .Select(static setter =>
                    (
                        (string?)setter.TargetName,
                        setter.Property,
                        setter.Value is DynamicResourceExtension { ResourceKey: ThemeBrushKey key }
                            ? key.Token
                            : (ColorToken?)null
                    )
                ),
        ];

    /// <summary>
    /// Whether every trigger of <paramref name="template"/> on <paramref name="state"/> comes after the triggers of the
    /// pointer, so the state still paints the control under it.
    /// </summary>
    public static bool StateWins(ControlTemplate template, DependencyProperty state)
    {
        var last = Of(template).Max(trigger => template.Triggers.IndexOf(trigger));
        var states = template
            .Triggers.OfType<Trigger>()
            .Where(trigger => trigger.Property == state)
            .ToList();
        return states.Count > 0
            && states.TrueForAll(trigger => template.Triggers.IndexOf(trigger) > last);
    }

    private static SetterBaseCollection Setters(TriggerBase trigger) =>
        trigger switch
        {
            Trigger single => single.Setters,
            MultiTrigger multi => multi.Setters,
            _ => throw new ArgumentException("Not a trigger with setters.", nameof(trigger)),
        };
}
