using Clicalo.Platform.Core.KeyLedger;
using Clicalo.Platform.Windows.SentinelHost;

namespace Clicalo.App.Composition;

/// <summary>What a sending start owns: Sentinel's supervisor first, then the ledger section.</summary>
/// <param name="supervisor">Sentinel's supervisor.</param>
/// <param name="section">The engine's ledger section.</param>
internal sealed class SendingResources(SentinelSupervisor supervisor, KeyLedgerSection section)
    : IDisposable
{
    /// <inheritdoc />
    public void Dispose()
    {
        supervisor.Dispose();
        section.Dispose();
    }
}
