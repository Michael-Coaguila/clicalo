using System.Collections.Immutable;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter;
using Clicalo.Presentation.ControlCenter.Editor;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// What M6 closed in the editor and the frame of the Control Center, headless (the window is built but never shown):
/// recording with the keyboard, the alternative of a blocked combination, «Elegir programa», the confirmation before
/// «Probar ahora», the auto-release lines, the plurals, the active app and the status bar as a live region.
/// </summary>
public sealed class ControlCenterM6Tests
{
    private static readonly ShortcutId Bold = new("bold");
    private static readonly ShortcutId Talk = new("talk");

    [Fact]
    [Trait("Req", "EDI-010")]
    [Trait("Req", "BIE-005")]
    public void Recording_with_the_keyboard_takes_the_keys_the_window_receives_in_press_order()
    {
        var world = new ControlCenterTestWorld();
        var notices = new List<WorkspaceNotice>();
        world.Shortcuts.Noticed += (_, e) => notices.Add(e.Notice);
        Run(
            world.Services,
            world,
            viewModel =>
            {
                var editor = viewModel.Shortcuts.Editor;
                var combo = editor.Model!.Combo.ShouldNotBeNull();
                combo.RecordText.ShouldBe("Grabar con teclado");
                combo.RecordHint.ShouldBe("Opcional, si alguien tiene un teclado a mano.");
                combo.Recording.ShouldBeNull();
                editor.IsRecording.ShouldBeFalse();

                editor.ToggleRecording();
                WpfThread.DrainPendingWork();
                editor.IsRecording.ShouldBeTrue();
                combo = editor.Model!.Combo.ShouldNotBeNull();
                combo.Recording.ShouldBe("Pulsa la combinación en tu teclado…");
                combo.RecordText.ShouldBe("Cancelar");

                // Modifiers alone do not end it; the first other key does, with the modifiers held and their side.
                editor.RecordKeyDown(RecordedKeys.IdOf(Key.RightShift));
                editor.RecordKeyDown(RecordedKeys.IdOf(Key.LeftCtrl));
                editor.IsRecording.ShouldBeTrue();
                editor.RecordKeyDown(RecordedKeys.IdOf(Key.OemComma));
                editor.IsRecording.ShouldBeTrue("a key that is not in the catalog is ignored");
                editor.RecordKeyDown(RecordedKeys.IdOf(Key.B));
                WpfThread.DrainPendingWork();

                editor.IsRecording.ShouldBeFalse();
                world.Store.Current.Library.TryGetShortcut(Bold, out var bold).ShouldBeTrue();
                bold!
                    .Action.ShouldBeOfType<TapAction>()
                    .Chord.Strokes.Select(s => (s.Key, s.Side))
                    .ShouldBe([
                        (KeyIds.Shift, KeySide.Right),
                        (KeyIds.Ctrl, KeySide.Left),
                        (KeyIds.B, KeySide.Any),
                    ]);
                world
                    .Localization.Current.Format(notices[^1].Text)
                    .ShouldStartWith("Combinación grabada: ");
                editor.Model!.Combo!.Recording.ShouldBeNull();

                // Esc cancels and changes nothing.
                editor.ToggleRecording();
                editor.RecordKeyDown(RecordedKeys.IdOf(Key.LeftAlt));
                editor.RecordKeyDown(RecordedKeys.IdOf(Key.Escape));
                editor.IsRecording.ShouldBeFalse();
                world.Store.Current.Library.TryGetShortcut(Bold, out var same).ShouldBeTrue();
                same.ShouldBe(bold);

                // «No puedo usar el teclado» hides the row.
                _ = world.Store.Dispatch(new SetSetting(SettingPaths.NoKeyboardUser, true));
                WpfThread.DrainPendingWork();
                editor.Model!.Combo!.RecordText.ShouldBeNull();
            }
        );
    }

