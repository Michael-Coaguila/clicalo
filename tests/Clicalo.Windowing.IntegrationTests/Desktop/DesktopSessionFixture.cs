using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Rendering;

[assembly: AssemblyFixture(
    typeof(Clicalo.Windowing.IntegrationTests.Desktop.DesktopSessionFixture)
)]

namespace Clicalo.Windowing.IntegrationTests.Desktop;

/// <summary>
/// Holds the <see cref="DesktopSessionLock"/> while the desktop tests of this assembly run, so another desktop test
/// run or a SpikeLab session never takes the foreground from this one's InputProbe. Only when desktop tests are
/// enabled; headless runs never wait for it. At the end it shuts the shared WPF thread down (<see cref="WpfThread"/>).
/// </summary>
public sealed class DesktopSessionFixture : IAsyncLifetime
{
    /// <summary>How long the WPF thread may take to end after its dispatcher is shut down.</summary>
    private static readonly TimeSpan WpfShutdownTimeout = TimeSpan.FromSeconds(10);

    private DesktopSessionLock? _lock;

    public async ValueTask InitializeAsync()
    {
        if (DesktopTestEnvironment.IsEnabled)
        {
            _lock = await DesktopSessionLock.AcquireAsync(DesktopSessionLock.BusyMessage);
        }
    }

    public ValueTask DisposeAsync()
    {
        // The process must not exit with the WPF thread still pumping messages: once its windows have had the
        // foreground, the thread holds the text input framework (TextInputFramework.dll, CoreMessaging.dll), and a
        // process that exits around it ended 1 in about 2,600 runs with the fail-fast code 0xE0464645 after every
        // test had passed; with the thread shut down first, 0 in 25,000 (S1.md, finding 20). A thread that does not
        // end fails the run here instead.
        WpfThread.Shutdown(WpfShutdownTimeout);
        _lock?.Dispose();
        return ValueTask.CompletedTask;
    }
}
