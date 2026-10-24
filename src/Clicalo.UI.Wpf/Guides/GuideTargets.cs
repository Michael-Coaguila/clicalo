using System.Windows;
using System.Windows.Media;
using Clicalo.Domain.Guides;

namespace Clicalo.UI.Wpf.Guides;

/// <summary>
/// Marks the controls a step of a guide points at (R-39): a view sets <c>GuideTargets.Target</c> on the control it
/// builds, and the guide finds it wherever the view is drawn, even after the view builds itself again.
/// </summary>
public static class GuideTargets
{
    /// <summary>Identifies the attached property <c>Target</c>.</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target",
        typeof(GuideTarget?),
        typeof(GuideTargets),
        new PropertyMetadata(null)
    );

    /// <summary>Marks <paramref name="element"/> as the control of <paramref name="target"/>.</summary>
    /// <param name="element">The control.</param>
    /// <param name="target">What it is for the guides.</param>
    public static void SetTarget(DependencyObject element, GuideTarget? target)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(TargetProperty, target);
    }

    /// <summary>Marks <paramref name="element"/> as the control of <paramref name="target"/>, and returns it.</summary>
    /// <typeparam name="T">The type of the control.</typeparam>
    /// <param name="element">The control.</param>
    /// <param name="target">What it is for the guides.</param>
    public static T Mark<T>(T element, GuideTarget target)
        where T : DependencyObject
    {
        SetTarget(element, target);
        return element;
    }

    /// <summary>What <paramref name="element"/> is for the guides, if anything.</summary>
    /// <param name="element">The control.</param>
    public static GuideTarget? GetTarget(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (GuideTarget?)element.GetValue(TargetProperty);
    }

    /// <summary>The visible control of <paramref name="target"/> under <paramref name="root"/>, if any.</summary>
    /// <param name="root">Where to look.</param>
    /// <param name="target">The control looked for.</param>
    public static FrameworkElement? Find(DependencyObject root, GuideTarget target)
    {
        ArgumentNullException.ThrowIfNull(root);
        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is UIElement { IsVisible: false })
            {
                continue;
            }

            if (node is FrameworkElement element && GetTarget(element) == target)
            {
                return element;
            }

            for (var i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
            {
                pending.Push(VisualTreeHelper.GetChild(node, i));
            }
        }

        return null;
    }
}
