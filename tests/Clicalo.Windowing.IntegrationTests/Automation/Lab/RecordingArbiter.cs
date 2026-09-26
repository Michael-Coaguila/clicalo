using System.Collections.Concurrent;
using Clicalo.Application.Ports;

namespace Clicalo.Windowing.IntegrationTests.Automation.Lab;

/// <summary>
/// The automation package's own <see cref="IActivationArbiter"/>: no lease is ever active (nothing in S3 may activate
/// a surface) and every reported violation is kept for the assertions.
/// </summary>
public sealed class RecordingArbiter : IActivationArbiter
{
    private readonly ConcurrentQueue<ActivationViolation> _violations = new();

    /// <summary>The violations reported so far.</summary>
    public IReadOnlyCollection<ActivationViolation> Violations => [.. _violations];

    /// <inheritdoc />
    public bool IsActivationLeased(WindowToken window) => false;

    /// <inheritdoc />
    public void ReportViolation(ActivationViolation violation) => _violations.Enqueue(violation);
}
