using System.IO;
using System.Windows;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// «Sistema» of the Control Center headless (docs/05 §5): the three tabs with their state (SIS-001), the update card
/// and its button (ACT-001), the two taps of [Volver] (ACT-005), the backups (COP-002 to COP-004), «Iniciar con
/// Windows» and «Reabrir como administrador» (SIS-002 as modified by D7), all over fakes. With
/// <c>CLICALO_CC_PREVIEW=1</c> it also writes PNG previews of the three tabs to <c>artifacts/cc-preview</c>.
/// </summary>
public sealed class SystemSectionTests
{
    [Fact]
    [Trait("Req", "SIS-001")]
    [Trait("Req", "ACT-001")]
    public void The_tabs_show_their_state_and_a_new_version_counts_in_the_menu()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var cc = new ControlCenterViewModel(world.Services, () => { });
            var system = cc.System.ShouldNotBeNull();
            WpfThread.DrainPendingWork();

            system.Screen.Title.ShouldBe("Sistema");
            system.Screen.Tab.ShouldBe(SystemTab.Updates);
            system.Screen.Tabs.Select(t => t.Status).ShouldBe(["v2.0.0 · al día", "Sin copias aún", "Inicio manual"]);
            cc.Nav.Single(n => n.Section == ControlCenterSection.System).Count.ShouldBe(0);

            world.Updates.Status = world.Updates.Status with { Phase = UpdatePhase.Found, NewVersion = "2.1.0" };
            WpfThread.DrainPendingWork();

