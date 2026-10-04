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

    private static readonly TimeSpan SwitchTimeout = TimeSpan.FromSeconds(30);

    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly List<string> _timeline = [];

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
            Note("theme applied: " + surface.Lab.Theme.Effective.ToString());
        EventHandler templated = (_, _) => Note("window template applied");
        EventHandler laidOut = (_, _) => Note("layout updated");
        WpfThread.Invoke(() =>
        {
            source.AddHook(hook);
            surface.Lab.Theme.Applied += applied;
            surface.Surface.TemplateApplied += templated;
            surface.Surface.LayoutUpdated += laidOut;
        });

        try
        {
            try
            {
                Note("contrast on requested");
                if (!SystemContrastSwitch.TrySet(on: true) || !await WaitForSystemAsync(on: true))
                {
                    Assert.Skip("Windows did not turn a contrast theme on in this runner session.");
                }

                Note("system reports contrast on");
                (await WaitForEffectiveAsync(ThemeId.SystemHighContrast)).ShouldBeTrue(
                    "the surface did not follow the Windows contrast theme (TEM-001)"
                );
                var during = Snapshot();
                ShouldBeTheSameTree(during, before, "with the contrast theme on");
                UiaVerifier.ShouldPass(during, expectations);
            }
            finally
            {
                Note("contrast off requested");
                SystemContrastSwitch.TrySet(on: false);
                await WaitForSystemAsync(on: false);
                Note("system reports contrast off");
            }

            (await WaitForEffectiveAsync(ThemeId.Dark)).ShouldBeTrue(
                "the surface did not come back"
            );
            ShouldBeTheSameTree(Snapshot(), before, "after the contrast theme went off");
        }
        finally
        {
            WpfThread.Invoke(() =>
            {
                source.RemoveHook(hook);
                surface.Lab.Theme.Applied -= applied;
                surface.Surface.TemplateApplied -= templated;
                surface.Surface.LayoutUpdated -= laidOut;
            });
        }

        surface.Guard.Violations.ShouldBe(0);
        WpfThread.Invoke(() => surface.Surface.IsActive).ShouldBeFalse();
    }

    /// <summary>
    /// The tree must be the same; when it is not, the failure carries the timeline of the switch and the tree read
    /// again once the surface's dispatcher is idle, to tell a tree caught mid-update from one that stays different.
    /// </summary>
    private void ShouldBeTheSameTree(UiaNode actual, UiaNode expected, string when)
    {
        var text = UiaTreeText.Format(actual);
        var wanted = UiaTreeText.Format(expected);
        var same = string.Equals(text, wanted, StringComparison.Ordinal);
        Note("snapshot " + when + ": " + (same ? "same" : "DIFFERENT"));
        if (same)
        {
            return;
        }

        WpfThread.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
        var again = UiaTreeText.Format(Snapshot());
        Note(
            "snapshot again after ApplicationIdle: "
                + (string.Equals(again, wanted, StringComparison.Ordinal) ? "same" : "DIFFERENT")
        );
        text.ShouldBe(
            wanted,
            "UI Automation tree "
                + when
                + ". Timeline:"
                + Environment.NewLine
                + Timeline()
                + "Tree read again after ApplicationIdle:"
                + Environment.NewLine
                + again
        );
    }

    private nint Record(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        var name = message switch
        {
            WmSysColorChange => "WM_SYSCOLORCHANGE",
            WmSettingChange => "WM_SETTINGCHANGE",
            WmThemeChanged => "WM_THEMECHANGED",
            WmDwmColorizationColorChanged => "WM_DWMCOLORIZATIONCOLORCHANGED",
            _ => null,
        };
        if (name is not null)
        {
            Note(name);
        }

        return 0;
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
