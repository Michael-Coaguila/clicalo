using Clicalo.Domain.Geometry;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Execution;

/// <summary>
/// Where and how an execution started, carried by every effect it produces later (the steps of a Tap, a macro, the
/// Ctrl+V of a paste): the foreground epoch and window it was planned for (INV-6), the mode of its profile (D24) and
/// the pointer position for mouse actions (EJE-009).
/// </summary>
/// <param name="Shortcut">The shortcut.</param>
/// <param name="Epoch">Foreground epoch when it was activated.</param>
/// <param name="RequiredForeground">The window that must still be in front, if any.</param>
/// <param name="Injection">The injection mode.</param>
/// <param name="ExternalPointer">Last pointer position outside Clícalo, or <see langword="null"/>.</param>
/// <param name="At">When it was activated.</param>
/// <param name="Trial">Whether it is a «Probar ahora» run, which leaves Frecuentes and Repetir alone (PRB-006).</param>
public sealed record ExecutionOrigin(
    ShortcutId Shortcut,
    long Epoch,
    ForegroundWindowId? RequiredForeground,
    InjectionMode Injection,
    PhysicalPoint? ExternalPointer,
    DateTimeOffset At,
    bool Trial = false
);
