using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;

namespace Clicalo.App.Composition;

/// <summary>
/// The clipboard of M2: pasting a Text action (EJE-008, <c>Platform.Windows/Clipboard</c> on the SysEvents thread)
/// arrives in M3. The text is never copied or kept here; the engine hears nothing back and its paste times out as any
/// paste that does not get ready.
/// </summary>
internal sealed class DeferredClipboardPaster : IClipboardPaster
{
    /// <inheritdoc />
    public void Prepare(
        EngineGeneration generation,
        EffectId effect,
        ReadOnlySpan<char> text,
        IEngineInbox replyTo
    ) { }
}
