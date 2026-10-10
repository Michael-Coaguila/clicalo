using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Profiles;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Messages;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Bubble;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Header;
using Clicalo.Presentation.Panel.Search;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Surfaces.TabView;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Interactions;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.SearchPanel;
using Clicalo.Windowing.IntegrationTests.Theming;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;
using SizeId = Clicalo.Domain.Catalog.PanelSize;
using SizeSetting = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>
/// REG-02 and REG-06 on the real surfaces of the panel (docs/04): the panel in its Full and Compact views in the three
/// sizes and its states (the profile grid, the search, the suggestion, Quick settings, edit mode, the menu of a
/// shortcut, the panic strip, the administrator notice…), the bubble, the panic pill, and the Tab view (handle, bar and
/// the windows beside it) on its four sides. Each surface is the product's own window, built but never shown; its
/// content is hosted on a hidden presentation source (<see cref="AuditHost"/>).
/// </summary>
public sealed class SurfacesAuditTests
{
    private static readonly DimSettings NoDim = new(AutoDim: false, Opacity: 1, DimTo: 1);

    /// <summary>The states of the panel audited: a name and what happened to get there.</summary>
    private static readonly Dictionary<string, Action<PanelWorld>> PanelStates = new(
        StringComparer.Ordinal
    )
    {
        ["idle"] = static _ => { },
        ["picker"] = static w =>
            w.Panel.ApplyContext(PanelBodyContext.Idle with { PickerOpen = true }),
        ["frequents"] = static w =>
            w.Panel.ApplyContext(PanelBodyContext.Idle with { Frequents = true }),
        ["notices"] = static w =>
            w.Panel.ApplyContext(
                PanelBodyContext.Idle with
                {
                    ElevatedApp = "Taskmgr",
                    SuggestionApp = "Notion",
                    CanRepeat = true,
                    Notice = new PanelNotice(
                        L.Deleted,
                        new IconRef("delete"),
                        NoticeTone.Notice,
                        CanUndo: true
                    ),
                }
            ),
        ["capture"] = static w =>
            w.Panel.ApplyContext(
                PanelBodyContext.Idle with
                {
                    Notice = new PanelNotice(
                        L.WaitingApp,
                        new IconRef("radar"),
                        NoticeTone.Notice,
                        CanCancel: true
                    ),
                }
            ),
        ["panic"] = static w =>
            w.Panel.ApplyEngine(PanelBodyTestData.Holding(new ShortcutId("w0"))),
        ["search"] = static w => _ = w.Search.OpenAsync(SearchTrigger.Touch),
        ["suggestion"] = static w =>
            w.Suggestion.Apply(new ProcessName("excel.exe"), searching: false),
        ["quick-settings"] = static w => w.Layers.QuickSettings.Open(),
        ["edit-mode"] = static w =>
        {
            w.Layers.EditMode.Enter();
            w.Panel.ApplyContext(PanelBodyContext.Idle with { EditMode = true, AddTile = true });
        },
        ["tile-menu"] = static w =>
            w.Layers.Menu.Open(new ShortcutId("w0"), "Atajo 0", "keyboard", inFrequents: false),
        ["test-mode"] = static w => w.Layers.TestMode.Start(),
    };

