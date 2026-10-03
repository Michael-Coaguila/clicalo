using Clicalo.Platform.Core.Injection;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// An <see cref="ILowLevelSender"/> that sends nothing and answers like a desktop that refuses input for a while: each
/// queued answer is used once, then every batch is accepted whole. It records every batch (the tests never inject).
/// </summary>
internal sealed class RefusingSender : ILowLevelSender
{
    /// <summary><c>ERROR_ACCESS_DENIED</c>: the secure desktop is in front.</summary>
    public const int AccessDenied = 5;

    private readonly Queue<Func<int, SendResult>> _answers = new();

    public List<LowLevelInput[]> Batches { get; } = [];

    /// <summary>Where each send is written, to check its order against the relaunch.</summary>
    public List<string>? Log { get; set; }

    /// <summary>The secure desktop refuses the next <paramref name="times"/> sends whole.</summary>
    public RefusingSender LockedFor(int times) =>
        Refuse(times, static _ => new SendResult(0, AccessDenied));

    /// <summary>The next <paramref name="times"/> sends accept nothing with <paramref name="error"/>.</summary>
    public RefusingSender RefuseWith(int error, int times) =>
        Refuse(times, _ => new SendResult(0, error));

    /// <summary>The next send accepts only its first <paramref name="accepted"/> events.</summary>
    public RefusingSender AcceptOnly(int accepted) =>
        Refuse(1, length => new SendResult(Math.Min(accepted, length), 0));

    public SendResult Send(ReadOnlySpan<LowLevelInput> inputs)
    {
        Batches.Add(inputs.ToArray());
        var result = _answers.TryDequeue(out var answer)
            ? answer(inputs.Length)
            : new SendResult(inputs.Length, 0);
        Log?.Add(result.Sent == inputs.Length ? "accepted" : "refused");
        return result;
    }

    private RefusingSender Refuse(int times, Func<int, SendResult> answer)
    {
        for (var i = 0; i < times; i++)
        {
            _answers.Enqueue(answer);
        }

        return this;
    }
}
