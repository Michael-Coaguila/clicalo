using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Automation.Lab;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;
using Clicalo.Windowing.IntegrationTests.Desktop;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// S3 · TEM-001 in place: when Windows turns a contrast theme on, the surface switches to the system colors by
/// itself, and the UI Automation tree (names, roles, patterns and states) stays exactly the same; then back. It
/// changes a session setting, so it runs <b>only on the CI runner</b> and is skipped everywhere else.
/// </summary>
/// <remarks>
/// <para>
/// A contrast switch reaches the surface in stages: <c>WM_SETTINGCHANGE</c> and <c>WM_SYSCOLORCHANGE</c> (the
/// <c>ThemeScope</c> follows them: <c>Effective</c> changes), and hundreds of milliseconds later one or two
/// <c>WM_THEMECHANGED</c>, after each of which WPF applies the window's template again, 100–400 ms later still, and
/// rebuilds the subtree that UI Automation walks. A client walking the tree during that rebuild sees it half built.
/// </para>
/// <para>
/// So the tree is read once the switch is over for the surface: its theme is the expected one, a
/// <c>WM_THEMECHANGED</c> has arrived, the window template has been applied after the last one and the dispatcher is
/// idle; and a read during which the surface handled another theme notification is not a reading of the switched
/// surface, so it is taken again (and logged). Before this, the read followed <c>Effective</c> only: 6 of 100 runs
/// of the focused s0 of 2026-10-03 (run 37163941781) read the tree while the template was being applied, missing
/// «9 Perfil», and every one read it complete again once the dispatcher was idle.
/// </para>
/// </remarks>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "TEM-001")]
[Trait("Req", "REG-06")]
public sealed class HighContrastTests(UiaSurfaceFixture surface) : IClassFixture<UiaSurfaceFixture>
{
    private const int WmSysColorChange = 0x0015;
    private const int WmSettingChange = 0x001A;
    private const int WmThemeChanged = 0x031A;
    private const int WmDwmColorizationColorChanged = 0x0320;

    /// <summary>Reads of the tree that a theme notification may spoil before the test gives up.</summary>
    private const int MaxReads = 3;

    private static readonly TimeSpan SwitchTimeout = TimeSpan.FromSeconds(30);

    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly List<string> _timeline = [];
    private readonly Lock _gate = new();
    private int _themeChanges;
    private int _notifications;
    private bool _templatePending;

    [DesktopFact]
    public async Task The_tree_survives_a_high_contrast_change()
    {
        Assert.SkipUnless(
            SystemContrastSwitch.IsAllowed,
            "Switches the Windows contrast theme: only on the CI runner, never on a person's machine (S3)."
        );
        Assert.SkipWhen(
            SystemContrastSwitch.IsOn,
            "A contrast theme is already on in this session."
        );
        WpfThread.Invoke(() =>
        {
            surface.Lab.Reset();
            surface.Lab.SetVoiceNumbers(true);
        });
        await surface.PrepareAsync();
        var expectations = LabExpectations.Create(voiceNumbers: true);
        var before = Snapshot();
        UiaVerifier.ShouldPass(before, expectations);
        var source = WpfThread.Invoke(() => HwndSource.FromHwnd(surface.SurfaceHandle));
        HwndSourceHook hook = Record;
        EventHandler applied = (_, _) =>
            Notify("theme applied: " + surface.Lab.Theme.Effective.ToString());
        EventHandler templated = (_, _) => OnTemplateApplied();
        WpfThread.Invoke(() =>
        {
            source.AddHook(hook);
            surface.Lab.Theme.Applied += applied;
            surface.Surface.TemplateApplied += templated;
        });

        try
        {
            var switched = false;
            var offEnded = false;
            try
            {
                var themeChanges = ThemeChanges;
                Note("contrast on requested");
                if (!SystemContrastSwitch.TrySet(on: true) || !await WaitForSystemAsync(on: true))
                {
                    Assert.Skip("Windows did not turn a contrast theme on in this runner session.");
                }

                switched = true;
                Note("system reports contrast on");
                (await WaitForEffectiveAsync(ThemeId.SystemHighContrast)).ShouldBeTrue(
                    "the surface did not follow the Windows contrast theme (TEM-001)"
                );
                SwitchShouldHaveEnded(
                    await SwitchEndedAsync(themeChanges),
                    "with the contrast theme on"
                );
                var during = await SettledSnapshotAsync("with the contrast theme on");
                ShouldBeTheSameTree(during, before, "with the contrast theme on");
                UiaVerifier.ShouldPass(during, expectations);
            }
            finally
            {
                var themeChanges = ThemeChanges;
                Note("contrast off requested");
                SystemContrastSwitch.TrySet(on: false);
                await WaitForSystemAsync(on: false);
                Note("system reports contrast off");
                // Also when the first half failed: the next test of the collection starts after the switch.
                offEnded = switched && await SwitchEndedAsync(themeChanges);
            }

            SwitchShouldHaveEnded(offEnded, "after the contrast theme went off");
            (await WaitForEffectiveAsync(ThemeId.Dark)).ShouldBeTrue(
                "the surface did not come back"
            );
            ShouldBeTheSameTree(
                await SettledSnapshotAsync("after the contrast theme went off"),
                before,
                "after the contrast theme went off"
            );
        }
        finally
        {
            WpfThread.Invoke(() =>
            {
                source.RemoveHook(hook);
                surface.Lab.Theme.Applied -= applied;
                surface.Surface.TemplateApplied -= templated;
            });
        }

        TestContext.Current.TestOutputHelper?.WriteLine("Contrast switch timeline:");
        TestContext.Current.TestOutputHelper?.WriteLine(Timeline());
        surface.Guard.Violations.ShouldBe(0);
        WpfThread.Invoke(() => surface.Surface.IsActive).ShouldBeFalse();
    }

