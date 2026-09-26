namespace Clicalo.Application.Ports;

/// <summary>What happened to a batch sent through <see cref="IInputInjector"/>.</summary>
public enum InjectionStatus
{
    /// <summary>Every event was sent and the ledger committed.</summary>
    Sent,

    /// <summary>The generation is old: nothing was sent (INV-11). The effect is dropped.</summary>
    Fenced,

    /// <summary><c>SendInput</c> sent fewer events than asked; the ledger keeps what is down.</summary>
    Failed,

    /// <summary>The secure desktop is in front (locked session): releases stay pending and are retried on unlock (INV-3).</summary>
    Blocked,
}
