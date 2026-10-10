using System.IO;
using System.Windows;
using Clicalo.Application.UseCases.Welcome;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
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
                screen.KeyboardLine.ShouldBe(
                    "Para Español (España) · Office y apps en español",
                    "the keyboard Windows reports and the programs language, as in Plantillas"
                );
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
    [Trait("Req", "BIE-010")]
    public void A_repeated_welcome_says_what_changes_and_what_stays_as_it_was_left()
    {
        var world = new WelcomeTestWorld();
        world.Session.Next();
        world.Session.ToggleUse(WelcomeUse.Tremor);
        while (!world.Session.HasEnded)
        {
            world.Session.Next();
        }

        // Afterwards the person makes the panel small by hand.
        _ = world.Store.Dispatch(new SetSetting(SettingPaths.Size, PanelSize.Small));
        var repeated = new WelcomeSession(world.Store, WelcomeTestWorld.Content(), repeat: true);
        var (window, theme, viewModel) = Build(world, repeated);
        try
        {
            WpfThread.Invoke(() =>
            {
                viewModel.Next();
                viewModel.Screen.Uses.Single(u => u.Selected).Label.ShouldBe("Tengo temblor");
                viewModel.Screen.Changes.ShouldBeNull("the recorded answers change nothing");

                viewModel.ToggleUse(nameof(WelcomeUse.Tremor));
                viewModel.ToggleUse(nameof(WelcomeUse.Voice));

                var note = viewModel.Screen.Changes.ShouldNotBeNull();
                note.ChangesTitle.ShouldBe("Al continuar, esto cambia:");
                note.Changes.ShouldBe([
                    "Precisión táctil: Estándar",
                    "Números para control por voz: activados",
                ]);
                note.KeptTitle.ShouldBe("Esto se queda como lo dejaste:");
                note.Kept.ShouldBe(["Tamaño del panel: Pequeño"]);

                viewModel.Next();
                var settings = world.Store.Current.Settings;
                settings.VoiceNumbers.ShouldBeTrue();
                settings.Size.ShouldBe(PanelSize.Small);
                viewModel.Back();
                viewModel.Screen.Changes.ShouldBeNull("nothing more to change");
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "NFR-010")]
    [Trait("Req", "REG-04")]
    public void A_reinstallation_asks_on_step_0_and_starting_from_scratch_takes_two_taps()
    {
        var world = new WelcomeTestWorld();
        world.Session.Skip();
        var session = new WelcomeSession(
            world.Store,
            WelcomeTestWorld.Content(),
            repeat: true,
            WelcomeFreshStart.Of(WelcomeTestWorld.Content, new FreshIds(), world.Time)
        );
        var (window, theme, viewModel) = Build(world, session);
        try
        {
            WpfThread.Invoke(() =>
            {
                var card = viewModel.Screen.Reinstall.ShouldNotBeNull();
                card.Title.ShouldBe("Encontré tus atajos y ajustes de antes");
                card.KeepText.ShouldBe("Conservar mis datos");
                card.FreshText.ShouldBe("Empezar de cero");
                card.FreshArmed.ShouldBeFalse();

                viewModel.StartFromScratch();
                viewModel.Screen.Reinstall!.FreshArmed.ShouldBeTrue();
                viewModel.Screen.Reinstall.FreshText.ShouldBe("¿Seguro?");
                world.Store.Current.Onboarding.Completed.ShouldBeTrue("one tap changes nothing");
                world.Time.Advance(TimeSpan.FromSeconds(4));
                viewModel.Screen.Reinstall!.FreshArmed.ShouldBeFalse("the button disarms on time");

                viewModel.StartFromScratch();
                viewModel.StartFromScratch();
                var done = viewModel.Screen.Reinstall.ShouldNotBeNull();
                done.Done.ShouldStartWith("Empezaste de cero");
                world.Store.Current.Onboarding.Completed.ShouldBeFalse();
                world.Store.Current.Library.AlwaysVisible.ShouldBeEmpty();
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
        WelcomeTestWorld world,
        WelcomeSession? session = null
    ) =>
        WpfThread.Invoke(() =>
        {
            var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                100,
                reduceMotion: true
            );
            var viewModel = new WelcomeViewModel(
                session ?? world.Session,
                world.Localization,
                () => KeyboardLayouts.SpanishSpain,
                world.Time,
                work => work()
            );
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

    private sealed class FreshIds : IIdGenerator
    {
        private int _next;

        public ProfileId NewProfileId() =>
            new("fp" + (++_next).ToString(System.Globalization.CultureInfo.InvariantCulture));

        public ShortcutId NewShortcutId() =>
            new("fs" + (++_next).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
