using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;

namespace Clicalo.App.Composition;

/// <summary>
/// The injector of <c>--no-input</c> (development and measurement on a machine whose keyboard must not be touched):
/// it accepts every batch and sends nothing, so the engine runs its whole plan while no key reaches any app. Counts
/// what it would have sent, for diagnostics.
/// </summary>
internal sealed class DryRunInputInjector : IInputInjector
{
    private long _events;

    /// <summary>The events the engine asked to send.</summary>
    public long Events => Interlocked.Read(ref _events);

    /// <inheritdoc />
    public InjectionResult Send(ReadOnlySpan<InjectedEvent> events) => Accept(events.Length);

    /// <inheritdoc />
    public InjectionResult TypeText(ReadOnlySpan<char> text) => Accept(text.Length * 2);

    /// <inheritdoc />
    public InjectionResult Mouse(MouseOp operation, PhysicalPoint? target) => Accept(1);

    /// <inheritdoc />
    public InjectionResult SendChord(InternalChord chord) => Accept(0);

    /// <inheritdoc />
    public InjectionResult ReleasePressed() => Accept(0);

    private InjectionResult Accept(int count)
    {
        _ = Interlocked.Add(ref _events, count);
        return new InjectionResult(InjectionStatus.Sent, count, 0);
    }
}
