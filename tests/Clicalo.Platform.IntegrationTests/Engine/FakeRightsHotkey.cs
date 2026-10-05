using Clicalo.Application.Ports;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>The registration of the rights chord, as the internal key effects read it.</summary>
internal sealed class FakeRightsHotkey : IInternalRightsHotkey
{
    public bool IsRegistered { get; set; }

    public ValueTask<bool> WaitForRightsAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);

    public ValueTask<bool> WaitForChordReleaseAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);
}
