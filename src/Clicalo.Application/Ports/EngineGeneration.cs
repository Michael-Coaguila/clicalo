namespace Clicalo.Application.Ports;

/// <summary>
/// The generation of the engine (blueprint §3.2, rule 6): every engine host is born with the current one, and every
/// external effect runs through the injection gate with it. After an emergency release the ledger's generation goes
/// up, so a zombie engine thread gets <see cref="InjectionStatus.Fenced"/> and sends nothing (INV-11).
/// </summary>
/// <param name="Value">The generation stored in the physical ledger; it only goes up.</param>
public readonly record struct EngineGeneration(ulong Value);
