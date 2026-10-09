using System.IO;
using System.Windows;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Welcome;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace.Welcome;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.Welcome;

/// <summary>
/// The welcome headless (docs/06): the window is built but never shown, so no handle, no desktop, no input and no
/// foreground. It checks the frame (BIE-001), the five steps with their texts and choices (BIE-003 to BIE-008) and
/// the hot language change (BIE-004). With <c>CLICALO_CC_PREVIEW=1</c> it also writes a PNG of each step to
/// <c>artifacts/cc-preview</c> to compare by eye with <c>docs/design/reference</c>.
/// </summary>
public sealed class WelcomeWindowTests
{
    [Fact]
    [Trait("Req", "BIE-001")]
    public void The_window_is_600_wide_on_top_and_never_activates_itself()
    {
        var world = new WelcomeTestWorld();
        var (window, theme, _) = Build(world);
        try
        {
            WpfThread.Invoke(() =>
            {
                window.Width.ShouldBe(WelcomeWindow.WelcomeWidth);
                window.Topmost.ShouldBeTrue();
                window.ShowActivated.ShouldBeFalse("it comes to the front only through its lease");
                window.Title.ShouldBe("Bienvenida a Clícalo");
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "BIE-001")]
    [Trait("Req", "BIE-003")]
    public void Alt_f4_asks_to_skip_and_the_window_stays()
    {
        var world = new WelcomeTestWorld();
        var (window, theme, _) = Build(world);
        try
        {
            var asked = 0;
            WpfThread.Invoke(() =>
            {
                window.CloseRequested += (_, _) => asked++;
                window.Close();
            });
            asked.ShouldBe(1, "Alt+F4 is [Omitir], and the window stays until the session ends");
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "BIE-003")]
    [Trait("Req", "BIE-004")]
    [Trait("Req", "BIE-005")]
    [Trait("Req", "BIE-006")]
    [Trait("Req", "BIE-007")]
    [Trait("Req", "BIE-008")]
    public void The_five_steps_show_the_texts_and_choices_of_the_prototype()
    {
        var world = new WelcomeTestWorld();
        var (window, theme, viewModel) = Build(world);
        try
        {
            WpfThread.Invoke(() =>
            {
                var screen = viewModel.Screen;
                screen.Title.ShouldBe("Tus atajos, a un toque");
                screen.Tagline.ShouldBe("Lo que quieras hacer, clícalo.");
                screen.CreatorRole.ShouldBe("Creador de Clícalo");
                screen.CanBack.ShouldBeFalse();
                screen.Languages.Select(l => l.Label).ShouldBe(["Español", "English"]);
                screen.StepName.ShouldBe("Paso 1 de 5");

                viewModel.Next();
                screen = viewModel.Screen;
                screen.Title.ShouldBe("¿Cómo usas tu equipo?");
                screen
                    .Uses.Select(u => u.Label)
                    .ShouldBe([
                        "Pantalla táctil",
                        "Control por voz",
                        "No puedo usar el teclado",
                        "Tengo temblor",
                        "Mouse o trackball",
                    ]);
                screen.Uses.ShouldAllBe(u => !u.Selected, "nothing is marked for a new user");

                viewModel.Next();
                screen = viewModel.Screen;
                screen.Title.ShouldBe("¿Qué apps usas más?");
                screen.KeyboardLine.ShouldBe("Para Office y apps en español");
                screen.Kit.Count.ShouldBe(10);
                screen.Kit[0].Label.ShouldBe("Básicos");
                screen.Kit[0].Selected.ShouldBeTrue();
                screen.Kit.Skip(1).ShouldAllBe(k => !k.Selected);

                viewModel.Next();
                screen = viewModel.Screen;
                screen.Views.Select(v => v.Label).ShouldBe(["Completa", "Compacta", "Pestaña"]);

                viewModel.Next();
                screen = viewModel.Screen;
                screen.Title.ShouldBe("Elige cómo se ve");
                screen.Sizes.Select(s => s.Label).ShouldBe(["Pequeño", "Mediano", "Grande"]);
                screen
                    .Themes.Select(t => t.Label)
                    .ShouldBe(["Auto", "Oscuro", "Claro", "Alto contraste"]);
                screen.NextText.ShouldBe("Empezar");
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "BIE-004")]
    [Trait("Req", "IDI-001")]
    public void The_language_buttons_change_the_language_at_once()
    {
        var world = new WelcomeTestWorld();
        var (window, theme, viewModel) = Build(world);
        try
        {
            WpfThread.Invoke(() =>
            {
                viewModel.SetLanguage("en");
                viewModel.Refresh();
                viewModel.Screen.Title.ShouldBe("Your shortcuts, one tap away");
                viewModel.Screen.Languages[1].Selected.ShouldBeTrue();
                window.Title.ShouldBe("Welcome to Clícalo");
                world.Store.Current.Settings.Language.Value.ShouldBe("en");
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "BIE-007")]
    [Trait("Req", "BIE-008")]
    public void View_size_and_theme_apply_on_tap()
    {
        var world = new WelcomeTestWorld();
        var (window, theme, viewModel) = Build(world);
        try
        {
            WpfThread.Invoke(() =>
            {
                viewModel.SetDensity(PanelDensity.Compact);
                viewModel.SetSize(PanelSize.Large);
                viewModel.SetTheme(nameof(ThemeChoice.Light));
                var settings = world.Store.Current.Settings;
                settings.Density.ShouldBe(PanelDensity.Compact);
                settings.Size.ShouldBe(PanelSize.Large);
                settings.Theme.ShouldBe(ThemeChoice.Light);
                viewModel.Screen.Sizes.Single(s => s.Selected).Size.ShouldBe(PanelSize.Large);
                viewModel.Screen.Themes.Single(t => t.Selected).Label.ShouldBe("Claro");
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    public void Previews_are_written_when_asked()
    {
        if (
            !string.Equals(
                Environment.GetEnvironmentVariable("CLICALO_CC_PREVIEW"),
                "1",
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        var world = new WelcomeTestWorld();
        var (window, theme, viewModel) = Build(world);
        try
        {
            Preview(window, theme, "welcome-step0");
            WpfThread.Invoke(() =>
            {
                viewModel.Next();
                viewModel.ToggleUse("Touch");
                viewModel.ToggleUse("Voice");
                viewModel.ToggleUse("NoKeyboard");
            });
            Preview(window, theme, "welcome-step1");
            WpfThread.Invoke(() =>
            {
                viewModel.Next();
                viewModel.ToggleKit("browser");
            });
            Preview(window, theme, "welcome-step2");
            WpfThread.Invoke(viewModel.Next);
            Preview(window, theme, "welcome-step3");
            WpfThread.Invoke(viewModel.Next);
            Preview(window, theme, "welcome-step4");
        }
        finally
        {
            Close(window, theme);
        }
    }

    private static (WelcomeWindow Window, ThemeService Theme, WelcomeViewModel ViewModel) Build(
        WelcomeTestWorld world
    ) =>
        WpfThread.Invoke(() =>
        {
            var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                100,
                reduceMotion: true
            );
            var viewModel = new WelcomeViewModel(world.Session, world.Localization);
            world.Localization.LanguageChanged += (_, _) => viewModel.Refresh();
            var window = new WelcomeWindow(viewModel, theme);
            WpfThread.DrainPendingWork();
            return (window, theme, viewModel);
        });

    private static void Preview(WelcomeWindow window, ThemeService theme, string name)
    {
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        FrameworkElement? root = null;
        var png = RenderSnapshot.Render(
            () =>
            {
                root = (FrameworkElement)window.Content;
                window.Content = null;
                theme.Attach(root);
                root.Width = WelcomeWindow.WelcomeWidth;
                return root;
            },
            new RenderSnapshotOptions { Width = WelcomeWindow.WelcomeWidth, Height = 760 }
        );
        WpfThread.Invoke(() =>
        {
            theme.Detach(root!);
            root!.Width = double.NaN;
            window.Content = root;
        });
        var folder = Path.Combine(RepoPaths.Root, "artifacts", "cc-preview");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), png);
    }

    private static void Close(WelcomeWindow window, ThemeService theme) =>
        WpfThread.Invoke(() =>
        {
            window.Destroy();
            theme.Dispose();
        });
}