    [Fact]
    [Trait("Req", "EDI-007")]
    public void The_warning_of_a_blocked_combination_offers_its_alternative()
    {
        var world = new ControlCenterTestWorld();
        Run(
            world.Services,
            world,
            viewModel =>
            {
                var editor = viewModel.Shortcuts.Editor;
                world.Shortcuts.RecordChord(KeyChord.FromKeys(KeyIds.Win, KeyIds.L));
                WpfThread.DrainPendingWork();
                var tap = editor.Model!.Combo.ShouldNotBeNull();
                tap.Warning.ShouldBe(WarningTone.Danger);
                tap.WarningText.ShouldNotBeNull()
                    .ShouldContain("En su lugar, al tocar el botón Clícalo bloquea el equipo");

                // A Hold of the same keys has no alternative: the button does nothing.
                world.Shortcuts.Select(Talk);
                world.Shortcuts.RecordChord(KeyChord.FromKeys(KeyIds.Win, KeyIds.L));
                WpfThread.DrainPendingWork();
                var hold = editor.Model!.Combo.ShouldNotBeNull();
                hold.Warning.ShouldBe(WarningTone.Danger);
                hold.WarningText.ShouldNotBeNull().ShouldEndWith("El botón no hará nada.");
            }
        );
    }

    [Fact]
    [Trait("Req", "EDI-014")]
    public void Choosing_the_app_kind_loads_the_open_apps_and_the_installed_programs()
    {
        var world = new ControlCenterTestWorld();
        const string Calculator = @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
        var services = world.Services with
        {
            InstalledPrograms = _ =>
                ValueTask.FromResult<ImmutableArray<InstalledProgram>>([
                    new InstalledProgram("Calculadora", Calculator),
                ]),
        };
        Run(
            services,
            world,
            viewModel =>
            {
                var editor = viewModel.Shortcuts.Editor;
                editor.SetKind(ActionKind.App);
                WpfThread.DrainPendingWork();
                WpfThread.DrainPendingWork();

                // Without opening the «Probar» card: the list is there when the kind shows.
                var target = editor.Model!.Target.ShouldNotBeNull();
                target.PickLabel.ShouldBe("Elegir programa");
                target.Picks.Select(p => p.Name).ShouldBe(["Word", "Chrome"]);
                target.ProgramsLabel.ShouldBe("Programas instalados");
                target.Programs.ShouldHaveSingleItem().Name.ShouldBe("Calculadora");

                editor.PickInstalled(Calculator);
                WpfThread.DrainPendingWork();
                world.Store.Current.Library.TryGetShortcut(Bold, out var shortcut).ShouldBeTrue();
                shortcut!
                    .Action.ShouldBeOfType<AppAction>()
                    .Target.ShouldBe(
                        new AppTarget.StoreApp("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")
                    );
                editor.Model!.Target!.Value.ShouldBe(Calculator);
            }
        );
    }

    [Fact]
    [Trait("Req", "PRB-004")]
    public void A_shortcut_that_asks_for_confirmation_is_confirmed_before_it_is_tried()
    {
        var world = new ControlCenterTestWorld();
        var tries = new List<Shortcut>();
        var services = world.Services with
        {
            TryNow = (shortcut, _, _) =>
            {
                tries.Add(shortcut);
                return ValueTask.FromResult(TryNowOutcome.Asked);
            },
        };
        Run(
            services,
            world,
            viewModel =>
            {
                var editor = viewModel.Shortcuts.Editor;
                world.Shortcuts.SetConfirm(true);
                editor.ToggleTest();
                WpfThread.DrainPendingWork();
                WpfThread.DrainPendingWork();
                var test = editor.Model!.Test.ShouldNotBeNull();
                test.CanLive.ShouldBeTrue();
                test.LiveArmed.ShouldBeFalse();
                test.LiveText.ShouldBe("Probar ahora en Word");

                editor.TryLive();
                WpfThread.DrainPendingWork();
                tries.ShouldBeEmpty("the first tap only arms it");
                test = editor.Model!.Test.ShouldNotBeNull();
                test.LiveArmed.ShouldBeTrue();
                test.LiveText.ShouldBe("Toca otra vez para confirmar");

                editor.TryLive();
                WpfThread.DrainPendingWork();
                tries.ShouldHaveSingleItem().Id.ShouldBe(Bold);
                editor.Model!.Test!.Question.ShouldBe("¿Hizo lo esperado en Word?");
            }
        );
    }

