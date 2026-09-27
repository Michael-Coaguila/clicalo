using Clicalo.App.Lifecycle;
using Clicalo.Application.Ports;

namespace Clicalo.App.Composition;

/// <summary>
/// The Win32 side of the engine the composition wires (blueprint §7.3, ADR-0004): the injector and the physical
/// ledger behind the generation fence, the internal key effects of the foreground ladder, the guardian and the
/// preventive release of the start. <see cref="Resources"/> owns the unmanaged pieces (the ledger section, the pipe).
/// </summary>
/// <param name="Injector">Sends input through the injection gate.</param>
/// <param name="Ledger">The engine's view of the physical ledger.</param>
/// <param name="KeyEffects">The internal chords (rights chord, Win+H), through the ledger.</param>
/// <param name="Guardian">Sentinel's launcher and watcher.</param>
/// <param name="StartupRelease">The preventive release of the start.</param>
/// <param name="Resources">Disposed when the process ends.</param>
internal sealed record EngineAdapterSet(
    IInputInjector Injector,
    IKeyLedger Ledger,
    IInternalKeyEffects KeyEffects,
    IGuardian Guardian,
    IStartupRelease StartupRelease,
    IDisposable Resources
);
