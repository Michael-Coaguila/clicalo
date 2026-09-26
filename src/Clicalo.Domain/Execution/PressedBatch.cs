using System.Collections.Immutable;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;

namespace Clicalo.Domain.Execution;

/// <summary>
/// What a sent batch pressed, kept for a while so that, if <c>SendInput</c> took only part of it
/// (<see cref="EngineEvent.InjectFailed"/>), the engine can release whatever the batch may have left down (INV-5).
/// </summary>
/// <param name="Holder">The holder the batch pressed for.</param>
/// <param name="Keys">The keys the batch pressed or released (a Tap batch releases too).</param>
/// <param name="Buttons">The mouse buttons the batch pressed or released.</param>
public sealed record PressedBatch(
    HolderId Holder,
    ImmutableArray<InjectedKey> Keys,
    MouseButtons Buttons
);
