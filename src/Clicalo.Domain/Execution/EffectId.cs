namespace Clicalo.Domain.Execution;

/// <summary>Identity of an external effect whose result comes back to the mailbox later (launch, system command, paste).</summary>
/// <param name="Value">Sequence number, unique per engine.</param>
public readonly record struct EffectId(long Value);
