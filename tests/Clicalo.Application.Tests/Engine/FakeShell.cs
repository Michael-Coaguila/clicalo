using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;

namespace Clicalo.Application.Tests.Engine;

/// <summary>The Shell thread and the clipboard, recording what the engine hands them.</summary>
internal sealed class FakeShell : IShellExecutor, IClipboardPaster
{
    public List<(
        EngineGeneration Generation,
        EffectId Effect,
        LaunchRequest Request,
        IEngineInbox ReplyTo
    )> Launches { get; } = [];

    public List<(
        EngineGeneration Generation,
        EffectId Effect,
        SystemCommandId Command
    )> Commands { get; } = [];

    public List<(EffectId Effect, string Text, IEngineInbox ReplyTo)> Pastes { get; } = [];

    public void Launch(
        EngineGeneration generation,
        EffectId effect,
        LaunchRequest request,
        IEngineInbox replyTo
    ) => Launches.Add((generation, effect, request, replyTo));

    public void Run(
        EngineGeneration generation,
        EffectId effect,
        SystemCommandId command,
        IEngineInbox replyTo
    ) => Commands.Add((generation, effect, command));

    public void Prepare(
        EngineGeneration generation,
        EffectId effect,
        ReadOnlySpan<char> text,
        IEngineInbox replyTo
    ) => Pastes.Add((effect, text.ToString(), replyTo));
}
