using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;

namespace Clicalo.Platform.Windows.Foreground;

/// <summary>
/// The single adapter of <see cref="IForegroundControl"/> and the ONLY file allowed to call
/// <c>SetForegroundWindow</c> and <c>AllowSetForegroundWindow</c> (banned-api-exceptions.json: foreground-control;
/// blueprint §3.6, §4.4). No <c>AttachThreadInput</c>, no synthetic Alt and no <c>LockSetForegroundWindow</c>.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the foreground package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class ForegroundControl : IForegroundControl
{
    /// <inheritdoc />
    public bool TrySetForeground(WindowToken window) =>
        throw new NotImplementedException("M1 foreground package.");

    /// <inheritdoc />
    public WindowToken GetForeground() =>
        throw new NotImplementedException("M1 foreground package.");

    /// <inheritdoc />
    public void FlashTaskbar(WindowToken window) =>
        throw new NotImplementedException("M1 foreground package.");
}
