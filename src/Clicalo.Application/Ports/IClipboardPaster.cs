using Clicalo.Domain.Execution;

namespace Clicalo.Application.Ports;

/// <summary>
/// Prepares a paste on the SysEvents thread (OLE STA, blueprint §7.7, EJE-008): captures every clipboard format, puts
/// the text with the formats that keep it out of the history and the cloud, posts
/// <see cref="EngineEvent.ClipboardReady"/>, and restores the original after <c>Timings.Injection.ClipboardRestoreDelay</c>
/// only if the clipboard sequence number is still its own.
/// </summary>
public interface IClipboardPaster
{
    /// <summary>Queues the paste; the adapter copies <paramref name="text"/> before returning.</summary>
    /// <param name="effect">The effect id.</param>
    /// <param name="text">The text, in a buffer the caller wipes afterwards.</param>
    /// <param name="replyTo">Where <see cref="EngineEvent.ClipboardReady"/> goes.</param>
    void Prepare(EffectId effect, ReadOnlySpan<char> text, IEngineInbox replyTo);
}
