using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Views;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// The guide strip: a NON-activatable surface with the current step, the big buttons and the measurements, so the
/// maintainer can follow and mark each cycle with a finger or by voice without taking the focus from the app under
/// test. Its buttons are instruments: they never count as repetitions.
/// </summary>
internal sealed class GuideStripSurface : LabSurface
{
    /// <summary>Creates the strip.</summary>
    public GuideStripSurface(SurfaceRegistry registry, LabSurfaceContext context)
        : base(LabSurfaceIds.Guide, registry, context, SurfaceGroup.Any)
    {
        View = new GuideView((tile, width, height) => AddTile(tile, width, height));
        Content = View;
    }

    /// <summary>The content.</summary>
    public GuideView View { get; }
}
