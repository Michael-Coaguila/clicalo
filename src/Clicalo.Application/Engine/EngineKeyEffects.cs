using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.Engine;

/// <summary>
/// The internal key effects of a sending start (blueprint §3.6, D-14, D-22): the rights chord of the foreground ladder
/// and Win+H are requests to the engine, which sends them under the gate with its own generation (INV-11), so a fenced
/// engine never sends them and no other thread injects beside the engine's. The answer is awaited for at most
/// <c>Timings.Engine.InternalChordWait</c>: a hung or replaced engine answers «not sent».
/// </summary>
public sealed class EngineKeyEffects : IInternalKeyEffects
{
    private readonly IEngineInbox _engine;
    private readonly InternalChordReplies _replies;
    private readonly IInternalRightsHotkey _hotkey;
    private readonly TimeProvider _time;

    /// <summary>Creates the effects.</summary>
    /// <param name="engine">The mailbox of the engine that runs now.</param>
    /// <param name="replies">Where the engine answers (the same instance as <see cref="EngineHostPorts.ChordReplies"/>).</param>
    /// <param name="hotkey">The registration of the rights chord: an unregistered chord is never sent.</param>
    /// <param name="time">The clock of the wait.</param>
    public EngineKeyEffects(
        IEngineInbox engine,
        InternalChordReplies replies,
        IInternalRightsHotkey hotkey,
        TimeProvider time
    )
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(replies);
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentNullException.ThrowIfNull(time);
        _engine = engine;
        _replies = replies;
        _hotkey = hotkey;
        _time = time;
    }

    /// <inheritdoc />
    public ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken) =>
        _hotkey.IsRegistered
            ? SendAsync(InternalChord.Rights, cancellationToken)
            : ValueTask.FromResult(false);

    /// <inheritdoc />
    public ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken) =>
        SendAsync(InternalChord.Dictation, cancellationToken);

    private async ValueTask<bool> SendAsync(
        InternalChord chord,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (request, reply) = _replies.Register();
        try
        {
            if (!_engine.Post(new EngineEvent.InternalChordRequested(chord, request)))
            {
                return false;
            }

            return await reply
                .WaitAsync(Timings.Engine.InternalChordWait, _time, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            _replies.Forget(request);
        }
    }
}
