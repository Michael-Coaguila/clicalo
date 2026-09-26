using System.Runtime.InteropServices;

namespace Clicalo.Domain.Migration.V1;

/// <summary>A v1 pair of integers (<c>window_pos</c>, <c>window_size</c>, <c>edit_size</c>, <c>button_size</c>).</summary>
/// <param name="First">x or width.</param>
/// <param name="Second">y or height.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct V1Pair(int First, int Second);
