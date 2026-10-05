using Clicalo.App.Lifecycle;
using Clicalo.Application.Ports;

namespace Clicalo.App.Composition;

/// <summary>
/// The Win32 side of the engine the composition wires (blueprint §7.3, ADR-0022): the injector, the internal key
/// effects of the foreground ladder, the guardian and the release of what Windows reports down.
/// <see cref="Resources"/> owns what must be disposed (Sentinel's supervisor).
/// </summary>
/// <param name="Injector">Sends input.</param>
/// <param name="KeyEffects">The internal chords (rights chord, Win+H), through the engine.</param>
/// <param name="Guardian">Sentinel's launcher and watcher.</param>
/// <param name="PressedRelease">The release of the start and of «Soltar todo» in the tray.</param>
/// <param name="Resources">Disposed when the process ends.</param>
internal sealed record EngineAdapterSet(
    IInputInjector Injector,
    IInternalKeyEffects KeyEffects,
    IGuardian Guardian,
    IPressedRelease PressedRelease,
    IDisposable Resources
);
