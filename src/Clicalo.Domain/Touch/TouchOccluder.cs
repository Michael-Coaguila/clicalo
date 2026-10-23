using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;

namespace Clicalo.Domain.Touch;

/// <summary>
/// A layer of a surface drawn over other controls (a card of a notice, the tile menu, the profile grid): a contact
/// inside it reaches only the controls of the layer, never one under it by its extra hit area (TAC-002, D21 review: a
/// touch on the top edge of a card sent the tile above it).
/// </summary>
/// <param name="Bounds">The drawn layer, in physical screen pixels.</param>
/// <param name="Own">The targets that belong to the layer: they keep their extra hit area inside it.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TouchOccluder(PhysicalRect Bounds, ImmutableArray<TouchTargetId> Own);
