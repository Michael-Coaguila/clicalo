using System.Globalization;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Clicalo.Application.Ports;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Workspace;
using Clicalo.Windowing.IntegrationTests.About;
using Clicalo.Windowing.IntegrationTests.ControlCenter;

namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>
/// REG-02 and REG-06 on the real Control Center (docs/05): its window with each of the six sections and their open
/// states (the editor of each kind of shortcut, the library, the review of repeated combinations, the preview of a
/// template, the three tabs of «Sistema»…), at its default size and at its smallest, in Spanish and in English. The
/// content of the window is hosted on a hidden presentation source (<see cref="AuditHost"/>); nothing is shown. Also
/// with the largest text in the light theme and in high contrast (<see cref="AuditLook"/>), and with no text drawn
/// cut (CCM-005).
/// </summary>
public sealed class ControlCenterAuditTests
{
    /// <summary>The states audited: a name and what the person did to get there.</summary>
    private static readonly Dictionary<string, Action<ControlCenterViewModel>> States = new(
        StringComparer.Ordinal
    )
    {
        ["shortcuts"] = static _ => { },
        ["shortcuts-editor-open"] = static cc =>
        {
            cc.Shortcuts.Editor.ToggleMore();
            cc.Shortcuts.Editor.TogglePicker();
            cc.Shortcuts.Editor.ToggleTest();
            cc.Shortcuts.Editor.ToggleVoiceHow();
        },
        ["shortcuts-hold"] = static cc => SelectShortcut(cc, "talk"),
        ["shortcuts-macro"] = static cc =>
        {
            SelectShortcut(cc, "macro");
            cc.Shortcuts.Editor.ToggleStep(1);
        },
        ["shortcuts-macro-keys"] = static cc =>
        {
            SelectShortcut(cc, "macro");
            cc.Shortcuts.Editor.ToggleStep(0);
        },
        ["shortcuts-incomplete"] = static cc => SelectShortcut(cc, "empty"),
        ["shortcuts-text"] = static cc => cc.Shortcuts.Editor.SetKind(ActionKind.Text),
        ["shortcuts-mouse"] = static cc => cc.Shortcuts.Editor.SetKind(ActionKind.Mouse),
        ["shortcuts-toggle"] = static cc => cc.Shortcuts.Editor.SetKind(ActionKind.Toggle),
        ["shortcuts-url"] = static cc => cc.Shortcuts.Editor.SetKind(ActionKind.Url),
        ["shortcuts-app"] = static cc => cc.Shortcuts.Editor.SetKind(ActionKind.App),
        ["shortcuts-system"] = static cc => cc.Shortcuts.Editor.SetKind(ActionKind.System),
        ["shortcuts-repeated"] = static cc =>
        {
            SelectShortcut(cc, "ccopy");
            cc.Shortcuts.Editor.ToggleDuplicates();
            cc.Shortcuts.ToggleProfileEdit();
            cc.Shortcuts.LinkAction();
        },
        ["shortcuts-always-visible"] = static cc =>
            cc.Shortcuts.SelectList(new ListRef.AlwaysVisible()),
        ["shortcuts-library"] = static cc => cc.Shortcuts.OpenLibrary(),
        ["templates"] = static cc => cc.Select(ControlCenterSection.Templates),
        ["templates-open"] = static cc =>
        {
            cc.Select(ControlCenterSection.Templates);
            var templates = cc.Templates!;
            templates.ToggleKey();
            templates.ToggleKeyboard();
            templates.ToggleBlank();
            templates.ToggleBlankIcons();
        },
        ["templates-preview"] = static cc =>
        {
            cc.Select(ControlCenterSection.Templates);
            cc.Templates!.PreviewTemplate("notes");
            WpfThread.DrainPendingWork();
            cc.Templates.EditRow(0);
        },
        ["templates-preview-keys"] = static cc =>
        {
            cc.Select(ControlCenterSection.Templates);
            cc.Templates!.PreviewTemplate("notes");
            WpfThread.DrainPendingWork();
            cc.Templates.EditRowKeys(1);
        },
        ["general"] = static cc => cc.Select(ControlCenterSection.Panel),
        ["touch"] = static cc => cc.Select(ControlCenterSection.Touch),
        ["system-updates"] = static cc => cc.Select(ControlCenterSection.System),
        ["system-backups"] = static cc =>
        {
            cc.Select(ControlCenterSection.System);
            cc.System!.SelectTab(SystemTab.Backups);
            cc.System.BackupNow();
        },
        ["system-start"] = static cc =>
        {
            cc.Select(ControlCenterSection.System);
            cc.System!.SelectTab(SystemTab.Start);
        },
        ["about"] = static cc => cc.Select(ControlCenterSection.About),
        ["about-open"] = static cc =>
        {
            cc.Select(ControlCenterSection.About);
            cc.About!.SetMessage("No abre el panel");
            cc.About.ToggleLog();
            cc.About.ToggleSystem();
            _ = cc.About.TogglePreviewAsync();
        },
    };

