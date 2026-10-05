using System.Diagnostics;
using System.Runtime.InteropServices;
using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.Platform.IntegrationTests.Desktop;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.Tray;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// Each foreground adapter of Platform.Windows on its own, against InputProbe (spike S4): <c>ForegroundControl</c>
/// (there and back, verified), <c>ForegroundMonitor</c> (an external change, once and on the SysEvents thread; never
/// the own windows), <c>InternalRightsHotkey</c> (registration, <c>WM_HOTKEY</c> within
/// <c>Timings.Foreground.RightsHotkeyTimeout</c>, and a chord whose F24 press reaches no app) and the tray icon.
/// </summary>
/// <remarks>
/// <para>
/// Safety (blueprint §3.6, S4 laboratory rule): every batch goes through a guarded injector that checks with
/// <c>GetForegroundWindow</c>, immediately before sending, that InputProbe or a window of this process is in front,
/// and carries its own releases; only left modifiers are used. No pointer input is injected.
/// </para>
/// <para>
/// The case without the foreground right cannot be produced here: Windows gives the right to the process that
/// injected the last input, and this process injects the chords (S4 finding). It is covered with fake ports in
/// Application.Tests and by hand in S4.
/// </para>
/// </remarks>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
public sealed class ForegroundAdapterTests : IClassFixture<ForegroundDesktopFixture>
{
    private readonly DesktopProbeFixture _desktop;
    private readonly ForegroundDesktopFixture _foreground;

    public ForegroundAdapterTests(DesktopProbeFixture desktop, ForegroundDesktopFixture foreground)
    {
        _desktop = desktop;
        _foreground = foreground;
        if (DesktopTestEnvironment.IsEnabled)
        {
            foreground.ProbeWindow = desktop.Probe.Window;
        }
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private WindowToken Probe => new(_desktop.Probe.Window);

    [DesktopFact]
    public async Task With_the_right_of_a_hotkey_the_foreground_goes_to_an_own_window_and_back_verified()
    {
        _ = await _desktop.PrepareAsync();
        await _foreground.GainRightsAsync(Cancellation);

        (
            await _foreground.SetForegroundVerifiedAsync(_foreground.Window.Token, Cancellation)
        ).ShouldBeTrue(ForegroundWindows.Describe());
        _foreground.Control.GetForeground().ShouldBe(_foreground.Window.Token);
        (await _foreground.SetForegroundVerifiedAsync(Probe, Cancellation)).ShouldBeTrue(
            ForegroundWindows.Describe()
        );

        _desktop.Probe.IsForeground.ShouldBeTrue(ForegroundWindows.Describe());
    }

    [DesktopFact]
    public async Task A_window_that_no_longer_exists_is_never_brought_to_the_front()
    {
        var closed = await TestWindow.CreateAsync(_foreground.Thread);
        await closed.DisposeAsync();

        _foreground.Control.TrySetForeground(closed.Token).ShouldBeFalse();
        _foreground.Control.TrySetForeground(WindowToken.None).ShouldBeFalse();
    }

    [DesktopFact]
    public async Task An_external_change_is_reported_once_on_the_SysEvents_thread_and_own_windows_never()
    {
        _ = await _desktop.PrepareAsync();
        await _foreground.GainRightsAsync(Cancellation);
        await SettleAsync();
        var reports = new List<(ExternalForeground Foreground, bool OnSysEvents)>();
        EventHandler<ExternalForegroundChangedEventArgs> handler = (_, e) =>
        {
            lock (reports)
            {
                reports.Add((e.Foreground, _foreground.Thread.CheckAccess()));
            }
        };
        _foreground.Monitor.ExternalForegroundChanged += handler;
        try
        {
            (
                await _foreground.SetForegroundVerifiedAsync(_foreground.Window.Token, Cancellation)
            ).ShouldBeTrue();
            await SettleAsync();
            lock (reports)
            {
                reports.ShouldBeEmpty("Clícalo's own windows are never an external foreground");
            }

            (await _foreground.SetForegroundVerifiedAsync(Probe, Cancellation)).ShouldBeTrue();
            await SettleAsync();
        }
        finally
        {
            _foreground.Monitor.ExternalForegroundChanged -= handler;
        }

        var report = reports.ShouldHaveSingleItem("the probe came back once");
        report.OnSysEvents.ShouldBeTrue();
        report.Foreground.Window.ShouldBe(Probe);
        report.Foreground.ProcessId.ShouldBe((uint)_desktop.Probe.ProcessId);
        report.Foreground.AppProcessId.ShouldBe((uint)_desktop.Probe.ProcessId);
        report.Foreground.Elevation.ShouldNotBe(ProcessElevation.Unknown);
        _foreground.Monitor.Current.ShouldBe(report.Foreground);
    }

    [DesktopFact]
    [Trait("Req", "BUS-002")]
    public async Task The_reserved_chord_arrives_as_WM_HOTKEY_in_time_and_its_F24_press_reaches_no_app()
    {
        _foreground.RequireRegisteredHotkey();
        var cursor = await _desktop.PrepareAsync();
        var arrivals = _foreground.Hotkey.Arrivals;
        var started = Stopwatch.GetTimestamp();

        var arrived = _foreground.Hotkey.WaitForRightsAsync(Cancellation).AsTask();
        (await _foreground.Keys.SendRightsHotkeyAsync(Cancellation)).ShouldBeTrue();
        (await arrived).ShouldBeTrue();
        var elapsed = Stopwatch.GetElapsedTime(started);

        elapsed.ShouldBeLessThan(Timings.Foreground.RightsHotkeyTimeout);
        _foreground.Hotkey.Arrivals.ShouldBe(arrivals + 1);
        var events = await _desktop.CollectAsync(
            cursor,
            received =>
                ProbeInput
                    .ChordModifiers(received)
                    .Any(key => key.VirtualKey == VirtualKeyCode.Control && key.IsRelease)
        );
        ProbeInput
            .ReservedKeyPresses(events)
            .ShouldBeEmpty("the system consumes the registered chord");
        ProbeEvents.TypedChars(events).ShouldBeEmpty();
        ProbeInput.KeyMenus(events).ShouldBeEmpty("Alt was released while Ctrl was down");
        TestContext.Current.TestOutputHelper?.WriteLine(
            "Chord messages seen by the probe: "
                + ProbeInput.Summary(
                    ProbeInput.ChordModifiers(events).Concat(ProbeInput.ReservedKeyReleases(events))
                )
                + string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"; WM_HOTKEY after {elapsed.TotalMilliseconds:0.0} ms"
                )
        );
    }

