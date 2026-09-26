using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;

namespace Clicalo.UI.Wpf.Pointer;

/// <summary>
/// A contact that <see cref="PointerInputSource"/> reported as down and has not ended yet, with its last position:
/// what it needs to report a <see cref="PointerPhase.Cancel"/> if the contact is lost.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal struct TrackedPointer
{
    /// <summary>The contact identifier.</summary>
    public uint PointerId;

    /// <summary>The device.</summary>
    public PointerKind Kind;

    /// <summary>The last reported position.</summary>
    public PhysicalPoint Position;

    /// <summary>The last reported contact area.</summary>
    public PhysicalRect Contact;

    /// <summary>The origin of the last message.</summary>
    public PointerInputOrigin Origin;
}