            system.Screen.Tabs[0].Warn.ShouldBeTrue();
            system.Screen.Tabs[0].Status.ShouldBe("Nueva versión");
            system.Screen.Updates.Card.Title.ShouldBe("Nueva versión disponible · v2.1.0");
            system.Screen.Updates.Card.Button.ShouldBe("Instalar ahora");
            var nav = cc.Nav.Single(n => n.Section == ControlCenterSection.System);
            nav.Count.ShouldBe(1);
            nav.CountName.ShouldBe("Nueva versión");
        });
    }

    [Fact]
    [Trait("Req", "ACT-001")]
    public void The_button_of_the_card_follows_its_state()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            system.UpdateAction();
            world.Updates.Status = world.Updates.Status with { Phase = UpdatePhase.Found, NewVersion = "2.1.0" };
            system.UpdateAction();
            world.Updates.Status = world.Updates.Status with { Phase = UpdatePhase.Updated };
            system.UpdateAction();
            world.Updates.Status = world.Updates.Status with
            {
                Phase = UpdatePhase.Failed,
                Error = UpdateError.Offline,
            };
            WpfThread.DrainPendingWork();
            system.Screen.Updates.Card.Subtitle.ShouldBe("Sin conexión. Comprueba internet y vuelve a intentarlo.");
            system.Screen.Updates.Card.Button.ShouldBe("Reintentar");
            system.UpdateAction();
            world.Updates.Status = world.Updates.Status with { Phase = UpdatePhase.Installing, Percent = 40 };
            WpfThread.DrainPendingWork();
            system.Screen.Updates.Card.ShowBar.ShouldBeTrue();
            system.Screen.Updates.Card.ButtonEnabled.ShouldBeFalse();
            system.UpdateAction();

            world.Updates.Calls.ShouldBe(["check", "install", "ack", "check"]);
        });
    }

    [Fact]
    [Trait("Req", "ACT-002")]
    public void The_switches_and_the_channel_change_the_settings()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            system.Screen.Updates.Switches.Select(s => s.On).ShouldBe([true, true, true]);

            system.ToggleUpdateSwitch(1);
            system.SetChannel(UpdateChannel.Beta);
            WpfThread.DrainPendingWork();

            var settings = world.Base.Store.Current.Settings.Updates;
            settings.AskBefore.ShouldBeFalse();
            settings.Channel.ShouldBe(UpdateChannel.Beta);
            system.Screen.Updates.Channels.Single(c => c.Selected).Label.ShouldBe("Beta");
            world.Updates.Calls.ShouldBe(["check"], "the new channel is read at once");
        });
    }

    [Fact]
    [Trait("Req", "ACT-005")]
    [Trait("Req", "REG-04")]
    public void Going_back_needs_two_taps()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            world.Updates.Status = world.Updates.Status with { RollbackVersion = "1.9.3" };
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            system.Screen.Updates.Rollback.ShouldNotBeNull().Title.ShouldBe("Volver a la versión 1.9.3");

            system.Rollback();
            WpfThread.DrainPendingWork();
            system.Screen.Updates.Rollback!.Armed.ShouldBeTrue();
            system.Screen.Updates.Rollback.Button.ShouldBe("Confirmar");
            world.Updates.Calls.ShouldBeEmpty();
            system.Rollback();

            world.Updates.Calls.ShouldBe(["rollback"]);
            world.Updates.RollbackToken!.Subject.Operation.ShouldBe(UpdateStatus.RollbackOperation);
            world.Updates.RollbackToken.Subject.Target.ShouldBe("1.9.3");
        });
    }

    [Fact]
    [Trait("Req", "COP-002")]
    [Trait("Req", "COP-004")]
    public void A_backup_now_appears_first_in_the_history_and_restores_with_two_taps()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            var notices = Notices(system);
            system.SelectTab(SystemTab.Backups);
            system.Screen.Backups.Rows.ShouldBeEmpty();

            system.BackupNow();
            WpfThread.DrainPendingWork();

            notices.Select(n => n.Icon).ShouldBe(["backup"]);
            var row = system.Screen.Backups.Rows.ShouldHaveSingleItem();
            row.Date.ShouldBe("Hoy, 11:00");
            row.Meta.ShouldBe("Manual · 3 perfiles · 8 atajos");
            system.Screen.Tabs[1].Status.ShouldBe("Última: Hoy, 11:00");
            system.ToggleUpdateSwitch(0);
            world.Base.Store.Current.Settings.Updates.Automatic.ShouldBeFalse();

            system.Restore(row.Id);
            WpfThread.DrainPendingWork();
            system.Screen.Backups.Rows[0].Button.ShouldBe("¿Seguro?");
            system.Restore(row.Id);
            WpfThread.DrainPendingWork();

            notices[^1].Icon.ShouldBe("restore");
            notices[^1].CanUndo.ShouldBeTrue();
            world.Base.Store.Current.Settings.Updates.Automatic.ShouldBeTrue("the backup came back");
        });
    }

    [Fact]
    [Trait("Req", "COP-002")]
    [Trait("Req", "COP-005")]
    public void Importing_first_reads_the_file_then_asks_merge_or_replace()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var cc = new ControlCenterViewModel(world.Services, () => { });
            var system = cc.System!;
            var notices = Notices(system);
            cc.Select(ControlCenterSection.System);
            system.SelectTab(SystemTab.Backups);
            world.Backups.Pick = Results.Ok(
                new ImportPick(ControlCenterTestWorld.Document(), 3, 9, "2.0.0", 2)
            );

            system.Import();
            WpfThread.DrainPendingWork();
            var card = system.Screen.Backups.ImportCard.ShouldNotBeNull();
            card.Summary.ShouldBe("3 perfiles · 9 atajos · versión 2.0.0");
            card.Warning.ShouldStartWith("2 textos cifrados");
            cc.Escape();
            WpfThread.DrainPendingWork();
            system.Screen.Backups.ImportCard.ShouldBeNull("Esc closes the question, not the window");

            system.Import();
            system.ImportMerge();
            WpfThread.DrainPendingWork();
            notices[^1].Icon.ShouldBe("merge");
            system.Screen.Backups.ImportCard.ShouldBeNull();

            system.Import();
            system.ImportReplace();
            WpfThread.DrainPendingWork();
            system.Screen.Backups.ImportCard!.ReplaceArmed.ShouldBeTrue();
            system.ImportReplace();
            WpfThread.DrainPendingWork();
            notices[^1].Icon.ShouldBe("swap_horiz");
            notices[^1].CanUndo.ShouldBeTrue();
        });
    }

    [Fact]
    [Trait("Req", "SIS-002")]
    public void Start_with_Windows_is_a_switch_and_only_for_the_installed_copy()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            var notices = Notices(system);

            system.ToggleStartWithWindows();
            WpfThread.DrainPendingWork();
            world.Startup.IsEnabled.ShouldBeTrue();
            world.Base.Store.Current.Settings.Reliability.StartWithWindows.ShouldBeTrue();
            system.Screen.Tabs[2].Status.ShouldBe("Inicia con Windows");
            system.Screen.Start.StartWithWindows.On.ShouldBeTrue();

            world.Startup.IsAvailable = false;
            system.ToggleStartWithWindows();
            notices.ShouldHaveSingleItem().Text.ShouldBe(Clicalo.Domain.Messages.L.StartNotInstalled);
        });
    }

    [Theory]
    [InlineData(ElevationOutcome.Started, 1, false)]
    [InlineData(ElevationOutcome.Cancelled, 0, true)]
    [InlineData(ElevationOutcome.NotInstalled, 0, true)]
    [InlineData(ElevationOutcome.Failed, 0, true)]
    [Trait("Req", "SIS-002")]
    [Trait("Req", "LOG-007")]
    public void Reopening_as_administrator_ends_this_instance_only_when_Windows_started_the_other(
        ElevationOutcome answer,
        int ended,
        bool explained
    )
    {
        var world = new SystemTestWorld();
        world.Elevation.Answer = answer;
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            var notices = Notices(system);
            system.Screen.Start.Admin.CanReopen.ShouldBeTrue();

            system.ReopenAsAdmin();
            WpfThread.DrainPendingWork();

            world.Elevation.Asked.ShouldBe(1);
            world.Ended.ShouldBe(ended);
            notices.Count.ShouldBe(explained ? 1 : 0);
            system.Screen.Start.Admin.CanReopen.ShouldBeTrue("the button comes back after the answer");
        });
    }

    [Fact]
    [Trait("Req", "SIS-002")]
    public void An_elevated_instance_says_so_instead_of_offering_to_reopen()
    {
        var world = new SystemTestWorld();
        world.Elevation.IsElevated = true;
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);

            system.Screen.Start.Admin.CanReopen.ShouldBeFalse();
            system.Screen.Start.Admin.Description.ShouldBe("Clícalo ya funciona como administrador");
            system.ReopenAsAdmin();
            world.Elevation.Asked.ShouldBe(0);
            system.Screen.Start.CrashStatus.ShouldBe("Siempre activa");
        });
    }

    [Fact]
    [Trait("Req", "SIS-001")]
    [Trait("Req", "REG-02")]
    public void The_window_draws_the_three_tabs()
    {
        var world = new SystemTestWorld();
        world.Updates.Status = world.Updates.Status with
        {
            Phase = UpdatePhase.Found,
            NewVersion = "2.1.0",
            RollbackVersion = "1.9.3",
            Notes =
            [
                new ReleaseNotes(
                    "2.1.0",
                    new DateOnly(2026, 10, 1),
                    true,
                    System.Collections.Immutable.ImmutableDictionary<string, System.Collections.Immutable.ImmutableArray<string>>.Empty.Add("es", ["Pestaña lateral"])
                ),
            ],
        };
        var (window, theme, viewModel) = WpfThread.Invoke(() =>
        {
            var theme = new ThemeService(new FakeSystemTheme(), ThemeChoice.Dark, 100, reduceMotion: true);
            var viewModel = new ControlCenterViewModel(world.Services, () => { });
            var window = new ControlCenterWindow(viewModel, theme);
            viewModel.Select(ControlCenterSection.System);
            WpfThread.DrainPendingWork();
            return (window, theme, viewModel);
        });
        try
        {
            var preview = string.Equals(
                Environment.GetEnvironmentVariable("CLICALO_CC_PREVIEW"),
                "1",
                StringComparison.Ordinal
            );
            foreach (var tab in Enum.GetValues<SystemTab>())
            {
                WpfThread.Invoke(() =>
                {
                    viewModel.System!.SelectTab(tab);
                    if (tab == SystemTab.Backups)
                    {
                        viewModel.System.BackupNow();
                    }

                    WpfThread.DrainPendingWork();
                    viewModel.System.Screen.Tab.ShouldBe(tab);
                });
                if (preview)
                {
                    Preview(window, theme, "system-" + tab.ToString().ToLowerInvariant());
                }
            }
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

    private static List<WorkspaceNotice> Notices(SystemSectionViewModel system)
    {
        var notices = new List<WorkspaceNotice>();
        system.Noticed += (_, e) => notices.Add(e.Notice);
        return notices;
    }

    private static void Preview(ControlCenterWindow window, ThemeService theme, string name)
    {
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        FrameworkElement? root = null;
        var png = RenderSnapshot.Render(
            () =>
            {
                root = (FrameworkElement)window.Content;
                window.Content = null;
                theme.Attach(root);
                return root;
            },
            new RenderSnapshotOptions { Width = 1120, Height = 1100 }
        );
        WpfThread.Invoke(() =>
        {
            theme.Detach(root!);
            window.Content = root;
        });
        var folder = Path.Combine(RepoPaths.Root, "artifacts", "cc-preview");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), png);
    }
}