    [Fact]
    [Trait("Req", "BUR-004")]
    [Trait("Req", "PRB-004")]
    public void Paused_the_test_card_says_so_and_its_button_resumes_instead_of_trying()
    {
        var world = new ControlCenterTestWorld();
        var tries = 0;
        var resumes = 0;
        var paused = true;
        var services = world.Services with
        {
            TryNow = (_, _, _) =>
            {
                tries++;
                return ValueTask.FromResult(TryNowOutcome.Asked);
            },
            IsPaused = () => paused,
            Resume = () => resumes++,
        };
        Run(
            services,
            world,
            viewModel =>
            {
                var editor = viewModel.Shortcuts.Editor;
                editor.ToggleTest();
                WpfThread.DrainPendingWork();
                WpfThread.DrainPendingWork();
                var test = editor.Model!.Test.ShouldNotBeNull();
                test.PausedText.ShouldBe(
                    "Clícalo está en pausa y no envía nada. Reanuda para probar."
                );
                test.LiveText.ShouldBe("Reanudar");
                test.CanLive.ShouldBeTrue();
                test.LiveArmed.ShouldBeFalse();

                editor.TryLive();
                WpfThread.DrainPendingWork();
                resumes.ShouldBe(1);
                tries.ShouldBe(0, "nothing is tried while paused");
                editor.Model!.Test!.Question.ShouldBeNull();

                // Resumed: the card is «Probar ahora» again and tries.
                paused = false;
                viewModel.Shortcuts.Invalidate();
                viewModel.Refresh();
                WpfThread.DrainPendingWork();
                test = editor.Model!.Test.ShouldNotBeNull();
                test.PausedText.ShouldBeNull();
                test.LiveText.ShouldBe("Probar ahora en Word");

                editor.TryLive();
                WpfThread.DrainPendingWork();
                tries.ShouldBe(1);
                resumes.ShouldBe(1);
            }
        );
    }

    [Fact]
    [Trait("Req", "EDI-016")]
    public void The_auto_release_lines_say_what_the_shortcut_really_does()
    {
        var world = new ControlCenterTestWorld();
        Run(
            world.Services,
            world,
            viewModel =>
            {
                var editor = viewModel.Shortcuts.Editor;
                world.Shortcuts.Select(Talk);
                editor.ToggleMore();
                WpfThread.DrainPendingWork();
                var more = editor.Model!.More;
                more.HoldText.ShouldBe(
                    "Por seguridad, si la olvidas pulsada se suelta sola al pasar ese tiempo."
                );
                more.HoldSwitchText.ShouldBe("También se suelta al cambiar de app.");

                // «Nunca» and «Soltar al cambiar de app» off: it promises neither.
                editor.SetHold(more.Holds.Count - 1);
                _ = world.Store.Dispatch(new SetSetting(SettingPaths.ReleaseOnAppSwitch, false));
                WpfThread.DrainPendingWork();
                more = editor.Model!.More;
                more.HoldText.ShouldStartWith("Con «Nunca» no se suelta sola");
                more.HoldSwitchText.ShouldStartWith("No se suelta al cambiar de app");
            }
        );
    }

    [Fact]
    [Trait("Req", "EDI-001")]
    [Trait("Req", "ACC-011")]
    public void Dictating_the_name_says_so_in_the_status_bar_and_the_icon_search_has_its_microphone()
    {
        var world = new ControlCenterTestWorld();
        var notices = new List<WorkspaceNotice>();
        var services = world.Services with
        {
            Dictate = _ => ValueTask.FromResult(true),
            Notify = notices.Add,
        };
        Run(
            services,
            world,
            viewModel =>
            {
                var editor = viewModel.Shortcuts.Editor;

                editor.DictateName();
                WpfThread.DrainPendingWork();

                var notice = notices.ShouldHaveSingleItem();
                notice.Text.ShouldBe(L.DictNameT);
                notice.Icon.ShouldBe("mic");
                editor.TogglePicker();
                WpfThread.DrainPendingWork();
                editor.Model!.Picker.ShouldNotBeNull().DictateName.ShouldBe("Dictar");
            }
        );
    }

