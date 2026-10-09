using Clicalo.Domain.Geometry;

namespace Clicalo.Application.Ports;

/// <summary>
/// The last pointer position outside Clícalo's windows (EJE-009, blueprint §7.11), kept by
/// <c>PointerPositionTracker</c> on the SysEvents thread. Safe to read from any thread; it never blocks.
/// </summary>
public interface IPointerPositionSource
{
    /// <summary>
    /// The last position outside Clícalo, in physical pixels, or <see langword="null"/> when there was none or its
    /// monitor is gone (the mouse actions then use the centre of the foreground window).
    /// </summary>
    PhysicalPoint? LastExternal { get; }
}
