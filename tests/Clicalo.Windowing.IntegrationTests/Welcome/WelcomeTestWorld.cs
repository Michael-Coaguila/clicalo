using Clicalo.Application.Localization;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases.Welcome;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.SearchPanel;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Windowing.IntegrationTests.Welcome;

/// <summary>
/// The data of the welcome tests: the starter kit of <c>data/content/starter.json</c> («Basics» marked and the nine
/// templates with their names, icons and programs, one shortcut each) on a first start with General and Always
/// visible empty, in Spanish. Nothing touches the desktop.
/// </summary>
internal sealed class WelcomeTestWorld
{
    private static readonly (
        string Id,
        string Icon,
        string Es,
        string En,
        string[] Processes
    )[] Templates =
    [
        ("word", "description", "Word", "Word", ["winword.exe"]),
        ("browser", "public", "Navegador", "Browser", ["chrome.exe", "msedge.exe", "firefox.exe"]),
        ("vscode", "code", "VS Code", "VS Code", ["code.exe"]),
        ("excel", "table_chart", "Excel", "Excel", ["excel.exe"]),
        ("ppt", "slideshow", "PowerPoint", "PowerPoint", ["powerpnt.exe"]),
        ("zoom", "videocam", "Zoom", "Zoom", ["zoom.exe"]),
        ("explorer", "folder", "Explorador", "Explorer", ["explorer.exe"]),
        ("outlook", "mail", "Correo", "Mail", ["outlook.exe", "olk.exe"]),
        ("notepad", "edit_note", "Bloc de notas", "Notepad", ["notepad.exe"]),
    ];

    public WelcomeTestWorld(bool repeat = false, string language = "es")
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        Localization = PanelTestData.Localization(language);
        var document = UserDocument.Create(
            ShortcutLibrary
                .CreateValidated(
                    [],
                    [
                        new Profile(
                            ProfileId.General,
                            LocalizedText.Same("General", LangCode.Es, LangCode.En),
                            new IconRef("apps"),
                            false,
                            new AppBinding.Manual(),
                            InjectionMode.VirtualKey,
                            [],
                            null
                        ),
                    ]
                )
                .Value,
            SettingsSchema.Defaults with
            {
                Language = new LangCode(language),
                Theme = ThemeChoice.Dark,
            }
        );
        Store = new DocumentStore(document, new Ids(), new SearchTestWorld.NoBackups(), Time);
        Store.Changed += (_, _) =>
            _ = Localization.TrySetLanguage(Store.Current.Settings.Language.Value);
        Session = new WelcomeSession(Store, Content(), repeat);
    }

    public FakeTimeProvider Time { get; }

    public LocalizationContext Localization { get; }

    public DocumentStore Store { get; }

    public WelcomeSession Session { get; }

    public static StarterContent Content() =>
        new(
            new StarterKit(
                1,
                [
                    new StarterOption(
                        "basics",
                        StarterOptionKind.Basics,
                        true,
                        new IconRef("apps"),
                        L.KitBasics.Key,
                        L.KitBasicsD.Key
                    ),
                    .. Templates.Select(t => new StarterOption(
                        t.Id,
                        StarterOptionKind.Template,
                        false
                    )),
                ]
            ),
            new SeedContent(
                1,
                [Item("copy", KeyIds.Ctrl, KeyIds.C)],
                LocalizedText.Same("General", LangCode.Es, LangCode.En),
                new IconRef("apps"),
                [Item("undo", KeyIds.Ctrl, KeyIds.Z)]
            ),
            [
                .. Templates.Select(t => new ProfileTemplate(
                    t.Id,
                    1,
                    new LocalizedText([new(LangCode.Es, t.Es), new(LangCode.En, t.En)]),
                    new IconRef(t.Icon),
                    [.. t.Processes.Select(p => new ProcessName(p))],
                    [Item(t.Id + "-save", KeyIds.Ctrl, KeyIds.G)]
                )),
            ]
        );

    private static TemplateShortcut Item(string id, params KeyId[] keys) =>
        new(
            id,
            LocalizedText.Same(id, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            new CategoryId("edit"),
            new TapAction(KeyChord.FromKeys(keys), []),
            false
        );

    private sealed class Ids : IIdGenerator
    {
        private int _next;

        public ProfileId NewProfileId() => new("p" + Interlocked.Increment(ref _next));

        public ShortcutId NewShortcutId() => new("s" + Interlocked.Increment(ref _next));
    }
}