    [DesktopFact]
    [Trait("Req", "BUS-002")]
    public async Task Without_the_chord_the_wait_ends_after_the_timeout()
    {
        _foreground.RequireRegisteredHotkey();
        var started = Stopwatch.GetTimestamp();
        var timerStarted = TimerClockMilliseconds();

        var arrived = await _foreground.Hotkey.WaitForRightsAsync(Cancellation);

        var timerElapsed = TimerClockMilliseconds() - timerStarted;
        var elapsed = Stopwatch.GetElapsedTime(started);
        arrived.ShouldBeFalse();
        TestContext.Current.TestOutputHelper?.WriteLine(
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Wait ended after {timerElapsed} ms of the timer clock, {elapsed.TotalMilliseconds:0.00} ms of Stopwatch"
            )
        );
        timerElapsed.ShouldBeGreaterThanOrEqualTo(
            (long)Timings.Foreground.RightsHotkeyTimeout.TotalMilliseconds,
            "the wait never gives up before RightsHotkeyTimeout on the clock its timer runs on"
        );
    }

    [DesktopFact]
    public void The_reserved_chord_is_Ctrl_Alt_Shift_F24_with_left_modifiers_released_in_reverse()
    {
        InternalRightsHotkey.VirtualKey.ShouldBe(0x87u);
        GuardedInternalKeyEffects
            .RightsChord.Select(stroke => (stroke.VirtualKey, stroke.IsKeyUp))
            .ShouldBe([
                (VirtualKeyCode.LeftControl, false),
                (VirtualKeyCode.LeftMenu, false),
                (VirtualKeyCode.LeftShift, false),
                (GuardedInternalKeyEffects.F24, false),
                (GuardedInternalKeyEffects.F24, true),
                (VirtualKeyCode.LeftShift, true),
                (VirtualKeyCode.LeftMenu, true),
                (VirtualKeyCode.LeftControl, true),
            ]);
    }

    [DesktopFact]
    [Trait("Req", "BUR-003")]
    public async Task The_tray_icon_is_added_updated_and_removed()
    {
        using var icon = new TrayIcon(_foreground.Thread);

        await icon.ShowAsync("Clicalo desktop test");
        await icon.SetTooltipAsync("Clicalo desktop test (2)");

        icon.IsShown.ShouldBeTrue("Shell_NotifyIcon(NIM_ADD) accepted the 64-bit NOTIFYICONDATAW");
    }

    /// <summary>
    /// The clock that decides when a <see cref="TimeProvider.System"/> timer fires, in whole milliseconds: .NET's timer
    /// queue reads <c>QueryUnbiasedInterruptTime</c>, truncated to the millisecond, when it arms a timer and when it
    /// checks it, and fires once the difference reaches the due time. That clock only advances on each clock interrupt,
    /// so against <see cref="Stopwatch"/> (QPC) a timer can fire up to one interrupt period plus one millisecond early:
    /// the s0 runs of 2026-10-03 saw this wait end at 499.67 ms and 499.13 ms of Stopwatch.
    /// </summary>
    private static long TimerClockMilliseconds()
    {
        QueryUnbiasedInterruptTime(out var hundredNanoseconds).ShouldBeTrue();
        return (long)(hundredNanoseconds / 10_000);
    }

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryUnbiasedInterruptTime(out ulong unbiasedTime);

    /// <summary>Lets the out-of-context WinEvents queued so far reach the SysEvents thread.</summary>
    private async Task SettleAsync()
    {
        await Task.Delay(InputProbeSession.SettleTime, Cancellation);
        await _foreground.Thread.InvokeAsync(() => { });
    }
}
