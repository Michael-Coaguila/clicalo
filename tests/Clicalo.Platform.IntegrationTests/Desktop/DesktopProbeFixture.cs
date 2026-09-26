using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Platform.IntegrationTests.Desktop;

/// <summary>
/// One InputProbe for the desktop collection, started only when desktop tests are enabled, plus the guarded
/// injector bound to it.
/// </summary>
public sealed class DesktopProbeFixture : IAsyncLifetime
{
    /// <summary>How long a test waits for the probe to reach the foreground before failing with a diagnostic.</summary>
    public static readonly TimeSpan ForegroundTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How long a test waits for the events it expects from one batch.</summary>
    public static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private InputProbeSession? _probe;
    private TestKeyboardInjector? _injector;

    /// <summary>The shared probe.</summary>
    public InputProbeSession Probe =>
        _probe ?? throw new InvalidOperationException(DesktopTestEnvironment.SkipReason);

    /// <summary>The injector bound to <see cref="Probe"/>: it never sends input anywhere else.</summary>
    public TestKeyboardInjector Injector =>
        _injector ?? throw new InvalidOperationException(DesktopTestEnvironment.SkipReason);

    public async ValueTask InitializeAsync()
    {
        if (!DesktopTestEnvironment.IsEnabled)
        {
            return;
        }

        _probe = await InputProbeSession.StartAsync(TestContext.Current.CancellationToken);
        _injector = new TestKeyboardInjector(_probe);
    }

    /// <summary>
    /// Makes sure the probe owns the foreground (failing with a diagnostic otherwise) and returns the cursor from
    /// which the calling test's events start.
    /// </summary>
    public async Task<int> PrepareAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await Probe.EnsureForegroundAsync(ForegroundTimeout, cancellationToken);
        await Probe.PingAsync(EventTimeout, cancellationToken);
        return Probe.Cursor;
    }

    /// <summary>
    /// Waits for the events a test expects after <paramref name="cursor"/>, plus the settle period. When nothing
    /// reached the probe, not even Raw Input, the failure says so: the input was consumed before any application saw
    /// it, which in practice means a low-level keyboard hook of another program.
    /// </summary>
    public async Task<IReadOnlyList<ProbeEvent>> CollectAsync(
        int cursor,
        Func<IReadOnlyList<ProbeEvent>, bool> isComplete
    )
    {
        try
        {
            return await Probe.CollectAsync(
                cursor,
                isComplete,
                EventTimeout,
                TestContext.Current.CancellationToken
            );
        }
        catch (TimeoutException ex)
            when (ProbeEvents.InjectedRaw(Probe.EventsSince(cursor)).Count == 0)
        {
            throw new TimeoutException(
                "The injected keys never reached the probe, not even as Raw Input, although it owned the foreground: "
                    + "they were consumed before any application could see them. The usual cause is a low-level "
                    + "keyboard hook of another running program (dictation and macro tools often reserve Right Alt or "
                    + "Right Ctrl). "
                    + ex.Message,
                ex
            );
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_probe is not null)
        {
            await _probe.DisposeAsync();
        }
    }
}
