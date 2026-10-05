namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// The token of a persistence test, cancelled after <see cref="Limit"/> of real time. These tests drive the writer's
/// retries (<c>Timings.Persistence.WriteRetryBackoff</c>) with a <c>FakeTimeProvider</c>: a regression that makes a
/// write fail and retry where it used to succeed would wait for a clock nobody moves, and hang the whole run. With this
/// token the retry wait is cancelled and the test fails on its own, next to the test that names the broken invariant.
/// </summary>
internal sealed class BoundedTestToken : IDisposable
{
    private readonly CancellationTokenSource _source =
        CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

    /// <summary>Starts the limit now.</summary>
    public BoundedTestToken() => _source.CancelAfter(Limit);

    /// <summary>Far above what any persistence test needs (seconds), far below a hung run.</summary>
    public static TimeSpan Limit { get; } = TimeSpan.FromSeconds(30);

    /// <summary>The token to pass to the code under test.</summary>
    public CancellationToken Token => _source.Token;

    /// <inheritdoc />
    public void Dispose() => _source.Dispose();
}
