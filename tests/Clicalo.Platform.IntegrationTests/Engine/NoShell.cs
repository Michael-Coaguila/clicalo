using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>Shell and clipboard ports of an engine host in scenarios that never launch nor paste.</summary>
internal sealed class NoShell : IShellExecutor, IClipboardPaster
{
    public static NoShell Instance { get; } = new();

    public void Launch(
        EngineGeneration generation,
        EffectId effect,
        LaunchRequest request,
        IEngineInbox replyTo
    ) => throw new InvalidOperationException("No launch in this scenario.");

    public void Run(
        EngineGeneration generation,
        EffectId effect,
        SystemCommandId command,
        IEngineInbox replyTo
    ) => throw new InvalidOperationException("No system command in this scenario.");

    public void Prepare(
        EngineGeneration generation,
        EffectId effect,
        ReadOnlySpan<char> text,
        IEngineInbox replyTo
    ) => throw new InvalidOperationException("No paste in this scenario.");
}
