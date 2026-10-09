using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Platform.IntegrationTests.Desktop;
using Clicalo.Platform.Windows.Clipboard;
using Clicalo.Platform.Windows.PointerTracking;
using Clicalo.Platform.Windows.SysEvents;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Platform.IntegrationTests.Actions;

/// <summary>
/// The adapters of the actions against the real session: the paste puts the text and gives the clipboard back
/// (EJE-008), and the pointer tracker knows where the cursor is outside Clícalo (EJE-009). Nothing is injected and no
/// window is started; the clipboard of the session ends as it was.
/// </summary>
[Trait("Requires", "Desktop")]
[Collection(DesktopCollectionDefinition.Name)]
public sealed class DesktopActionAdapterTests : IDisposable
{
    private readonly SysEventsThread _thread = SysEventsThread.Start();

    public void Dispose() => _thread.Dispose();

    [DesktopFact]
    [Trait("Req", "EJE-008")]
    public async Task A_paste_puts_the_text_then_gives_the_clipboard_back()
    {
        var time = new FakeTimeProvider();
        using var paster = new ClipboardPaster(_thread, time);
        var before = await _thread.InvokeAsync(paster.ReadText);
        var inbox = new ReadyInbox();

        paster.Prepare(new EffectId(1), "Texto de prueba 123", inbox);
        await inbox.Ready.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken
        );
        var during = await _thread.InvokeAsync(paster.ReadText);
        time.Advance(TimeSpan.FromMilliseconds(600));
        var after = await Eventually(() => _thread.InvokeAsync(paster.ReadText), before);

        during.ShouldBe("Texto de prueba 123");
        after.ShouldBe(before);
    }

    [DesktopFact]
    [Trait("Req", "EJE-009")]
    public async Task The_tracker_knows_the_cursor_position_outside_clicalo()
    {
        using var tracker = new PointerPositionTracker(_thread);

        await _thread.InvokeAsync(() => { });

        tracker.LastExternal.ShouldNotBeNull();
    }

    private static async Task<string?> Eventually(Func<Task<string?>> read, string? expected)
    {
        string? value = null;
        for (var i = 0; i < 50; i++)
        {
            value = await read();
            if (string.Equals(value, expected, StringComparison.Ordinal))
            {
                return value;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), TimeProvider.System);
        }

        return value;
    }

    private sealed class ReadyInbox : IEngineInbox
    {
        public TaskCompletionSource<EngineEvent> Ready { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Post(EngineEvent engineEvent) => Ready.TrySetResult(engineEvent);
    }
}
