using System.Collections.Concurrent;
using Clicalo.Application.Ports;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// The arbiter of the pointer test surfaces: no lease is ever granted (touching a surface must never need one), and
/// every violation is kept so a test can show that none happened.
/// </summary>
public sealed class PointerTestArbiter : IActivationArbiter
{
    private readonly ConcurrentQueue<ActivationViolation> _violations = new();

    public IReadOnlyList<ActivationViolation> Violations => [.. _violations];

    public bool IsActivationLeased(WindowToken window) => false;

    public void ReportViolation(ActivationViolation violation) => _violations.Enqueue(violation);
}
