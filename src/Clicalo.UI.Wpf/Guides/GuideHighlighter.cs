using System.Windows;
using System.Windows.Documents;
using Clicalo.Domain.Guides;

namespace Clicalo.UI.Wpf.Guides;

/// <summary>
/// Draws the <see cref="GuideRing"/> of the step on show around its control inside one window (R-39). Views build their
/// controls again on many changes (a new size, a new language), so the ring looks for its control again whenever it
/// is gone, after the next layout pass.
/// </summary>
public sealed class GuideHighlighter
{
    private readonly FrameworkElement _root;
    private GuideTarget? _target;
    private FrameworkElement? _element;
    private GuideRing? _ring;
    private AdornerLayer? _layer;

    /// <summary>Highlights inside <paramref name="root"/>.</summary>
    /// <param name="root">The content of a window.</param>
    public GuideHighlighter(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
        _root.LayoutUpdated += (_, _) => FollowLayout();
    }

    /// <summary>The control with the ring now, if any.</summary>
    public FrameworkElement? Highlighted => _ring is null ? null : _element;

    /// <summary>Points at <paramref name="target"/>, or at nothing.</summary>
    /// <param name="target">The control of the step on show here, or <see langword="null"/>.</param>
    public void Show(GuideTarget? target)
    {
        if (_target == target && (target is null || IsLive(_element)))
        {
            return;
        }

        _target = target;
        Attach(target is { } found ? GuideTargets.Find(_root, found) : null);
    }

    private bool IsLive(FrameworkElement? element) =>
        element is { IsVisible: true } && _root.IsAncestorOf(element) && _ring is not null;

    private void FollowLayout()
    {
        if (_target is { } target && !IsLive(_element))
        {
            Attach(GuideTargets.Find(_root, target));
        }
    }

    private void Attach(FrameworkElement? element)
    {
        if (ReferenceEquals(element, _element) && (element is null || _ring is not null))
        {
            return;
        }

        Detach();
        _element = element;
        if (element is null || AdornerLayer.GetAdornerLayer(element) is not { } layer)
        {
            return;
        }

        _ring = new GuideRing(element);
        _layer = layer;
        layer.Add(_ring);
    }

    private void Detach()
    {
        if (_ring is not null)
        {
            _layer?.Remove(_ring);
        }

        _ring = null;
        _layer = null;
        _element = null;
    }
}
