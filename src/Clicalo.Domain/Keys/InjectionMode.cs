namespace Clicalo.Domain.Keys;

/// <summary>
/// How keys are sent (blueprint §7.7, D24). Chosen per profile («compatible mode», persisted as <c>compat</c>) and
/// carried by every press to the ledger, so each release uses the same mode (INV-12).
/// </summary>
public enum InjectionMode : byte
{
    /// <summary>Virtual key resolved with the foreground layout; the scan code is informative.</summary>
    VirtualKey,

    /// <summary>Scan code only (games, remote desktop, virtual machines).</summary>
    ScanCode,
}
