using System.Globalization;
using System.Runtime;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The runtime's own counters that can stretch a tap: garbage collections per generation, total GC pause, and methods
/// compiled by the JIT with their compilation time (process wide).
/// </summary>
internal sealed record RuntimeCounters(
    int Gen0,
    int Gen1,
    int Gen2,
    TimeSpan GcPause,
    long JitMethods,
    TimeSpan JitTime
)
{
    /// <summary>The counters now.</summary>
    public static RuntimeCounters Read() =>
        new(
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            GC.GetTotalPauseDuration(),
            JitInfo.GetCompiledMethodCount(currentThread: false),
            JitInfo.GetCompilationTime(currentThread: false)
        );

    /// <summary>What changed since <paramref name="earlier"/>.</summary>
    public RuntimeCounters Minus(RuntimeCounters earlier) =>
        new(
            Gen0 - earlier.Gen0,
            Gen1 - earlier.Gen1,
            Gen2 - earlier.Gen2,
            GcPause - earlier.GcPause,
            JitMethods - earlier.JitMethods,
            JitTime - earlier.JitTime
        );

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"gc {Gen0}/{Gen1}/{Gen2} pause {GcPause.TotalMilliseconds:0.00} ms; jit {JitMethods} methods {JitTime.TotalMilliseconds:0.00} ms"
        );
}