    public static TheoryData<string, SizeSetting, bool, string> PanelCases()
    {
        var cases = new TheoryData<string, SizeSetting, bool, string>();
        foreach (var state in PanelStates.Keys)
        {
            cases.Add(state, SizeSetting.Small, false, "es");
            cases.Add(state, SizeSetting.Medium, true, "en");
            cases.Add(state, SizeSetting.Small, true, "es");
            cases.Add(state, SizeSetting.Large, false, "en");
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(PanelCases))]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-002")]
    public void Every_state_of_the_panel_follows_the_UIA_rules_and_keeps_44(
        string state,
        SizeSetting size,
        bool compact,
        string language
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var world = new PanelWorld(language, time);
            world.Panel.ApplyLayout(
                PanelLayoutSettings.Default with
                {
                    Size = size,
                    Compact = compact,
                    StickyRow = true,
                    VoiceNumbers = true,
                }
            );
            var window = new PanelWindow(
                world.Panel,
                lab.Registry,
                time,
                theme,
                NoDim,
                world.Header,
                world.Search,
                world.Suggestion,
                world.Layers
            );
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                PanelStates[state](world);
                host.LayOut();

                SurfaceAudit.ShouldPass(
                    host,
                    $"panel {state} ({size}, {(compact ? "compact" : "full")}, {language})",
                    atLeast: 8,
                    TouchInput.PointerLayer
                );
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-002")]
    [InlineData(DockSide.Right, SizeId.S)]
    [InlineData(DockSide.Left, SizeId.M)]
    [InlineData(DockSide.Top, SizeId.S)]
    [InlineData(DockSide.Bottom, SizeId.L)]
    public void The_handle_and_the_bar_of_the_Tab_view_follow_the_UIA_rules_and_keep_44(
        DockSide side,
        SizeId size
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(time, side, size, DockFlyout.None, coachStep: 0);
            var handle = new DockHandleWindow(
                side,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var bar = new DockBarWindow(
                side,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            try
            {
                using (var host = AuditHost.OfWindow(handle, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        $"handle ({side})",
                        atLeast: 1,
                        TouchInput.PointerLayer
                    );
                }

                using (var host = AuditHost.OfWindow(bar, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        $"bar ({side}, {size})",
                        atLeast: 10,
                        TouchInput.PointerLayer
                    );
                }
            }
            finally
            {
                handle.Close();
                bar.Close();
            }
        });
    }

    [Theory]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-002")]
    [InlineData(DockFlyout.Pinned, 3)]
    [InlineData(DockFlyout.Profiles, 3)]
    [InlineData(DockFlyout.Sticky, 4)]
    public void The_windows_beside_the_bar_follow_the_UIA_rules_and_keep_44(
        DockFlyout flyout,
        int atLeast
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var world = new PanelWorld("es", time);
            world.Panel.ApplyLayout(PanelLayoutSettings.Default with { StickyRow = true });
            world.Panel.ApplyContext(PanelBodyContext.Idle with { PickerOpen = true });
            var dock = Dock(time, DockSide.Right, SizeId.M, flyout, coachStep: 0);
            var window = new DockFlyoutWindow(
                flyout,
                dock,
                world.Panel,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                SurfaceAudit.ShouldPass(host, $"flyout {flyout}", atLeast, TouchInput.PointerLayer);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-002")]
    public void The_coach_the_notices_the_menu_and_Quick_settings_of_the_Tab_view_keep_44()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var world = new PanelWorld("es", time);
            var dock = Dock(time, DockSide.Right, SizeId.M, DockFlyout.None, coachStep: 1);
            var coach = new DockCoachWindow(
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var notice = new DockNoticeWindow(
                world.Panel,
                world.Layers.TestMode,
                new DockLabels(world.Interactions.Localization),
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var menu = new DockMenuWindow(
                world.Layers.Menu,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var quick = new DockQuickWindow(
                world.Layers.QuickSettings,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                using (var host = AuditHost.OfWindow(coach, theme))
                {
                    SurfaceAudit.ShouldPass(host, "coach", atLeast: 2, TouchInput.PointerLayer);
                }

                world.Panel.ApplyContext(
                    new PanelBodyContext(
                        ElevatedApp: "regedit",
                        Notice: new PanelNotice(
                            L.Deleted,
                            new IconRef("delete"),
                            NoticeTone.Notice,
                            CanUndo: true
                        )
                    )
                );
                using (var host = AuditHost.OfWindow(notice, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        "notices with undo",
                        atLeast: 2,
                        TouchInput.PointerLayer
                    );
                    world.Panel.ApplyContext(
                        new PanelBodyContext(
                            Notice: new PanelNotice(
                                L.WaitingApp,
                                new IconRef("radar"),
                                NoticeTone.Notice,
                                CanCancel: true
                            )
                        )
                    );
                    world.Layers.TestMode.Start();
                    host.LayOut();
                    SurfaceAudit.ShouldPass(
                        host,
                        "notices with cancel in test mode",
                        atLeast: 1,
                        TouchInput.PointerLayer
                    );
                }

                world.Layers.Menu.Open(
                    InteractionsWorld.Bold,
                    "Negrita",
                    "format_bold",
                    inFrequents: true
                );
                using (var host = AuditHost.OfWindow(menu, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        "menu of a shortcut",
                        atLeast: 4,
                        TouchInput.PointerLayer
                    );
                }

                world.Layers.QuickSettings.Views[2].Select();
                world.Layers.QuickSettings.Open();
                using (var host = AuditHost.OfWindow(quick, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        "Quick settings beside the bar",
                        atLeast: 17,
                        TouchInput.PointerLayer
                    );
                }
            }
            finally
            {
                coach.Close();
                notice.Close();
                menu.Close();
                quick.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-002")]
    public void The_bubble_and_the_panic_pill_follow_the_UIA_rules_and_keep_44()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var localization = PanelTestData.Localization("es");
            var bubble = new BubbleWindow(
                new BubbleViewModel(localization, static () => { }, static () => { }),
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var pill = new PanicPillWindow(
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static () => { }
            );
            pill.ApplyLabel(localization.Current.Format(L.ReleaseAll));
            try
            {
                using (var host = AuditHost.OfWindow(bubble, theme))
                {
                    SurfaceAudit.ShouldPass(host, "bubble", atLeast: 1, TouchInput.PointerLayer);
                }

                using (var host = AuditHost.OfWindow(pill, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        "panic pill",
                        atLeast: 1,
                        TouchInput.PointerLayer
                    );
                }
            }
            finally
            {
                bubble.Close();
                pill.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "AJR-001")]
    public void Quick_settings_of_the_header_is_an_expand_collapse_button_that_follows_its_sheet()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var world = new PanelWorld("es", time);
            var window = new PanelWindow(
                world.Panel,
                lab.Registry,
                time,
                theme,
                NoDim,
                world.Header,
                world.Search,
                world.Suggestion,
                world.Layers
            );
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                var quick = host.Peer("Ajustes rápidos");

                quick.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
                quick.GetPattern(PatternInterface.Invoke).ShouldBeNull("one pattern per button");
                quick.GetPattern(PatternInterface.Toggle).ShouldBeNull();
                var sheet = quick
                    .GetPattern(PatternInterface.ExpandCollapse)
                    .ShouldBeAssignableTo<IExpandCollapseProvider>()!;
                sheet.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);

                sheet.Collapse();
                world.QuickSettingsTaps.ShouldBe(0, "it is already closed");
                sheet.Expand();
                world.QuickSettingsTaps.ShouldBe(1, "expanding is the tap");

                world.Header.ApplyLayers(
                    searchOpen: false,
                    editing: false,
                    quickSettingsOpen: true
                );
                sheet.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
                sheet.Expand();
                world.QuickSettingsTaps.ShouldBe(1, "it is already open");
                sheet.Collapse();
                world.QuickSettingsTaps.ShouldBe(2);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-003")]
    [Trait("Req", "PES-009")]
    public void The_buttons_of_the_bar_say_what_they_open_and_what_is_on()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(time, DockSide.Right, SizeId.M, DockFlyout.Pinned, coachStep: 0);
            var bar = new DockBarWindow(
                DockSide.Right,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            try
            {
                // Close, expand, Frequents, profile, Auto/Fixed, search, repeat, pinned, sticky, lock, tune.
                var buttons = bar.Buttons;
                Expanded(buttons[3]).ShouldBe(ExpandCollapseState.Collapsed, "the profile button");
                Expanded(buttons[7]).ShouldBe(ExpandCollapseState.Expanded, "Pinned is open");
                Expanded(buttons[8]).ShouldBe(ExpandCollapseState.Collapsed, "sticky keys");
                Expanded(buttons[10]).ShouldBe(ExpandCollapseState.Collapsed, "Quick settings");
                On(buttons[4]).ShouldBe(ToggleState.Off, "Auto");
                On(buttons[9]).ShouldBe(ToggleState.Off, "the bar folds by itself");
                foreach (var index in new[] { 3, 4, 7, 8, 9, 10 })
                {
                    PeerOf(buttons[index])
                        .GetPattern(PatternInterface.Invoke)
                        .ShouldBeNull("one pattern per button");
                }

                var state = State(DockSide.Right, SizeId.M, DockFlyout.Profiles, coachStep: 0);
                dock.Apply(
                    state with
                    {
                        IsFixed = true,
                        QuickOpen = true,
                        Dock = state.Dock with { PinOpen = true },
                    }
                );

                Expanded(buttons[3]).ShouldBe(ExpandCollapseState.Expanded);
                Expanded(buttons[7]).ShouldBe(ExpandCollapseState.Collapsed);
                Expanded(buttons[10]).ShouldBe(ExpandCollapseState.Expanded);
                On(buttons[4]).ShouldBe(ToggleState.On, "Fixed");
                On(buttons[9]).ShouldBe(ToggleState.On, "the bar stays open");
            }
            finally
            {
                bar.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "PES-013")]
    [Trait("Req", "SEG-002")]
    public void The_floating_Release_all_announces_what_is_held_when_it_appears_and_when_it_changes()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var pill = new PanicPillWindow(
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static () => { }
            );
            pill.ApplyLabel("Soltar todo");
            try
            {
                pill.ApplyHeld("Pulsado: Mantener Ctrl");
                pill.Announcer.Announcements.ShouldBe(0, "a hidden surface announces nothing");
                AutomationProperties.GetHelpText(pill.Button).ShouldBe("Pulsado: Mantener Ctrl");

                using (var host = AuditHost.OfWindow(pill, theme))
                {
                    pill.Announcer.Announcements.ShouldBe(1, "it says why it appeared");
                    pill.Announcer.LastText.ShouldBe("Pulsado: Mantener Ctrl");
                    pill.Announcer.LastUrgency.ShouldBe(AnnouncementUrgency.Assertive);
                    AutomationProperties
                        .GetLiveSetting(pill.Button)
                        .ShouldBe(AutomationLiveSetting.Assertive);
                    pill.Button.Content.ShouldBe("Soltar todo", "the button keeps its own text");
                    host.Peer("Soltar todo").GetHelpText().ShouldBe("Pulsado: Mantener Ctrl");

                    pill.ApplyHeld("Pulsado: Mantener Ctrl");
                    pill.Announcer.Announcements.ShouldBe(1, "nothing changed");
                    pill.ApplyHeld("Pulsado: Mantener Ctrl, Mayús fija");
                    pill.Announcer.Announcements.ShouldBe(2);
                    pill.Announcer.LastText.ShouldBe("Pulsado: Mantener Ctrl, Mayús fija");
                }

                using (AuditHost.OfWindow(pill, theme))
                {
                    pill.Announcer.Announcements.ShouldBe(3, "it appeared again");
                }
            }
            finally
            {
                pill.Close();
            }
        });
    }

    private static AutomationPeer PeerOf(UIElement element) =>
        UIElementAutomationPeer.CreatePeerForElement(element);

    private static ExpandCollapseState Expanded(UIElement element) =>
        PeerOf(element)
            .GetPattern(PatternInterface.ExpandCollapse)
            .ShouldBeAssignableTo<IExpandCollapseProvider>()!
            .ExpandCollapseState;

    private static ToggleState On(UIElement element) =>
        PeerOf(element)
            .GetPattern(PatternInterface.Toggle)
            .ShouldBeAssignableTo<IToggleProvider>()!
            .ToggleState;

    private static ThemeService Theme() =>
        new(new FakeSystemTheme(), ThemeChoice.Dark, 100, reduceMotion: true);

    private static DockBarViewModel Dock(
        TimeProvider time,
        DockSide side,
        SizeId size,
        DockFlyout flyout,
        int coachStep
    )
    {
        var controller = new PanelInteractionController(new PanelEngineInbox(), () => 1, time);
        var dock = new DockBarViewModel(
            controller,
            PanelTestData.Localization("es"),
            new NoDockIntents()
        );
        var model = PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es);
        dock.ApplyTiles(model.Tiles, model.Tiles);
        dock.Apply(State(side, size, flyout, coachStep));
        return dock;
    }

    private static DockBarState State(
        DockSide side,
        SizeId size,
        DockFlyout flyout,
        int coachStep
    ) =>
        new(
            new DockSettings
            {
                Side = side,
                HandlePositions = new DockHandlePositions(50, 50, 50, 50),
                HandleLocked = false,
                PinOpen = false,
                Gutter = false,
                PerPage = 8,
                CoachDone = coachStep == 0,
            },
            PanelSizes.Get(size),
            ShowStripRow: true,
            StickyRow: true,
            VoiceNumbers: true,
            Frequents: false,
            ProfileName: "Word",
            ProfileIcon: "description",
            IsActiveApp: true,
            IsFixed: false,
            CanRepeat: true,
            flyout,
            coachStep,
            BarOpen: true,
            AnythingHeld: true
        );

    /// <summary>The view models of a whole panel over one document and one clock.</summary>
    private sealed class PanelWorld
    {
        public PanelWorld(string language, FakeTimeProvider time)
        {
            Interactions = new InteractionsWorld(language);
            Panel = PanelBodyTestData.Panel(
                PanelBodyTestData.Library(14, 3),
                PanelBodyTestData.Word,
                new RecordingBodyIntents(),
                language
            );
            Header = new PanelHeaderViewModel(
                new ProfileViewCoordinator(Interactions.Store),
                Interactions.Localization,
                new PanelHeaderActions(
                    static () => { },
                    static () => { },
                    () => QuickSettingsTaps++,
                    static () => { }
                )
            );
            Header.Apply(
                PanelHeaderProjection.Project(
                    new ProfileState(new ViewTarget.Profile(SearchTestWorld.Word), false, null),
                    Interactions.Store.Current.Library,
                    SearchTestWorld.Word,
                    searchingWithText: false,
                    new LangCode(language),
                    LangCode.Es
                )
            );
            Search = SearchSurfaceTests.Search();
            Suggestion = SearchSurfaceTests.Suggestion();
            Layers = new PanelLayerModels(
                Interactions.QuickSettings,
                Interactions.EditMode,
                Interactions.Menu,
                Interactions.TestMode,
                Interactions.Modes,
                static () => false
            );
            _ = time;
        }

        public InteractionsWorld Interactions { get; }

        /// <summary>How many times «Ajustes rápidos» of the header was used.</summary>
        public int QuickSettingsTaps { get; private set; }

        public PanelViewModel Panel { get; }

        public PanelHeaderViewModel Header { get; }

        public Clicalo.Presentation.Panel.Search.SearchViewModel Search { get; }

        public Clicalo.Presentation.Panel.Search.SuggestionViewModel Suggestion { get; }

        public PanelLayerModels Layers { get; }
    }

    private sealed class NoDockIntents : IDockIntents
    {
        public void OpenBar() { }

        public void MoveHandle(int percent) { }

        public void CloseBar() { }

        public void Expand() { }

        public void ShowFrequents() { }

        public void ProfileButton() { }

        public void ToggleLock() { }

        public void Search() { }

        public void Repeat() { }

        public void TogglePinned() { }

        public void ToggleSticky() { }

        public void TogglePinOpen() { }

        public void QuickSettings() { }

        public void CoachNext() { }

        public void CoachSkip() { }

        public void ReleaseAll() { }
    }
}
