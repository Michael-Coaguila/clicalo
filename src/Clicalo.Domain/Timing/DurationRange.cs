using System.Runtime.InteropServices;

namespace Clicalo.Domain.Timing;

/// <summary>
/// Inclusive range of durations with the step used by − and + controls, such as the macro wait
/// (<c>Timings.Macro.MacroWaitRange</c>). Generated from <c>data/catalogs/timings.json</c>.
/// </summary>
/// <param name="Min">Shortest allowed duration.</param>
/// <param name="Max">Longest allowed duration.</param>
/// <param name="Step">Increment of the − and + controls.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DurationRange(TimeSpan Min, TimeSpan Max, TimeSpan Step);
