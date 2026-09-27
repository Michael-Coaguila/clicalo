using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;

namespace Clicalo.Application.Tests.Engine;

/// <summary>An injector that injects nothing: it records every call and answers with <see cref="NextStatus"/>.</summary>
internal sealed class FakeInjector : IInputInjector
{
    public List<(EngineGeneration Generation, InjectedEvent[] Events)> Batches { get; } = [];

    public List<string> Texts { get; } = [];

    public List<(MouseOp Op, PhysicalPoint? Target)> MouseActions { get; } = [];

    /// <summary>The status of the next calls; <see cref="InjectionStatus.Sent"/> by default.</summary>
    public InjectionStatus NextStatus { get; set; } = InjectionStatus.Sent;

    public InjectionResult Send(EngineGeneration generation, ReadOnlySpan<InjectedEvent> events)
    {
        Batches.Add((generation, events.ToArray()));
        return Result(events.Length);
    }

    public InjectionResult TypeText(EngineGeneration generation, ReadOnlySpan<char> text)
    {
        Texts.Add(text.ToString());
        return Result(text.Length);
    }

    public InjectionResult Mouse(
        EngineGeneration generation,
        MouseOp operation,
        PhysicalPoint? target
    )
    {
        MouseActions.Add((operation, target));
        return Result(1);
    }

    /// <summary>How many times the engine asked to send the physical ledger's pending releases again.</summary>
    public int PendingReleaseRequests { get; private set; }

    public InjectionResult ReleasePending(EngineGeneration generation)
    {
        PendingReleaseRequests++;
        return Result(0);
    }

    private InjectionResult Result(int count) =>
        NextStatus switch
        {
            InjectionStatus.Sent => new InjectionResult(InjectionStatus.Sent, count, 0),
            InjectionStatus.Fenced => new InjectionResult(InjectionStatus.Fenced, 0, 0),
            InjectionStatus.Blocked => new InjectionResult(InjectionStatus.Blocked, 0, 5),
            _ => new InjectionResult(InjectionStatus.Failed, 0, 87),
        };
}
