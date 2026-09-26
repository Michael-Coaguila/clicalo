using System.Runtime.InteropServices;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Tests.Touch;

/// <summary>One step of a <see cref="TouchTrace"/>: a frame, or a timer tick when <see cref="Frame"/> is null.</summary>
/// <param name="At">When it happens.</param>
/// <param name="Frame">The frame to feed; null for a tick.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TraceStep(DateTimeOffset At, PointerFrame? Frame);