    [Fact]
    [Trait("Req", "IDI-004")]
    [Trait("Req", "ATJ-008")]
    public void Counts_have_their_plural_and_the_app_in_front_is_marked_active()
    {
        var world = new ControlCenterTestWorld();
        Run(
            world.Services,
            world,
            viewModel =>
            {
                viewModel.Nav[0].CountName.ShouldBe("1 combinación repetida entre perfiles");
                var screen = viewModel.Shortcuts.Screen;
                screen.TopBar.Duplicates.ShouldBe(
                    "1 combinación repetida entre perfiles · Revisar"
                );
                screen.TopBar.Layers[1].Meta.ShouldBe("6 · app activa");
                screen.Header.Subtitle.ShouldBe("Se activa solo al abrir WINWORD.EXE");

                // The profile of Chrome: Word is in front behind the Control Center, so it goes first, marked.
                world.Shortcuts.SelectList(new ListRef.InProfile(new ProfileId("chrome")));
                viewModel.Shortcuts.LinkAction();
                WpfThread.DrainPendingWork();
                WpfThread.DrainPendingWork();
                var apps = viewModel.Shortcuts.Screen.Link.ShouldNotBeNull().Apps;
                apps.Select(a => a.Name).ShouldBe(["Word", "Chrome"]);
                apps[0].Mark.ShouldBe("app activa");
                apps[0].Selected.ShouldBeFalse();
                apps[1].Mark.ShouldBeNull();
                apps[1].Selected.ShouldBeTrue("the bound one");
            }
        );
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "CCM-003")]
    public void The_status_bar_is_a_live_region_that_keeps_its_element_and_announces_each_message()
    {
        var world = new ControlCenterTestWorld();
        Run(
            world.Services,
            world,
            (viewModel, window) =>
            {
                viewModel.Status.IsNotice.ShouldBeFalse("[saved] at rest is not read");
                var resting = StatusText(window, "Cambios guardados");

                viewModel.ShowNotice(
                    new WorkspaceNotice(L.ComboCleared, "restart_alt", true, false)
                );
                viewModel.Status.IsNotice.ShouldBeTrue();
                var notice = StatusText(window, viewModel.Status.Text);
                notice.ShouldBeSameAs(resting, "the region stays, so its change is announced");
                AutomationProperties.GetLiveSetting(notice).ShouldBe(AutomationLiveSetting.Polite);

                viewModel.ShowNotice(new WorkspaceNotice(L.BlockedB, "block", false, true));
                var warning = StatusText(window, viewModel.Status.Text);
                warning.ShouldBeSameAs(resting);
                AutomationProperties
                    .GetLiveSetting(warning)
                    .ShouldBe(AutomationLiveSetting.Assertive);

                viewModel.ClearNotice();
                viewModel.Status.IsNotice.ShouldBeFalse();
                StatusText(window, "Cambios guardados").ShouldBeSameAs(resting);
            }
        );
    }

    private static TextBlock StatusText(ControlCenterWindow window, string text) =>
        Descendants((DependencyObject)window.Content)
            .OfType<TextBlock>()
            .Where(block => string.Equals(block.Text, text, StringComparison.Ordinal))
            .ShouldHaveSingleItem();

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    private static void Run(
        ControlCenterServices services,
        ControlCenterTestWorld world,
        Action<ControlCenterViewModel> test
    ) => Run(services, world, (viewModel, _) => test(viewModel));

    private static void Run(
        ControlCenterServices services,
        ControlCenterTestWorld world,
        Action<ControlCenterViewModel, ControlCenterWindow> test
    )
    {
        var (window, theme, viewModel) = WpfThread.Invoke(() =>
        {
            var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                100,
                reduceMotion: true
            );
            world.Shortcuts.Open(new ListRef.InProfile(ControlCenterTestWorld.Word), null, false);
            var viewModel = new ControlCenterViewModel(services, () => { });
            // As the composition root does: every change projects again once per dispatcher turn.
            world.Shortcuts.Changed += (_, _) => viewModel.Shortcuts.Invalidate();
            world.Profiles.Changed += (_, _) => viewModel.Shortcuts.Invalidate();
            world.Store.Changed += (_, _) =>
            {
                world.Shortcuts.OnDocumentChanged();
                viewModel.Shortcuts.Invalidate();
            };
            var window = new ControlCenterWindow(viewModel, theme);
            WpfThread.DrainPendingWork();
            return (window, theme, viewModel);
        });
        try
        {
            WpfThread.Invoke(() => test(viewModel, window));
        }
        finally
        {
            WpfThread.Invoke(() =>
            {
                window.Destroy();
                theme.Dispose();
            });
        }
    }
}
