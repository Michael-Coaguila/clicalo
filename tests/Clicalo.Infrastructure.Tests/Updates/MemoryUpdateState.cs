using Clicalo.Infrastructure.Updates;

namespace Clicalo.Infrastructure.Tests.Updates;

/// <summary><see cref="UpdateState"/> in memory.</summary>
internal sealed class MemoryUpdateState : IUpdateStateStore
{
    public UpdateState? State { get; set; }

    public UpdateState? Load() => State;

    public Task SaveAsync(UpdateState state, CancellationToken cancellationToken)
    {
        State = state;
        return Task.CompletedTask;
    }
}
