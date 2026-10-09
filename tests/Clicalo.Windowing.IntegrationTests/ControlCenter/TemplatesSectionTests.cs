using System.Collections.Immutable;
using System.IO;
using System.Windows;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases.Ai;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Application.UseCases.Templates;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Sharing;
using Clicalo.Domain.Templates;
using Clicalo.Presentation.ControlCenter;
using Clicalo.Presentation.ControlCenter.Templates;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// The section Plantillas headless (docs/05 §2): the view models project the AI card, the keyboard line, the open apps
/// without a profile, the available templates, «Tus perfiles» and the preview, and the window draws them. Nothing
/// leaves the machine: the AI is a canned generator and the key lives in memory. With <c>CLICALO_CC_PREVIEW=1</c> it
/// writes PNG previews to <c>artifacts/cc-preview</c>.
/// </summary>
public sealed class TemplatesSectionTests
{
    private static readonly ProcessName Notepad = new("notepad.exe");

    [Fact]
    [Trait("Req", "PLA-001")]
    [Trait("Req", "PLA-009")]
    [Trait("Req", "PLA-011")]
    [Trait("Req", "PLA-012")]
    [Trait("Req", "PLA-014")]
    public void The_section_projects_its_parts_in_order()
    {
        var setup = new Setup();
        var (window, theme, viewModel) = setup.Build();
        try
        {
            WpfThread.Invoke(() =>
            {
                viewModel.Select(ControlCenterSection.Templates);
                WpfThread.DrainPendingWork();
                var screen = viewModel.Templates.ShouldNotBeNull().Screen;
                screen.Title.ShouldBe("Empieza con atajos listos");
                screen.Ai.CanGenerate.ShouldBeFalse("Generar is disabled with the field empty");
                screen.Ai.Examples.ShouldBe([
                    "Photoshop",
                    "Spotify",
                    "Teams",
                    "Canva",
                    "WhatsApp",
                    "OBS",
                ]);
                screen.Ai.KeyStatus.ShouldBe(
                    "Sin clave: la IA usa tu propia clave de API de Anthropic"
                );
                screen.Ai.Keyboard.Line.ShouldBe(
                    "Para Español (Latinoamérica) · Office y apps en español"
                );
                screen.Ai.Keyboard.Layouts.Count.ShouldBe(4);
                screen.Suggested.Cards.ShouldHaveSingleItem().Id.ShouldBe("notes");
                screen.Available.Select(c => c.Id).ShouldBe(["excel"]);
                screen
                    .Installed.Profiles.Any(p =>
                        string.Equals(p.Name, "Word", StringComparison.Ordinal)
                    )
                    .ShouldBeTrue();
                screen.Preview.HasContent.ShouldBeFalse();
                window.Content.ShouldNotBeNull();
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "PLA-004")]
    [Trait("Req", "PLA-005")]
    [Trait("Req", "PLA-017")]
    public void Generating_asks_for_consent_then_shows_the_proposal_and_creates_the_profile()
    {
        var setup = new Setup();
        var (window, theme, viewModel) = setup.Build();
        try
        {
            WpfThread.Invoke(() =>
            {
                var templates = viewModel.Templates!;
                viewModel.Select(ControlCenterSection.Templates);
                templates.SetQuery("WhatsApp");
                templates.SaveKey("sk-test");
                _ = templates.GenerateAsync();
                WpfThread.DrainPendingWork();
                templates.Screen.Ai.Consent.ShouldNotBeNull();
                setup.Generator.Requests.ShouldBeEmpty("nothing is sent before the consent");

                _ = templates.AcceptConsentAsync();
                WpfThread.DrainPendingWork();
                var preview = templates.Screen.Preview;
                preview.HasContent.ShouldBeTrue();
                preview.Name.ShouldBe("WhatsApp");
                preview.ButtonText.ShouldBe("Crear perfil con 2 atajos");
                preview.Rows[1].Warning.ShouldBe("Cierra o borra al instante");

                templates.InstallPreview();
                WpfThread.DrainPendingWork();
                setup
                    .World.Store.Current.Library.ProfileFor(new ProcessName("WhatsApp.exe"))
                    .ShouldNotBeNull();
                viewModel.Section.ShouldBe(ControlCenterSection.Templates);
                setup.Notices.ShouldContain(n => n.CanUndo);
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "PLA-006")]
    public void Without_a_key_the_error_card_offers_three_ways_out()
    {
        var setup = new Setup(consent: true);
        var (window, theme, viewModel) = setup.Build();
        try
        {
            WpfThread.Invoke(() =>
            {
                var templates = viewModel.Templates!;
                templates.SetQuery("Spotify");
                _ = templates.GenerateAsync();
                WpfThread.DrainPendingWork();
                var error = templates.Screen.Ai.Error.ShouldNotBeNull();
                error.Title.ShouldBe("Falta tu clave");
                error
                    .Actions.Select(a => a.Kind)
                    .ShouldBe([
                        ErrorActionKind.Key,
                        ErrorActionKind.Blank,
                        ErrorActionKind.Templates,
                    ]);

                _ = templates.ErrorActionAsync(ErrorActionKind.Blank);
                WpfThread.DrainPendingWork();
                templates.Screen.Ai.Error.ShouldBeNull();
                templates.Screen.Blank.Open.ShouldBeTrue();
                templates.Screen.Blank.Name.ShouldBe(
                    "Spotify",
                    "the empty profile starts with the name typed"
                );
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "PLA-013")]
    [Trait("Req", "PLA-015")]
    public void A_template_card_opens_its_preview_and_installs_the_checked_shortcuts()
    {
        var setup = new Setup();
        var (window, theme, viewModel) = setup.Build();
        try
        {
            WpfThread.Invoke(() =>
            {
                var templates = viewModel.Templates!;
                viewModel.Select(ControlCenterSection.Templates);
                WpfThread.DrainPendingWork();
                templates.PreviewTemplate("notes");
                WpfThread.DrainPendingWork();
                templates.Screen.Preview.Rows.Count.ShouldBe(2);
                templates.Screen.Suggested.Cards.ShouldHaveSingleItem().Selected.ShouldBeTrue();
                templates.ToggleRow(1);
                WpfThread.DrainPendingWork();
                templates.Screen.Preview.ButtonText.ShouldBe("Instalar 1 atajos");

                templates.InstallPreview();
                WpfThread.DrainPendingWork();
                setup
                    .World.Store.Current.Library.ProfileFor(Notepad)
                    .ShouldNotBeNull()
                    .Shortcuts.Count.ShouldBe(1);
                templates.Screen.Suggested.Cards.ShouldBeEmpty("Notepad has its profile now");
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

        var setup = new Setup();
        var (window, theme, viewModel) = setup.Build();
        try
        {
            WpfThread.Invoke(() => viewModel.Select(ControlCenterSection.Templates));
            Preview(window, theme, "templates-base", 1120, 1600);
            WpfThread.Invoke(() =>
            {
                viewModel.Templates!.PreviewTemplate("notes");
                viewModel.Templates.ToggleKeyboard();
                viewModel.Templates.ToggleBlank();
            });
            Preview(window, theme, "templates", 1120, 1600);
            WpfThread.Invoke(() =>
            {
                viewModel.Templates!.SetQuery("Raro");
                _ = viewModel.Templates.GenerateAsync();
            });
            Preview(window, theme, "templates-error", 1120, 900);
        }
        finally
        {
            Close(window, theme);
        }
    }

    private static void Preview(
        ControlCenterWindow window,
        ThemeService theme,
        string name,
        double width,
        double height
    )
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
            new RenderSnapshotOptions { Width = width, Height = height }
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

    private static void Close(ControlCenterWindow window, ThemeService theme) =>
        WpfThread.Invoke(() =>
        {
            window.Destroy();
            theme.Dispose();
        });

    private static TemplateShortcut Item(string id, string name, params KeyId[] keys) =>
        new(
            id,
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            new CategoryId("edit"),
            new TapAction(KeyChord.FromKeys(keys), []),
            false
        );

    private sealed class Setup
    {
        public Setup(bool consent = false)
        {
            World = new ControlCenterTestWorld();
            if (consent)
            {
                _ = World.Store.Dispatch(
                    new Clicalo.Domain.Commands.SetSetting(SettingPaths.AiConsent, true)
                );
            }

            var templates = new TemplatesServices(
                new TemplatePreviewSession(World.Store),
                new AiAssistant(World.Store, Generator, Keys, World.Time),
                new NoSharing(),
                () => KeyboardLayouts.SpanishLatinAmerica,
                () => LangCode.Es,
                _ => ValueTask.FromResult<ReadOnlyMemory<byte>?>(null),
                (_, _, _) => ValueTask.FromResult<bool?>(null),
                Notices.Add
            );
            Services = World.Services with
            {
                Catalogs = () => World.Catalogs with { Starter = Content() },
                OpenApps = _ =>
                    ValueTask.FromResult<ImmutableArray<OpenApp>>([
                        new(
                            new ProcessName("WINWORD.EXE"),
                            "Word",
                            new WindowToken(1),
                            false,
                            null
                        ),
                        new(Notepad, "Bloc de notas", new WindowToken(3), false, null),
                    ]),
                Templates = templates,
            };
        }

        public ControlCenterTestWorld World { get; }

        public ControlCenterServices Services { get; }

        public Canned Generator { get; } = new();

        public MemoryKeys Keys { get; } = new();

        public List<WorkspaceNotice> Notices { get; } = [];

        public (
            ControlCenterWindow Window,
            ThemeService Theme,
            ControlCenterViewModel ViewModel
        ) Build() =>
            WpfThread.Invoke(() =>
            {
                var theme = new ThemeService(
                    new FakeSystemTheme(),
                    ThemeChoice.Dark,
                    100,
                    reduceMotion: true
                );
                World.Shortcuts.Open(
                    new ListRef.InProfile(ControlCenterTestWorld.Word),
                    null,
                    false
                );
                var viewModel = new ControlCenterViewModel(Services, () => { });
                World.Store.Changed += (_, _) =>
                {
                    World.Shortcuts.OnDocumentChanged();
                    viewModel.Shortcuts.Invalidate();
                    viewModel.Refresh();
                };
                var window = new ControlCenterWindow(viewModel, theme);
                WpfThread.DrainPendingWork();
                return (window, theme, viewModel);
            });

        private static StarterContent Content() =>
            new(
                new StarterKit(
                    1,
                    [
                        new StarterOption("notes", StarterOptionKind.Template, false),
                        new StarterOption("excel", StarterOptionKind.Template, false),
                    ]
                ),
                new SeedContent(
                    1,
                    [],
                    LocalizedText.Same("General", LangCode.Es, LangCode.En),
                    new IconRef("apps"),
                    []
                ),
                [
                    new ProfileTemplate(
                        "notes",
                        1,
                        LocalizedText.Same("Bloc de notas", LangCode.Es, LangCode.En),
                        new IconRef("edit_note"),
                        [Notepad],
                        [
                            Item("save", "Guardar", KeyIds.Ctrl, KeyIds.G),
                            Item("find", "Buscar", KeyIds.Ctrl, KeyIds.B),
                        ]
                    ),
                    new ProfileTemplate(
                        "excel",
                        1,
                        LocalizedText.Same("Excel", LangCode.Es, LangCode.En),
                        new IconRef("table_chart"),
                        [new ProcessName("excel.exe")],
                        [Item("sum", "Autosuma", KeyIds.Alt, KeyIds.F2)]
                    ),
                ]
            );
    }

    private sealed class Canned : ITemplateGenerator
    {
        public List<TemplateRequest> Requests { get; } = [];

        public ValueTask<TemplateGeneration> GenerateAsync(
            TemplateRequest request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(request);
            return ValueTask.FromResult(
                TemplateGeneration.Ok(
                    new AiTemplateProposal(
                        true,
                        request.AppName,
                        request.AppName + ".exe",
                        "chat",
                        [
                            new AiProposedShortcut(
                                "Nuevo chat",
                                "New chat",
                                "add",
                                ["ctrl", "n"],
                                "file",
                                0.9
                            ),
                            new AiProposedShortcut(
                                "Cerrar",
                                "Close",
                                "close",
                                ["ctrl", "w"],
                                "win",
                                0.6
                            ),
                        ]
                    )
                )
            );
        }
    }

    private sealed class MemoryKeys : IAiKeyStore
    {
        private Sensitive<string>? _key;

        public string Target => "Clicalo/ai/test";

        public bool HasKey() => _key is not null;

        public bool Save(Sensitive<string> key)
        {
            _key = key;
            return true;
        }

        public Sensitive<string>? Read() => _key;

        public bool Delete()
        {
            _key = null;
            return true;
        }
    }

    private sealed class NoSharing : IProfileSharing
    {
        public SharedProfileFile Export(Profile profile, bool includeTextsInClear) =>
            new("clicalo-perfil-x.json", ReadOnlyMemory<byte>.Empty, 0);

        public Result<SharedProfile> Import(ReadOnlyMemory<byte> utf8) =>
            Results.Fail<SharedProfile>(
                new Failure(
                    "x",
                    Clicalo.Domain.Messages.L.ImportInvalid,
                    FailureSeverity.Warning,
                    FailureRecovery.None,
                    FailureAnnouncement.Polite
                )
            );
    }
}
