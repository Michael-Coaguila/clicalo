using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Execution;

/// <summary>Everything the activation policy needs, as values (blueprint §7.2).</summary>
/// <param name="Request">The activation.</param>
/// <param name="Shortcut">The tile's shortcut.</param>
/// <param name="OriginProfile">The profile it was activated from (Frequents runs with its origin profile, FRE-003).</param>
/// <param name="Injection">
/// The mode of the profile in view; General and Frequents inherit the mode of the profile resolved for the foreground
/// app; <see cref="InjectionMode.VirtualKey"/> by default (D24).
/// </param>
/// <param name="LastExternalPointer">Last pointer position outside Clícalo, for mouse actions (EJE-009).</param>
/// <param name="EditMode">Whether the panel is in edit mode.</param>
/// <param name="TestMode">Whether test mode is on (TAC-008).</param>
/// <param name="Elevation">Whether Clícalo may send to the foreground app.</param>
/// <param name="Armed">The armed shortcut, if any.</param>
/// <param name="Filter">The filter state of this tile.</param>
/// <param name="Touch">The touch filter parameters.</param>
/// <param name="Now">The current time.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ActivationContext(
    ActivationRequest Request,
    Shortcut Shortcut,
    ProfileId? OriginProfile,
    InjectionMode Injection,
    PhysicalPoint? LastExternalPointer,
    bool EditMode,
    bool TestMode,
    ElevationState Elevation,
    ArmedConfirmation? Armed,
    ButtonFilterState Filter,
    TouchSettings Touch,
    DateTimeOffset Now
)
{
    /// <summary>How much longer the confirmation window lasts: ×1, ×2 or ×3 (ACC-006).</summary>
    public int TimeMultiplier { get; init; } = 1;
}
