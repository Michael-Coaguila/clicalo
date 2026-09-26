using Clicalo.TestKit.Windows;

[assembly: AssemblyFixture(
    typeof(Clicalo.Windowing.IntegrationTests.Desktop.DesktopSessionFixture)
)]

namespace Clicalo.Windowing.IntegrationTests.Desktop;

/// <summary>
/// Holds the <see cref="DesktopSessionLock"/> while the desktop tests of this assembly run, so another desktop test
/// run or a SpikeLab session never takes the foreground from this one's InputProbe. Only when desktop tests are
/// enabled; headless runs never wait for it.
/// </summary>
public sealed class DesktopSessionFixture : IAsyncLifetime
{
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
        _lock?.Dispose();
        return ValueTask.CompletedTask;
    }
}
