using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;

namespace Clicalo.Application.Tests.Engine;

/// <summary>An injector that injects nothing: it records every call and answers with <see cref="NextStatus"/>.</summary>
internal sealed class FakeInjector : IInputInjector
{
    public List<(InjectedEvent[] Events, int Call)> Batches { get; } = [];

    public List<string> Texts { get; } = [];

    public List<(MouseOp Op, PhysicalPoint? Target)> MouseActions { get; } = [];

    public List<InternalChord> Chords { get; } = [];

    /// <summary>How many times the engine asked to release whatever Windows reports down.</summary>
    public int PressedReleases { get; private set; }

    /// <summary>The status of the next calls; <see cref="InjectionStatus.Sent"/> by default.</summary>
    public InjectionStatus NextStatus { get; set; } = InjectionStatus.Sent;

    public InjectionResult Send(ReadOnlySpan<InjectedEvent> events)
    {
        Batches.Add((events.ToArray(), Batches.Count + 1));
        return Result(events.Length);
    }

    public InjectionResult TypeText(ReadOnlySpan<char> text)
    {
        Texts.Add(text.ToString());
        return Result(text.Length);
    }

    public InjectionResult Mouse(MouseOp operation, PhysicalPoint? target)
    {
        MouseActions.Add((operation, target));
        return Result(1);
    }

    public InjectionResult SendChord(InternalChord chord)
    {
        Chords.Add(chord);
        return Result(4);
    }

    public InjectionResult ReleasePressed()
    {
        PressedReleases++;
        return Result(0);
    }

    private InjectionResult Result(int count) =>
        NextStatus switch
        {
            InjectionStatus.Sent => new InjectionResult(InjectionStatus.Sent, count, 0),
            InjectionStatus.Blocked => new InjectionResult(InjectionStatus.Blocked, 0, 5),
            _ => new InjectionResult(InjectionStatus.Failed, 0, 87),
        };
}
