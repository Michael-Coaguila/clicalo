using Clicalo.Application.Engine;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;

namespace Clicalo.App.Composition;

/// <summary>
/// The engine mailbox every other piece holds (the panel, the tray, the foreground, the exit): it forwards to the
/// engine host that runs now. After an emergency release the generation goes up and a new host takes over
/// (blueprint §3.2 rule 6); switching <see cref="Target"/> is then the only change, and nothing keeps posting to the
/// fenced zombie. Before the first host and after the last one, events are refused (<see cref="Post"/> is false).
/// </summary>
internal sealed class EngineInboxRelay : IEngineInbox
{
    private EngineHost? _target;

    /// <summary>The engine host that runs now.</summary>
    public EngineHost? Target
    {
        get => Volatile.Read(ref _target);
        set => Volatile.Write(ref _target, value);
    }

    /// <inheritdoc />
    public bool Post(EngineEvent engineEvent) => Target?.Post(engineEvent) ?? false;
}
