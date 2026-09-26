using Clicalo.Platform.Core.Injection;

namespace Clicalo.Sentinel.Tests;

/// <summary>An <see cref="ILowLevelSender"/> that sends nothing: it records every batch (the tests never inject).</summary>
internal sealed class RecordingSender : ILowLevelSender
{
    public List<LowLevelInput[]> Batches { get; } = [];

    public IEnumerable<LowLevelInput> Sent => Batches.SelectMany(static b => b);

    public SendResult Send(ReadOnlySpan<LowLevelInput> inputs)
    {
        Batches.Add(inputs.ToArray());
        return new SendResult(inputs.Length, 0);
    }
}
