using System.Runtime.InteropServices;

namespace Clicalo.Domain.Timing;

/// <summary>
/// A number of occurrences within a sliding time window, such as «3 crashes in 10 minutes»
/// (<c>Timings.App.CrashLoop</c>). Generated from <c>data/catalogs/timings.json</c>.
/// </summary>
/// <param name="Count">Occurrences that reach the threshold.</param>
/// <param name="Window">Length of the sliding window.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct CountWindow(int Count, TimeSpan Window);
