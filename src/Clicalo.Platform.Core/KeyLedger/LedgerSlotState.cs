namespace Clicalo.Platform.Core.KeyLedger;

/// <summary>State of a ledger slot (blueprint §7.4).</summary>
public enum LedgerSlotState : byte
{
    /// <summary>Unused.</summary>
    Free = 0,

    /// <summary>Recorded before <c>SendInput(down)</c>: if the process dies now, one extra release is sent (harmless thanks to the menu mask).</summary>
    DownPending = 1,

    /// <summary>The key is down.</summary>
    Down = 2,

    /// <summary>The release could not be sent (secure desktop); retried on unlock or resume.</summary>
    ReleasePending = 3,
}
