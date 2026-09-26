using Clicalo.Application.Ports;

namespace Clicalo.Application.Foreground;

/// <summary>
/// The single owner of every foreground change (blueprint §3.6, ADR-0005): typed leases with verified restoration
/// and a rights ladder per origin. Implemented by <c>ForegroundOrchestrator</c>, an actor whose work is serialized
/// (it runs on the SysEvents thread in the app); it is the only user of <see cref="IForegroundControl"/>.
/// </summary>
public interface IForegroundOrchestrator
{
    /// <summary>The last verified EXTERNAL foreground and its epoch.</summary>
    ForegroundSnapshot Current { get; }

    /// <summary>
    /// Brings <see cref="LeaseRequest.Target"/> to the foreground following the ladder of
    /// <see cref="LeaseRequest.Origin"/> and returns the lease, or the reason for the denial. A request replaces
    /// the active lease (inheriting its previous foreground), except that nothing replaces
    /// <see cref="LeaseKind.TrayMenu"/>.
    /// </summary>
    ValueTask<LeaseResult> AcquireAsync(LeaseRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// After a REG-01 violation, gives the foreground back to <paramref name="expected"/> (normally
    /// <see cref="Current"/>) with verification, within <c>Timings.Windowing.ViolationRestoreBudget</c>.
    /// </summary>
    ValueTask RestoreAfterViolationAsync(WindowToken expected, CancellationToken cancellationToken);
}
