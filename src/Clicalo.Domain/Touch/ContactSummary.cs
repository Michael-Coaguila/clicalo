using System.Runtime.InteropServices;

namespace Clicalo.Domain.Touch;

/// <summary>What <see cref="TouchFilter"/> needs to know about a finished contact (blueprint §7.8).</summary>
/// <param name="Duration">From down to up.</param>
/// <param name="MaxDisplacementPx">Largest distance from the down position, in the same pixels as the settings.</param>
/// <param name="PalmLike">True when the contact area (<c>rcContact</c>) was palm-sized at any moment.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ContactSummary(
    TimeSpan Duration,
    double MaxDisplacementPx,
    bool PalmLike
);
