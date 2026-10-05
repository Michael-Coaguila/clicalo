using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;

namespace Clicalo.Application.Tests.Engine;

/// <summary>The Shell thread and the clipboard, recording what the engine hands them.</summary>
internal sealed class FakeShell : IShellExecutor, IClipboardPaster
{
    public List<(EffectId Effect, LaunchRequest Request, IEngineInbox ReplyTo)> Launches { get; } =
    [];

    public List<(EffectId Effect, SystemCommandId Command)> Commands { get; } = [];

    public List<(EffectId Effect, string Text, IEngineInbox ReplyTo)> Pastes { get; } = [];

    public void Launch(EffectId effect, LaunchRequest request, IEngineInbox replyTo) =>
        Launches.Add((effect, request, replyTo));

    public void Run(EffectId effect, SystemCommandId command, IEngineInbox replyTo) =>
        Commands.Add((effect, command));

    public void Prepare(EffectId effect, ReadOnlySpan<char> text, IEngineInbox replyTo) =>
        Pastes.Add((effect, text.ToString(), replyTo));
}
