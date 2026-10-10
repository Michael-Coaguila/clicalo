using System.Globalization;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Presentation.Welcome;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace.Welcome;
using Clicalo.Windowing.IntegrationTests.Theming;
using Clicalo.Windowing.IntegrationTests.Welcome;

namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>
/// REG-02 and REG-06 on the real welcome (docs/06): its window in each of its five steps, in Spanish and in English, at
/// its width. The content of the window is hosted on a hidden presentation source (<see cref="AuditHost"/>); nothing is
/// shown.
/// </summary>
public sealed class WelcomeAuditTests
{
    [Theory]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-002")]
    [InlineData("es")]
    [InlineData("en")]
    public void Every_step_of_the_welcome_follows_the_UIA_rules_and_keeps_44(string language)
    {
        var world = new WelcomeTestWorld(language: language);
        WpfThread.Invoke(() =>
        {
            using var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                100,
                reduceMotion: true
            );
            var viewModel = new WelcomeViewModel(
                world.Session,
                world.Localization,
                static () => KeyboardLayouts.SpanishSpain,
                world.Time,
                static work => work()
            );
            var window = new WelcomeWindow(viewModel, theme);
            try
            {
                using var host = AuditHost.OfWindow(window, theme, WelcomeWindow.WelcomeWidth);
                for (var step = 0; step < 5; step++)
                {
                    if (step > 0)
                    {
                        viewModel.Next();
                    }

                    if (step == 1)
                    {
                        viewModel.ToggleUse("Touch");
                        viewModel.ToggleUse("NoKeyboard");
                    }

                    host.LayOut();
                    SurfaceAudit.ShouldPass(
                        host,
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"welcome step {step + 1} ({language})"
                        ),
                        atLeast: 3,
                        TouchInput.Wpf
                    );
                }
            }
            finally
            {
                window.Destroy();
            }
        });
    }
}
