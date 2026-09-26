using System.Runtime.InteropServices;
using Clicalo.Platform.Windows.SysEvents;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// The SysEvents thread (blueprint §3.1, §3.2): an STA thread with a message-only window and a message loop that runs
/// posted work in order, routes messages to handlers and survives failing work. Headless: no input, no foreground.
/// </summary>
public sealed class SysEventsThreadTests : IDisposable
{
    private const uint TestMessage = 0x8000 + 0x100;

    private readonly SysEventsThread _thread = SysEventsThread.Start();

    public void Dispose() => _thread.Dispose();

    [Fact]
    public async Task Work_runs_on_the_SysEvents_thread_in_the_order_it_was_posted()
    {
        var order = new List<int>();
        var apartments = new List<ApartmentState>();

        for (var i = 0; i < 5; i++)
        {
            var index = i;
            _thread.Post(() =>
            {
                order.Add(index);
                apartments.Add(Thread.CurrentThread.GetApartmentState());
            });
        }

        var onThread = await _thread.InvokeAsync(_thread.CheckAccess);

        onThread.ShouldBeTrue();
        _thread.CheckAccess().ShouldBeFalse();
        order.ShouldBe([0, 1, 2, 3, 4]);
        apartments.ShouldAllBe(apartment => apartment == ApartmentState.STA);
        _thread.MessageWindow.ShouldNotBe(0);
    }

    [Fact]
    public async Task InvokeAsync_returns_the_result_or_the_exception_of_the_work()
    {
        (await _thread.InvokeAsync(() => 42)).ShouldBe(42);

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            _thread.InvokeAsync<int>(() => throw new InvalidOperationException("boom"))
        );
        failure.Message.ShouldBe("boom");
    }

    [Fact]
    public async Task A_message_reaches_its_handler_until_the_registration_is_disposed()
    {
        var received = new List<(nint, nint)>();
        var registration = _thread.AddMessageHandler(
            TestMessage,
            (wParam, lParam) =>
            {
                received.Add((wParam, lParam));
                return true;
            }
        );

        PostMessageW(_thread.MessageWindow, TestMessage, 7, 11).ShouldBeTrue();
        await _thread.InvokeAsync(() => { });
        registration.Dispose();
        PostMessageW(_thread.MessageWindow, TestMessage, 8, 12).ShouldBeTrue();
        await _thread.InvokeAsync(() => { });

        received.ShouldBe([(7, 11)]);
    }

    [Fact]
    public async Task Failing_work_is_reported_and_the_loop_keeps_running()
    {
        var reported = new List<Exception>();
        _thread.UnhandledException += (_, e) => reported.Add((Exception)e.ExceptionObject);

        _thread.Post(() => throw new InvalidOperationException("posted"));
        var stillRunning = await _thread.InvokeAsync(() => true);

        stillRunning.ShouldBeTrue();
        reported.ShouldHaveSingleItem().Message.ShouldBe("posted");
    }

    [Fact]
    public void After_disposal_no_work_is_accepted()
    {
        var thread = SysEventsThread.Start();

        thread.Dispose();

        Should.Throw<ObjectDisposedException>(() => thread.Post(() => { }));
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint window, uint message, nint wParam, nint lParam);
}
