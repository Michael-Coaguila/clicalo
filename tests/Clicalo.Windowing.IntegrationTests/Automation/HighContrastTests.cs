using System.Windows;
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
    private static readonly TimeSpan SwitchTimeout = TimeSpan.FromSeconds(30);

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

        try
        {
            if (!SystemContrastSwitch.TrySet(on: true) || !await WaitForSystemAsync(on: true))
            {
                Assert.Skip("Windows did not turn a contrast theme on in this runner session.");
            }

            (await WaitForEffectiveAsync(ThemeId.SystemHighContrast)).ShouldBeTrue(
                "the surface did not follow the Windows contrast theme (TEM-001)"
            );
            var during = Snapshot();
            UiaTreeText.Format(during).ShouldBe(UiaTreeText.Format(before));
            UiaVerifier.ShouldPass(during, expectations);
        }
        finally
        {
            SystemContrastSwitch.TrySet(on: false);
            await WaitForSystemAsync(on: false);
        }

        (await WaitForEffectiveAsync(ThemeId.Dark)).ShouldBeTrue("the surface did not come back");
        UiaTreeText.Format(Snapshot()).ShouldBe(UiaTreeText.Format(before));
        surface.Guard.Violations.ShouldBe(0);
        WpfThread.Invoke(() => surface.Surface.IsActive).ShouldBeFalse();
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
