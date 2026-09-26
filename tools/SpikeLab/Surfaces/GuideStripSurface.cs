using System.Windows;
using System.Windows.Media;
using Clicalo.Domain.Geometry;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Views;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// The guide strip: a NON-activatable surface with the current step, the big buttons and the measurements, so the
/// maintainer can follow and mark each cycle with a finger or by voice without taking the focus from the app under
/// test. Its buttons are instruments: they never count as repetitions, and neither does a drag of its handle. It
/// starts at the bottom of the work area and keeps its bottom edge there while it grows or folds; dragged or moved to
/// the upper half, it keeps its top edge instead. It never leaves the work area.
/// </summary>
internal sealed class GuideStripSurface : LabSurface
{
    private bool _anchoredAtBottom = true;
    private int? _bottom;

    /// <summary>Creates the strip.</summary>
    public GuideStripSurface(SurfaceRegistry registry, LabSurfaceContext context)
        : base(LabSurfaceIds.Guide, registry, context, SurfaceGroup.Any)
    {
        View = new GuideView((tile, width, height) => AddTile(tile, width, height));
        Content = View;
        LocationChanged += (_, _) => RememberBottom();
    }

    /// <summary>The content.</summary>
    public GuideView View { get; }

    /// <summary>True while the strip keeps its bottom edge (it is in the lower half of the work area).</summary>
    public bool IsAnchoredAtBottom => _anchoredAtBottom;

    /// <inheritdoc />
    protected override FrameworkElement DragHandle => View.Grip;

    /// <inheritdoc />
    protected override bool CountsDrags => false;

    /// <summary>«Plegar la tira» and «Desplegar la tira».</summary>
    public void ToggleFolded() => View.IsFolded = !View.IsFolded;

    /// <summary>«Ver instrucción completa» and «Acortar la instrucción».</summary>
    public void ToggleFullInstruction() => View.ShowsFullInstruction = !View.ShowsFullInstruction;

    /// <summary>«Mover la tira»: to the top of the work area when it is at the bottom, and back.</summary>
    public void MoveToOtherHalf()
    {
        var bounds = Bounds();
        var area = bounds.IsEmpty ? bounds : WorkAreas.Of(bounds);
        if (!IsShown || area.IsEmpty)
        {
            return;
        }

        var gap = ToPhysical(LabLayout.StripBottomGap);
        _anchoredAtBottom = !_anchoredAtBottom;
        _bottom = _anchoredAtBottom ? area.Bottom - gap : null;
        TryMove(
            LabLayout.Inside(
                bounds with
                {
                    Top = _anchoredAtBottom ? area.Bottom - gap - bounds.Height : area.Top + gap,
                },
                area
            )
        );
    }

    /// <inheritdoc />
    protected override void OnShown()
    {
        var bounds = Bounds();
        var area = bounds.IsEmpty ? bounds : WorkAreas.Of(bounds);
        if (_anchoredAtBottom && _bottom is null && !area.IsEmpty)
        {
            _bottom = area.Bottom - ToPhysical(LabLayout.StripBottomGap);
        }

        LimitHeight(area);
        KeepInsideWorkArea(_anchoredAtBottom ? _bottom : null);
    }

    /// <inheritdoc />
    protected override void OnDragEnded()
    {
        var bounds = Bounds();
        var area = bounds.IsEmpty ? bounds : WorkAreas.Of(bounds);
        if (!area.IsEmpty)
        {
            _anchoredAtBottom = LabLayout.IsInLowerHalf(bounds, area);
        }

        KeepInsideWorkArea();
        RememberBottom();
    }

    /// <inheritdoc />
    protected override void OnResized()
    {
        var bounds = Bounds();
        LimitHeight(bounds.IsEmpty ? bounds : WorkAreas.Of(bounds));
        KeepInsideWorkArea(_anchoredAtBottom ? _bottom : null);
    }

    /// <summary>
    /// Never taller than the work area of its monitor: the whole instruction gives up lines before the buttons leave
    /// the screen (<see cref="GuideView"/> keeps the buttons in their own row).
    /// </summary>
    private void LimitHeight(PhysicalRect area)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
        var limit = LabLayout.StripMaximumHeight(area.Height / scale);
        if (!MaxHeight.Equals(limit))
        {
            MaxHeight = limit;
        }
    }

    private void RememberBottom()
    {
        if (_anchoredAtBottom && IsShown && Bounds() is { IsEmpty: false } bounds)
        {
            _bottom = bounds.Bottom;
        }
    }
}
