using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace;
using Clicalo.Windowing.IntegrationTests.Automation.Audit;
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
            system
                .Screen.Tabs.Select(t => t.Status)
                .ShouldBe(["v2.0.0 · al día", "Sin copias aún", "Inicio manual"]);
            cc.Nav.Single(n => n.Section == ControlCenterSection.System).Count.ShouldBe(0);

            world.Updates.Status = world.Updates.Status with
            {
                Phase = UpdatePhase.Found,
                NewVersion = "2.1.0",
            };
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
            world.Updates.Status = world.Updates.Status with
            {
                Phase = UpdatePhase.Found,
                NewVersion = "2.1.0",
            };
            system.UpdateAction();
            world.Updates.Status = world.Updates.Status with { Phase = UpdatePhase.Updated };
            system.UpdateAction();
            world.Updates.Status = world.Updates.Status with
            {
                Phase = UpdatePhase.Failed,
                Error = UpdateError.Offline,
            };
            WpfThread.DrainPendingWork();
            system.Screen.Updates.Card.Subtitle.ShouldBe(
                "Sin conexión. Comprueba internet y vuelve a intentarlo."
            );
            system.Screen.Updates.Card.Button.ShouldBe("Reintentar");
            system.UpdateAction();
            world.Updates.Status = world.Updates.Status with
            {
                Phase = UpdatePhase.Installing,
                Percent = 40,
            };
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
            system
                .Screen.Updates.Rollback.ShouldNotBeNull()
                .Title.ShouldBe("Volver a la versión 1.9.3");

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
            world
                .Base.Localization.Current.Format(notices[^1].Text)
                .ShouldBe("Copia restaurada: Hoy, 11:00", "COP-004: the notice says which backup");
            world.Base.Store.Current.Settings.Updates.Automatic.ShouldBeTrue(
                "the backup came back"
            );
        });
    }

    [Fact]
    [Trait("Req", "LOG-008")]
    [Trait("Req", "COP-002")]
    public void An_imported_backup_asks_to_confirm_its_web_app_and_macro_shortcuts_one_by_one()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            system.SelectTab(SystemTab.Backups);
            world.Backups.Pick = Results.Ok(
                new ImportPick(WithRisky(ControlCenterTestWorld.Document()), 3, 10, "2.0.0", 0)
            );

            system.Import();
            WpfThread.DrainPendingWork();

            var card = system.Screen.Backups.ImportCard.ShouldNotBeNull();
            card.CanMerge.ShouldBeTrue();
            card.ReviewNote.ShouldStartWith("Estos atajos abren algo o ejecutan varios pasos");
            card.Review.Select(r => r.Name).ShouldBe(["Clima", "Bloc"]);
            card.Review.Select(r => r.Detail).ShouldBe(["https://clima.example/", "notepad.exe"]);
            card.Review.ShouldAllBe(r => !r.Checked, "nothing risky is installed by default");

            system.ToggleReview(card.Review[0].Id);
            WpfThread.DrainPendingWork();
            system.Screen.Backups.ImportCard!.Review[0].Checked.ShouldBeTrue();
            system.ImportMerge();
            WpfThread.DrainPendingWork();

            var names = world
                .Base.Store.Current.Library.EnumerateShortcuts()
                .Select(located => located.Shortcut.Name.Get(LangCode.Es, LangCode.Es))
                .ToList();
            names.ShouldContain("Clima", StringComparer.Ordinal, "the one that was ticked");
            names.ShouldNotContain("Bloc", StringComparer.Ordinal, "the one that was not");
        });
    }

    [Fact]
    [Trait("Req", "LOG-008")]
    [Trait("Req", "COP-004")]
    [Trait("Req", "REG-04")]
    public void Restoring_a_backup_with_new_risky_shortcuts_reviews_them_first()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            var notices = Notices(system);
            system.SelectTab(SystemTab.Backups);
            system.BackupNow();
            WpfThread.DrainPendingWork();
            var row = system.Screen.Backups.Rows.ShouldHaveSingleItem();
            // The file of that backup was changed outside Clícalo: it now opens an address and an app.
            world.Backups.Files[row.Id] = WithRisky(world.Backups.Files[row.Id]);

            system.Restore(row.Id);
            system.Restore(row.Id);
            WpfThread.DrainPendingWork();

            var card = system.Screen.Backups.ImportCard.ShouldNotBeNull("the review opens instead");
            card.CanMerge.ShouldBeFalse();
            card.Title.ShouldBe("Hoy, 11:00");
            card.Replace.ShouldBe("Restaurar");
            card.Review.Select(r => r.Name).ShouldBe(["Clima", "Bloc"]);
            notices
                .Select(n => n.Icon)
                .ShouldBe(
                    ["backup", "shield"],
                    "nothing was restored yet: the review is announced"
                );

            system.ImportReplace();
            WpfThread.DrainPendingWork();
            system.Screen.Backups.ImportCard!.ReplaceArmed.ShouldBeTrue();
            system.Screen.Backups.ImportCard.Replace.ShouldBe("¿Seguro?");
            system.ImportReplace();
            WpfThread.DrainPendingWork();

            notices[^1].Icon.ShouldBe("restore");
            notices[^1].CanUndo.ShouldBeTrue();
            system.Screen.Backups.ImportCard.ShouldBeNull();
            world
                .Base.Store.Current.Library.EnumerateShortcuts()
                .Select(located => located.Shortcut.Id.Value)
                .ShouldNotContain(
                    id => id.StartsWith("risky-", StringComparison.Ordinal),
                    "nothing that was not ticked came back"
                );
        });
    }

    [Fact]
    [Trait("Req", "NFR-010")]
    [Trait("Req", "REG-04")]
    public void Uninstalling_takes_two_taps_and_keeps_the_data_by_default()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            system.SelectTab(SystemTab.Start);
            WpfThread.DrainPendingWork();
            var row = system.Screen.Start.Uninstall;
            row.Title.ShouldBe("Desinstalar Clícalo");
            row.Button.ShouldBe("Desinstalar");
            row.DeleteData.On.ShouldBeFalse("the data is kept by default");
            row.Available.ShouldBeTrue();

            system.Uninstall();
            WpfThread.DrainPendingWork();
            system.Screen.Start.Uninstall.Armed.ShouldBeTrue();
            system.Screen.Start.Uninstall.Button.ShouldBe("¿Seguro?");
            world.Uninstaller.Calls.ShouldBeEmpty();
            system.Uninstall();
            WpfThread.DrainPendingWork();

            world.Uninstaller.Calls.ShouldBe([false]);
            world.Backups.Exported.ShouldBe(0, "keeping the data needs no copy");
            world.Uninstaller.Token!.Subject.Operation.ShouldBe(ISystemUninstall.Operation);
        });
    }

    [Theory]
    [Trait("Req", "NFR-010")]
    [Trait("Req", "REG-08")]
    [InlineData(ExportOutcome.Done, true)]
    [InlineData(ExportOutcome.Cancelled, false)]
    [InlineData(ExportOutcome.Failed, false)]
    [InlineData(ExportOutcome.InsideData, false)]
    public void Deleting_the_data_needs_a_saved_copy_first(ExportOutcome copy, bool uninstalls)
    {
        var world = new SystemTestWorld();
        world.Backups.Export = copy;
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            var notices = Notices(system);
            system.ToggleDeleteData();
            system.Uninstall();
            system.ToggleDeleteData();
            system.ToggleDeleteData();
            system.Uninstall();
            WpfThread.DrainPendingWork();
            world.Uninstaller.Calls.ShouldBeEmpty("changing the option disarms the button");
            system.Uninstall();
            WpfThread.DrainPendingWork();

            world.Backups.Exported.ShouldBe(1);
            world.Backups.ExportedOutsideData.ShouldBe(
                true,
                "the copy must survive the uninstaller"
            );
            world.Uninstaller.Calls.ShouldBe(uninstalls ? [true] : []);
            if (!uninstalls)
            {
                notices
                    .ShouldHaveSingleItem()
                    .Text.ShouldBe(
                        copy == ExportOutcome.InsideData
                            ? Clicalo.Domain.Messages.L.UninstallCopyInside
                            : Clicalo.Domain.Messages.L.UninstallNeedsCopy
                    );
            }
        });
    }

    [Fact]
    [Trait("Req", "NFR-010")]
    public void A_copy_that_was_not_installed_or_an_uninstaller_that_does_not_start_is_explained()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var system = new SystemSectionViewModel(world.Services, world.Services.System!);
            var notices = Notices(system);
            world.Uninstaller.Starts = false;
            system.Uninstall();
            system.Uninstall();
            WpfThread.DrainPendingWork();
            notices[^1].Text.ShouldBe(Clicalo.Domain.Messages.L.UninstallFailed);

            world.Uninstaller.IsAvailable = false;
            system.Refresh();
            system.Screen.Start.Uninstall.Available.ShouldBeFalse();
            system.Screen.Start.Uninstall.Description.ShouldStartWith(
                "Esta copia de Clícalo no se instaló"
            );
            system.Uninstall();
            notices[^1].Text.ShouldBe(Clicalo.Domain.Messages.L.UninstallNotInstalled);
            world.Uninstaller.Calls.Count.ShouldBe(1);
        });
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "SIS-001")]
    public void The_tabs_are_a_tab_control_with_selectable_tab_items_for_ui_automation()
    {
        var world = new SystemTestWorld();
        WpfThread.Invoke(() =>
        {
            var viewModel = new SystemSectionViewModel(world.Services, world.Services.System!);
            var view = new Clicalo.UI.Wpf.Workspace.SystemSection.SystemSectionView(viewModel);
            view.Measure(new Size(1000, 900));
            view.Arrange(new Rect(0, 0, 1000, 900));
            view.UpdateLayout();
            var strip = Find(view, AutomationControlType.Tab).ShouldHaveSingleItem();
            strip.GetName().ShouldBe("Sistema");
            var selection = strip
                .GetPattern(PatternInterface.Selection)
                .ShouldBeAssignableTo<ISelectionProvider>()!;
            selection.CanSelectMultiple.ShouldBeFalse();
            selection.IsSelectionRequired.ShouldBeTrue();

            var tabs = Find(view, AutomationControlType.TabItem);
            tabs.Select(t => t.GetName())
                .ShouldBe(["Actualizaciones", "Copias de seguridad", "Inicio y estabilidad"]);
            tabs.ShouldAllBe(t => t.GetPattern(PatternInterface.Toggle) == null);
            var items = tabs.Select(t =>
                    t.GetPattern(PatternInterface.SelectionItem)
                        .ShouldBeAssignableTo<ISelectionItemProvider>()!
                )
                .ToList();
            items.Select(i => i.IsSelected).ShouldBe([true, false, false]);
            Should.Throw<InvalidOperationException>(items[0].RemoveFromSelection);

            items[2].Select();
            WpfThread.DrainPendingWork();

            viewModel.Screen.Tab.ShouldBe(SystemTab.Start);
            view.UpdateLayout();
            Find(view, AutomationControlType.TabItem)
                .Select(t =>
                    (
                        (ISelectionItemProvider)t.GetPattern(PatternInterface.SelectionItem)!
                    ).IsSelected
                )
                .ShouldBe([false, false, true]);
            view.Detach();
        });
    }

    private static List<AutomationPeer> Find(UIElement root, AutomationControlType type)
    {
        var found = new List<AutomationPeer>();
        void Walk(DependencyObject node)
        {
            if (
                node is UIElement element
                && UIElementAutomationPeer.CreatePeerForElement(element) is { } peer
                && peer.GetAutomationControlType() == type
            )
            {
                found.Add(peer);
            }

            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                Walk(System.Windows.Media.VisualTreeHelper.GetChild(node, i));
            }
        }

        Walk(root);
        return found;
    }

    /// <summary><paramref name="document"/> with a Web and an App shortcut added to General.</summary>
    private static UserDocument WithRisky(UserDocument document)
    {
        static Shortcut Risky(string id, string name, string icon, ShortcutAction action) =>
            new(
                new ShortcutId(id),
                LocalizedText.Same(name, LangCode.Es, LangCode.En),
                new IconRef(icon),
                AutoIcon: false,
                new CategoryId("edit"),
                action,
                new ShortcutOptions(
                    Confirm: false,
                    new HoldLimit.InheritGlobal(),
                    IsPrivate: false
                ),
                Origin: null,
                PinnedFrom: null
            );

        var library = document
            .Library.AddShortcut(
                new ListRef.InProfile(ProfileId.General),
                Risky(
                    "risky-web",
                    "Clima",
                    "public",
                    new UrlAction(new UrlTarget.Valid(new Uri("https://clima.example/")))
                ),
                ListPosition.End
            )
            .Bind(l =>
                l.AddShortcut(
                    new ListRef.InProfile(ProfileId.General),
                    Risky(
                        "risky-app",
                        "Bloc",
                        "edit_note",
                        new AppAction(new AppTarget.Executable("notepad.exe", string.Empty))
                    ),
                    ListPosition.End
                )
            )
            .Value;
        return document with { Library = library };
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
            system.Screen.Backups.ImportCard.ShouldBeNull(
                "Esc closes the question, not the window"
            );

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
            notices
                .ShouldHaveSingleItem()
                .Text.ShouldBe(Clicalo.Domain.Messages.L.StartNotInstalled);
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
            system.Screen.Start.Admin.CanReopen.ShouldBeTrue(
                "the button comes back after the answer"
            );
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
            system.Screen.Start.Admin.Description.ShouldBe(
                "Clícalo ya funciona como administrador"
            );
            system.ReopenAsAdmin();
            world.Elevation.Asked.ShouldBe(0);
            system.Screen.Start.CrashStatus.ShouldBe("Siempre activa");
        });
    }

    [Fact]
    [Trait("Req", "SIS-001")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    public void The_window_draws_the_three_tabs_with_every_control_named_and_at_44()
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
                    System.Collections.Immutable.ImmutableDictionary<
                        string,
                        System.Collections.Immutable.ImmutableArray<string>
                    >.Empty.Add("es", ["Pestaña lateral"])
                ),
            ],
        };
        var (window, theme, viewModel) = WpfThread.Invoke(() =>
        {
            var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                100,
                reduceMotion: true
            );
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

                    // REG-02, REG-06: the tab as it is drawn, at the default size of the window.
                    using var host = AuditHost.OfWindow(
                        window,
                        theme,
                        ControlCenterWindow.DefaultWidth,
                        ControlCenterWindow.DefaultHeight
                    );
                    SurfaceAudit.ShouldPass(host, "Sistema · " + tab, atLeast: 10, TouchInput.Wpf);
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