    /// <summary>The first state of each section: where the light theme, which only changes colors, is audited.</summary>
    private static readonly string[] Sections =
    [
        "shortcuts",
        "templates",
        "general",
        "touch",
        "system-updates",
        "about",
    ];

    /// <summary>
    /// Wide (the menu with its names), the default size and the smallest window (CCM-005), as drawn (dark, text at
    /// 100 %). The text size of the person does not reach the Control Center (CUA-011: it is the tiles of the panel
    /// that grow), so the other looks are not repeated on every width: high contrast, whose thicker borders leave the
    /// least room, on every state in the smallest window, and the light theme on the first state of each section.
    /// </summary>
    public static TheoryData<string, string, double, double, AuditLook> Cases()
    {
        var cases = new TheoryData<string, string, double, double, AuditLook>();
        foreach (var state in States.Keys)
        {
            cases.Add(state, "es", 1300, 760, AuditLook.Dark);
            cases.Add(
                state,
                "es",
                ControlCenterWindow.DefaultWidth,
                ControlCenterWindow.DefaultHeight,
                AuditLook.Dark
            );
            cases.Add(state, "en", 760, 520, AuditLook.Dark);
            cases.Add(state, "en", 760, 520, AuditLook.ContrastLargeText);
        }

        foreach (var section in Sections)
        {
            cases.Add(
                section,
                "es",
                ControlCenterWindow.DefaultWidth,
                ControlCenterWindow.DefaultHeight,
                AuditLook.LightLargeText
            );
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-002")]
    [Trait("Req", "ACC-011")]
    public void Every_state_of_the_control_center_follows_the_UIA_rules_and_keeps_44(
        string state,
        string language,
        double width,
        double height,
        AuditLook look
    ) =>
        Run(
            language,
            width,
            height,
            look,
            (viewModel, host) =>
            {
                States[state](viewModel);
                host.LayOut();

                SurfaceAudit.ShouldPass(
                    host,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{state} ({language}, {width:0} × {height:0}, {look})"
                    ),
                    atLeast: 12,
                    TouchInput.Wpf
                );
            }
        );

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "CCM-002")]
    [Trait("Req", "PLA-010")]
    [Trait("Req", "PLA-011")]
    public void Choices_are_selection_items_collapsibles_expand_and_a_switch_row_is_one_switch() =>
        Run(
            "es",
            1300,
            760,
            AuditLook.Dark,
            (viewModel, host) =>
            {
                // The sections of the menu are one group of choices: radio buttons with SelectionItem, not toggles.
                var templates = host.Peer("Plantillas", AutomationControlType.RadioButton);
                templates.GetPattern(PatternInterface.Toggle).ShouldBeNull();
                templates.GetPattern(PatternInterface.Invoke).ShouldBeNull();
                var choice = templates
                    .GetPattern(PatternInterface.SelectionItem)
                    .ShouldBeAssignableTo<ISelectionItemProvider>()!;
                choice.IsSelected.ShouldBeFalse();
                Selection(host.Peer("Atajos", AutomationControlType.RadioButton))
                    .IsSelected.ShouldBeTrue();

                choice.Select();
                host.LayOut();

                viewModel.Section.ShouldBe(ControlCenterSection.Templates, "selecting is the tap");
                var chosen = Selection(host.Peer("Plantillas", AutomationControlType.RadioButton));
                chosen.IsSelected.ShouldBeTrue();
                Should.Throw<InvalidOperationException>(chosen.RemoveFromSelection);

                // PLA-010: «Perfil vacío» is a collapsible with ExpandCollapse.
                var blank = host.Peer("Perfil vacío", AutomationControlType.Button);
                blank.GetPattern(PatternInterface.Toggle).ShouldBeNull();
                var section = blank
                    .GetPattern(PatternInterface.ExpandCollapse)
                    .ShouldBeAssignableTo<IExpandCollapseProvider>()!;
                section.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
                section.Expand();
                host.LayOut();
                viewModel.Templates!.Screen.Blank.Open.ShouldBeTrue();
                host.Peer("Perfil vacío", AutomationControlType.Button)
                    .GetPattern(PatternInterface.ExpandCollapse)
                    .ShouldBeAssignableTo<IExpandCollapseProvider>()!
                    .ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);

                // A row with a switch is the switch (PLA-011): one Toggle with the state, nothing nameless inside.
                var detect = host.Peer("Detectar", AutomationControlType.Button);
                detect
                    .GetPattern(PatternInterface.Toggle)
                    .ShouldBeAssignableTo<IToggleProvider>()!
                    .ToggleState.ShouldBe(ToggleState.On);
                (detect.GetChildren() ?? [])
                    .Where(static child => child.IsControlElement())
                    .ShouldAllBe(static child =>
                        child.GetAutomationControlType() == AutomationControlType.Text
                    );
            }
        );

    private static ISelectionItemProvider Selection(AutomationPeer peer) =>
        peer.GetPattern(PatternInterface.SelectionItem)
            .ShouldBeAssignableTo<ISelectionItemProvider>()!;

    /// <summary>
    /// The Control Center with its six sections over fakes, its window arranged for <paramref name="width"/> and its
    /// content hosted at that size.
    /// </summary>
    private static void Run(
        string language,
        double width,
        double height,
        AuditLook look,
        Action<ControlCenterViewModel, AuditHost> audit
    )
    {
        var setup = new TemplatesSectionTests.Setup();
        var system = new SystemTestWorld();
        system.Updates.Status = system.Updates.Status with
        {
            Phase = UpdatePhase.Found,
            NewVersion = "2.1.0",
            RollbackVersion = "1.9.3",
        };
        setup.World.Localization.TrySetLanguage(language).ShouldBeTrue();
        var services = setup.Services with
        {
            System = system.Services.System,
            About = new AboutSectionTests.World().Services with
            {
                Localization = setup.World.Localization,
            },
            OpenWelcome = static () => { },
        };

        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(look);
            setup.World.Shortcuts.Open(
                new ListRef.InProfile(ControlCenterTestWorld.Word),
                null,
                false
            );
            var viewModel = new ControlCenterViewModel(services, static () => { });
            setup.World.Shortcuts.Changed += (_, _) => viewModel.Shortcuts.Invalidate();
            setup.World.Profiles.Changed += (_, _) => viewModel.Shortcuts.Invalidate();
            setup.World.Store.Changed += (_, _) =>
            {
                setup.World.Shortcuts.OnDocumentChanged();
                viewModel.Shortcuts.Invalidate();
                viewModel.Refresh();
            };
            var window = new ControlCenterWindow(viewModel, theme);
            try
            {
                window.ApplyWidth(width);
                using var host = AuditHost.OfWindow(window, theme, width, height);
                audit(viewModel, host);
            }
            finally
            {
                window.Destroy();
            }
        });
    }

    private static void SelectShortcut(ControlCenterViewModel cc, string id)
    {
        cc.Shortcuts.SelectTile(new ShortcutId(id));
        cc.Shortcuts.Refresh();
    }
}
