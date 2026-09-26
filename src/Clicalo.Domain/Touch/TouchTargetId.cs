using System.Runtime.InteropServices;

namespace Clicalo.Domain.Touch;

/// <summary>
/// Identifies a <see cref="TouchTarget"/> within one surface. The surface assigns it and maps it back to its control
/// when a <see cref="GestureEvent"/> arrives.
/// </summary>
/// <param name="Value">Surface-local identifier.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TouchTargetId(int Value);