    private int ThemeChanges
    {
        get
        {
            lock (_gate)
            {
                return _themeChanges;
            }
        }
    }

    private int Notifications
    {
        get
        {
            lock (_gate)
            {
                return _notifications;
            }
        }
    }

    /// <summary>
    /// Waits until the surface has received a <c>WM_THEMECHANGED</c> after <paramref name="themeChangesBefore"/> and
    /// WPF has applied the window template after the last one; false after <see cref="SwitchTimeout"/>.
    /// </summary>
    private Task<bool> SwitchEndedAsync(int themeChangesBefore) =>
        PollAsync(() =>
        {
            lock (_gate)
            {
                return _themeChanges > themeChangesBefore && !_templatePending;
            }
        });

    private void SwitchShouldHaveEnded(bool ended, string when)
    {
        Note("switch over " + when + ": " + (ended ? "yes" : "NO"));
        ended.ShouldBeTrue(
            "the surface never got WM_THEMECHANGED and its window template "
                + when
                + ". Timeline:"
                + Environment.NewLine
                + Timeline()
        );
    }

    /// <summary>
    /// The tree read while the surface's dispatcher is idle and no theme notification reaches it; a read that
    /// overlapped one is logged and taken again, at most <see cref="MaxReads"/> times.
    /// </summary>
    private async Task<UiaNode> SettledSnapshotAsync(string when)
    {
        for (var read = 1; ; read++)
        {
            await WpfThread.Dispatcher.InvokeAsync(
                static () => { },
                DispatcherPriority.ApplicationIdle
            );
            var notifications = Notifications;
            var snapshot = Snapshot();
            await WpfThread.Dispatcher.InvokeAsync(
                static () => { },
                DispatcherPriority.ApplicationIdle
            );
            if (Notifications == notifications)
            {
                Note("tree read " + when);
                return snapshot;
            }

            Note("tree read " + when + " overlapped a theme notification: read again");
            read.ShouldBeLessThan(
                MaxReads,
                "the surface kept handling theme notifications "
                    + when
                    + ". Timeline:"
                    + Environment.NewLine
                    + Timeline()
            );
        }
    }

    private void ShouldBeTheSameTree(UiaNode actual, UiaNode expected, string when) =>
        UiaTreeText
            .Format(actual)
            .ShouldBe(
                UiaTreeText.Format(expected),
                "UI Automation tree " + when + ". Timeline:" + Environment.NewLine + Timeline()
            );

    private nint Record(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        switch (message)
        {
            case WmThemeChanged:
                lock (_gate)
                {
                    _themeChanges++;
                    _templatePending = true;
                }

                Notify("WM_THEMECHANGED");
                break;
            case WmSysColorChange:
                Notify("WM_SYSCOLORCHANGE");
                break;
            case WmSettingChange:
                Notify("WM_SETTINGCHANGE");
                break;
            case WmDwmColorizationColorChanged:
                Notify("WM_DWMCOLORIZATIONCOLORCHANGED");
                break;
        }

        return 0;
    }

    private void OnTemplateApplied()
    {
        lock (_gate)
        {
            _templatePending = false;
        }

        Notify("window template applied");
    }

    private void Notify(string what)
    {
        lock (_gate)
        {
            _notifications++;
        }

        Note(what);
    }

    private void Note(string what)
    {
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"+{Stopwatch.GetElapsedTime(_started).TotalMilliseconds:0.0} ms {what}"
        );
        lock (_timeline)
        {
            _timeline.Add(line);
        }
    }

    private string Timeline()
    {
        lock (_timeline)
        {
            return string.Concat(_timeline.Select(line => line + Environment.NewLine));
        }
    }

    private UiaNode Snapshot() =>
        FlaUiSnapshot.Capture(surface.Automation, surface.Window(), surface.Scale);

    private static async Task<bool> WaitForSystemAsync(bool on) =>
        await PollAsync(() =>
            SystemContrastSwitch.IsOn == on
            && WpfThread.Invoke(() => SystemParameters.HighContrast) == on
        );

    private async Task<bool> WaitForEffectiveAsync(ThemeId theme) =>
        await PollAsync(() => WpfThread.Invoke(() => surface.Lab.Theme.Effective) == theme);

    private static async Task<bool> PollAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + SwitchTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return true;
    }
}
