using System.Collections.Concurrent;
using Clicalo.Application.Ports;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// The test side of <c>ForegroundOrchestrator</c> for <c>ActivationGuard</c> (M1-ownership: windowing has its own
/// arbiter): windows can be leased by hand, every reported violation is recorded, and <see cref="Restore"/>, when set,
/// runs on the thread pool, as the orchestrator restores on its own thread and never inside the window procedure.
/// </summary>
public sealed class RecordingArbiter : IActivationArbiter
{
    private readonly ConcurrentDictionary<nint, bool> _leased = new();
    private readonly ConcurrentQueue<ActivationViolation> _violations = new();

    /// <summary>What to do after a violation: the desktop tests give the foreground back to InputProbe.</summary>
    public Func<ActivationViolation, Task>? Restore { get; set; }

    /// <summary>Every violation reported so far, in order.</summary>
    public IReadOnlyList<ActivationViolation> Violations => [.. _violations];

    /// <summary>Lets <paramref name="window"/> activate, as a granted lease would.</summary>
    public void Lease(WindowToken window) => _leased[window.Handle] = true;

    /// <summary>Ends the lease of <paramref name="window"/>.</summary>
    public void EndLease(WindowToken window) => _leased.TryRemove(window.Handle, out _);

    /// <inheritdoc />
    public bool IsActivationLeased(WindowToken window) => _leased.ContainsKey(window.Handle);

    /// <inheritdoc />
    public void ReportViolation(ActivationViolation violation)
    {
        _violations.Enqueue(violation);
        if (Restore is { } restore)
        {
            _ = Task.Run(() => restore(violation));
        }
    }
}
