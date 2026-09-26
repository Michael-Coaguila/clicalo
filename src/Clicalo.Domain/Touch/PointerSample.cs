using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;

namespace Clicalo.Domain.Touch;

/// <summary>
/// One contact at one instant, translated from a <c>WM_POINTER*</c> message by <c>PointerInputSource</c>
/// (UI.Wpf.Pointer) and consumed by <see cref="GestureRecognizer"/> (blueprint §7.8, ADR-0006). Immutable and free of
/// Win32 types, so recorded traces (<c>tests/fixtures/pointer/</c>) replay the same way on any machine.
/// </summary>
/// <param name="PointerId">
/// Contact identifier (<c>GET_POINTERID_WPARAM</c>). Unique among the contacts that are down at the same time;
/// Windows reuses it after the contact ends.
/// </param>
/// <param name="Kind">Finger, pen or mouse.</param>
/// <param name="Phase">Down, move, up or cancel.</param>
/// <param name="Position">Hot spot of the contact, in physical screen pixels (<c>ptPixelLocation</c>).</param>
/// <param name="Contact">
/// Contact area in physical screen pixels (<c>POINTER_TOUCH_INFO.rcContact</c>), used to detect a palm (ACC-007).
/// For a pen or a mouse it is the 1×1 rectangle at <paramref name="Position"/>.
/// </param>
/// <param name="Timestamp">
/// When the sample happened, on the <see cref="TimeProvider"/> timeline of the recognizer, so the recognizer never
/// reads a clock itself.
/// </param>
/// <param name="Origin">Hardware or injected (<c>GetCurrentInputMessageSource</c>).</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PointerSample(
    uint PointerId,
    PointerKind Kind,
    PointerPhase Phase,
    PhysicalPoint Position,
    PhysicalRect Contact,
    DateTimeOffset Timestamp,
    PointerInputOrigin Origin
);
