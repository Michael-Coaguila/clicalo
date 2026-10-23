using System.Globalization;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
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
            cc.Shortcuts.Editor.ToggleVoiceHow();
        },
        ["shortcuts-test"] = static cc => cc.Shortcuts.Editor.ToggleTest(),
        ["shortcuts-icons"] = static cc => cc.Shortcuts.Editor.TogglePicker(),
        ["shortcuts-icons-search"] = static cc =>
        {
            cc.Shortcuts.Editor.TogglePicker();
            cc.Shortcuts.Editor.SearchIcons("guardar");
        },
        ["shortcuts-icons-category"] = static cc =>
        {
            cc.Shortcuts.Editor.TogglePicker();
            cc.Shortcuts.Editor.ChooseIconCategory("text");
        },
        ["shortcuts-profile-edit"] = static cc => cc.Shortcuts.ToggleProfileEdit(),
        ["shortcuts-profile-icons"] = static cc =>
        {
            cc.Shortcuts.ToggleProfileEdit();
            cc.Shortcuts.ToggleProfileIcons();
        },
        ["shortcuts-link"] = static cc => cc.Shortcuts.LinkAction(),
        ["shortcuts-profiles"] = static _ => { },
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
        ["shortcuts-repeated-many"] = static cc =>
        {
            // REP-004: two repeated combinations, one of them in three places, so the card counts them.
            SelectShortcut(cc, "ccopy");
            cc.Shortcuts.Editor.ToggleDuplicates();
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
        },
        ["templates-icons"] = static cc =>
        {
            cc.Select(ControlCenterSection.Templates);
            cc.Templates!.ToggleBlank();
            cc.Templates.ToggleBlankIcons();
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

    /// <summary>
    /// What the document of a state has besides the one of the tests: «Subrayar» takes Ctrl+C, which «Copiar» already
    /// has in Always visible and in Navegador, and «Cursiva» takes Ctrl+N, the combination of «Negrita» in the same
    /// list (REP-002: the same combination in two profiles with other names is no repetition).
    /// </summary>
    private static readonly Dictionary<string, Action<DocumentStore>> Documents = new(
        StringComparer.Ordinal
    )
    {
        ["shortcuts-repeated-many"] = static store =>
        {
            Rebind(store, "under", KeyIds.Ctrl, KeyIds.C);
            Rebind(store, "italic", KeyIds.Ctrl, KeyIds.N);
            DuplicateIndex
                .Build(store.Current.Library, store.Current.Duplicates)
                .RepeatedCombinationCount.ShouldBe(2);
        },
    };

    /// <summary>
    /// The states of the editor: in the narrow window they are on the page of the editor, which a tap on a tile opens
    /// (CCM-005).
    /// </summary>
    private static readonly HashSet<string> EditorPages = new(StringComparer.Ordinal)
    {
        "shortcuts-editor-open",
        "shortcuts-test",
        "shortcuts-icons",
        "shortcuts-icons-search",
        "shortcuts-icons-category",
        "shortcuts-hold",
        "shortcuts-macro",
        "shortcuts-macro-keys",
        "shortcuts-incomplete",
        "shortcuts-text",
        "shortcuts-mouse",
        "shortcuts-toggle",
        "shortcuts-url",
        "shortcuts-app",
        "shortcuts-system",
        "shortcuts-repeated",
        "shortcuts-repeated-many",
        "shortcuts-library",
    };

    /// <summary>The size of the maximized window on the person's screen: 2400 × 1600 at 175 %, without the taskbar.</summary>
    private const double MaximizedWidth = 1371;

    private const double MaximizedHeight = 859;

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
            cases.Add(state, "es", MaximizedWidth, MaximizedHeight, AuditLook.Dark);
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
            (viewModel, window, host) =>
            {
                States[state](viewModel);
                WpfThread.DrainPendingWork();
                if (EditorPages.Contains(state))
                {
                    window.ShowShortcutPage();
                }
                else if (string.Equals(state, "shortcuts-profiles", StringComparison.Ordinal))
                {
                    window.ShowShortcutProfiles();
                }

                host.LayOut();

                SurfaceAudit.ShouldPass(
                    host,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{state} ({language}, {width:0} × {height:0}, {look})"
                    ),
                    atLeast: 12,
                    TouchInput.Wpf,
                    "Centro de control",
                    // ACC-001: the names on the frames of the groups and a few plain panels of the Control Center
                    // still never reach UI Automation (D21 review); its live regions are held to a peer already.
                    liveRegionsOnly: true
                );
            },
            Documents.GetValueOrDefault(state)
        );

    /// <summary>The states with a layer over the content, at the default size and in the smallest window.</summary>
    public static TheoryData<string, string, double, double> LayerCases()
    {
        var cases = new TheoryData<string, string, double, double>();
        foreach (
            var state in new[]
            {
                "shortcuts-profile-edit",
                "shortcuts-link",
                "shortcuts-icons",
                "shortcuts-profile-icons",
                "shortcuts-test",
                "templates-icons",
            }
        )
        {
            cases.Add(
                state,
                "es",
                ControlCenterWindow.DefaultWidth,
                ControlCenterWindow.DefaultHeight
            );
            cases.Add(state, "en", 760, 520);
        }

        cases.Add("shortcuts-profiles", "en", 760, 520);
        return cases;
    }

    [Theory]
    [MemberData(nameof(LayerCases))]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "CCM-005")]
    public void What_a_layer_covers_answers_neither_the_finger_nor_UI_Automation(
        string state,
        string language,
        double width,
        double height
    ) =>
        Run(
            language,
            width,
            height,
            AuditLook.Dark,
            (viewModel, window, host) =>
            {
                States[state](viewModel);
                WpfThread.DrainPendingWork();
                if (EditorPages.Contains(state))
                {
                    window.ShowShortcutPage();
                }
                else if (string.Equals(state, "shortcuts-profiles", StringComparison.Ordinal))
                {
                    window.ShowShortcutProfiles();
                }

                host.LayOut();
                var violations = Rules.Uia011CoveredTargetRule.Verify(host.Root);
                violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
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
            (viewModel, _, host) =>
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

                // A row with a switch is the switch (PLA-011, DIS-30): the whole header is one Toggle with the state,
                // named with what it does and what it detects, and nothing nameless inside.
                var detect = host.Peer(
                    "Detectar Apps abiertas sin perfil",
                    AutomationControlType.Button
                );
                detect
                    .GetPattern(PatternInterface.Toggle)
                    .ShouldBeAssignableTo<IToggleProvider>()!
                    .ToggleState.ShouldBe(ToggleState.On);
                (detect.GetChildren() ?? [])
                    .Where(static child => child.IsControlElement())
                    .ShouldAllBe(static child =>
                        child.GetAutomationControlType() == AutomationControlType.Text
                    );
                // The title is inside the switch, so a tap on it toggles too.
                var row = (System.Windows.FrameworkElement)((UIElementAutomationPeer)detect).Owner;
                InTree(row)
                    .OfType<Clicalo.UI.Wpf.Controls.Overline>()
                    .ShouldContain(static title =>
                        string.Equals(
                            title.Text,
                            "Apps abiertas sin perfil",
                            StringComparison.Ordinal
                        )
                    );
            }
        );

    private static IEnumerable<System.Windows.DependencyObject> InTree(
        System.Windows.DependencyObject root
    )
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in InTree(child))
            {
                yield return nested;
            }
        }
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "CCM-001")]
    public void The_languages_of_the_title_bar_are_named_as_they_read() =>
        Run(
            "es",
            ControlCenterWindow.DefaultWidth,
            ControlCenterWindow.DefaultHeight,
            AuditLook.Dark,
            (_, _, host) =>
            {
                // Voice access finds a control by what it shows: «ES» and «EN», with the language's name as help.
                var spanish = host.Peer("ES", AutomationControlType.RadioButton);
                spanish.GetHelpText().ShouldBe("Español");
                Selection(spanish).IsSelected.ShouldBeTrue();
                host.Peer("EN", AutomationControlType.RadioButton)
                    .GetHelpText()
                    .ShouldBe("English");
            }
        );

    [Fact]
    [Trait("Req", "EDI-006")]
    [Trait("Req", "EDI-001")]
    [Trait("Req", "ACC-001")]
    public void The_fields_of_the_editor_tell_their_example_as_help() =>
        Run(
            "es",
            ControlCenterWindow.DefaultWidth,
            ControlCenterWindow.DefaultHeight,
            AuditLook.Dark,
            (viewModel, _, host) =>
            {
                // Narrator reads the example a field shows while it is empty, not only its name.
                var editor = viewModel.Shortcuts.Editor;
                host.Peer(editor.Model!.Identity.NameLabel, AutomationControlType.Edit)
                    .GetHelpText()
                    .ShouldBe(editor.Model.Identity.Placeholder);
                editor.SetKind(ActionKind.Url);
                WpfThread.DrainPendingWork();
                var target = editor.Model!.Target.ShouldNotBeNull();
                target.Placeholder.ShouldBe("Ej.: ejemplo.com");
                host.Peer(target.Label, AutomationControlType.Edit)
                    .GetHelpText()
                    .ShouldBe(target.Placeholder);
            }
        );

    [Theory]
    [InlineData("es", 1120, 680)]
    [InlineData("en", 760, 520)]
    [InlineData("es", 1371, 859)]
    [Trait("Req", "EDI-008")]
    public void The_groups_of_keys_fill_their_rows_and_never_leave_a_chip_alone(
        string language,
        double width,
        double height
    ) =>
        Run(
            language,
            width,
            height,
            AuditLook.Dark,
            (_, window, host) =>
            {
                window.ShowShortcutPage();
                host.LayOut();
                var chips = InTree(host.Root)
                    .OfType<Clicalo.UI.Wpf.Controls.OptionButton>()
                    .Where(static chip =>
                        chip.Kind == Clicalo.UI.Wpf.Controls.OptionKind.Chip
                        && System.Windows.Media.VisualTreeHelper.GetParent(chip)
                            is System.Windows.Controls.Panel { } panel
                        && string.Equals(
                            panel.GetType().Name,
                            "JustifiedWrap",
                            StringComparison.Ordinal
                        )
                    )
                    .ToList();
                chips.Count.ShouldBeGreaterThan(3);
                var wrap = (System.Windows.FrameworkElement)
                    System.Windows.Media.VisualTreeHelper.GetParent(chips[0]);
                var rows = chips
                    .Select(chip =>
                        (
                            Box: chip.TransformToAncestor(wrap)
                                .TransformBounds(new System.Windows.Rect(chip.RenderSize)),
                            Chip: chip
                        )
                    )
                    .GroupBy(static cell => Math.Round(cell.Box.Top))
                    .ToList();
                foreach (var row in rows)
                {
                    row.Min(static cell => cell.Box.Left).ShouldBe(0, 1);
                    row.Max(static cell => cell.Box.Right).ShouldBe(wrap.ActualWidth, 1);
                    if (rows.Count > 1)
                    {
                        row.Count().ShouldBeGreaterThan(1, "a chip alone under a full row");
                    }
                }
            }
        );

    private static ISelectionItemProvider Selection(AutomationPeer peer) =>
        peer.GetPattern(PatternInterface.SelectionItem)
            .ShouldBeAssignableTo<ISelectionItemProvider>()!;

    /// <summary>
    /// The Control Center with its six sections over fakes, its window arranged for <paramref name="width"/> and its
    /// content hosted at that size; <paramref name="document"/> changes the document of the tests first.
    /// </summary>
    private static void Run(
        string language,
        double width,
        double height,
        AuditLook look,
        Action<ControlCenterViewModel, ControlCenterWindow, AuditHost> audit,
        Action<DocumentStore>? document = null
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

        // In the app the interface follows the language of the document (LanguageFollower): both say the same here too,
        // so «Idioma · Language» marks the language the window is drawn in.
        if (
            !string.Equals(
                language,
                setup.World.Store.Current.Settings.Language.Value,
                StringComparison.Ordinal
            )
        )
        {
            setup
                .World.Store.Dispatch(new SetSetting(SettingPaths.Language, new LangCode(language)))
                .IsSuccess.ShouldBeTrue();
        }

        document?.Invoke(setup.World.Store);
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
                audit(viewModel, window, host);
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

    /// <summary>Gives the tap shortcut <paramref name="id"/> the combination of <paramref name="keys"/>.</summary>
    private static void Rebind(DocumentStore store, string id, params KeyId[] keys)
    {
        store.Current.Library.TryGetShortcut(new ShortcutId(id), out var shortcut).ShouldBeTrue(id);
        store
            .Dispatch(
                new EditShortcut(
                    shortcut with
                    {
                        Action = new TapAction(KeyChord.FromKeys(keys), []),
                    }
                )
            )
            .IsSuccess.ShouldBeTrue(id);
    }
}
