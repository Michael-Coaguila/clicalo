using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Application.UseCases.Library;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Presentation.ControlCenter;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.SearchPanel;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// The data of the Control Center tests: Always visible «Copiar»; General «Deshacer»; Word (winword.exe) with
/// «Negrita» (Ctrl+N), «Cursiva» (Ctrl+K), «Subrayar» (Ctrl+S), a Hold, a macro and an incomplete one; Navegador
/// (chrome.exe) with «Copiar» repeated by name. The key labels, icons and mouse actions are the real catalogs of
/// <c>data/catalogs</c>; the library has two ready actions. Nothing touches the desktop: the foreground refuses every
/// lease and the open apps are a fixed list.
/// </summary>
internal sealed class ControlCenterTestWorld
{
    public static readonly ProfileId Word = new("word");

    public ControlCenterTestWorld()
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        Store = new DocumentStore(Document(), new Ids(), new SearchTestWorld.NoBackups(), Time);
        Localization = PanelTestData.Localization("es");
        Catalogs = LoadCatalogs();
        Shortcuts = new ShortcutsWorkspace(Store, Localization, () => Catalogs, () => Word);
        Profiles = new ProfileWorkspace(Store, Shortcuts, () => Catalogs);
        Services = new ControlCenterServices(
            Store,
            Shortcuts,
            Profiles,
            Localization,
            () => Catalogs,
            new TwoStepConfirm(Time),
            Time,
            action => _ = WpfThread.Dispatcher.BeginInvoke(action),
            () => Store.Current.Library.TryGetProfile(Word, out var word) ? word : null,
            _ =>
                ValueTask.FromResult<ImmutableArray<OpenApp>>([
                    new(new ProcessName("WINWORD.EXE"), "Word", new WindowToken(1), false, null),
                    new(new ProcessName("chrome.exe"), "Chrome", new WindowToken(2), false, null),
                ]),
            () => new ProcessName("WINWORD.EXE"),
            _ => ValueTask.FromResult(false),
            (_, _, _) => ValueTask.FromResult(TryNowOutcome.Asked),
            () => { }
        );
    }

    public FakeTimeProvider Time { get; }

    public DocumentStore Store { get; }

    public Clicalo.Application.Localization.LocalizationContext Localization { get; }

    public EditorCatalogs Catalogs { get; }

    public ShortcutsWorkspace Shortcuts { get; }

    public ProfileWorkspace Profiles { get; }

    public ControlCenterServices Services { get; }

    public static UserDocument Document() =>
        UserDocument.Create(
            ShortcutLibrary
                .CreateValidated(
                    [Tap("copy", "Copiar", "Copy", "content_copy", "edit", KeyIds.Ctrl, KeyIds.C)],
                    [
                        Profile(
                            ProfileId.General,
                            "General",
                            "apps",
                            null,
                            Tap("undo", "Deshacer", "Undo", "undo", "hist", KeyIds.Ctrl, KeyIds.Z)
                        ),
                        Profile(
                            Word,
                            "Word",
                            "description",
                            "WINWORD.EXE",
                            Tap(
                                "bold",
                                "Negrita",
                                "Bold",
                                "format_bold",
                                "fmt",
                                KeyIds.Ctrl,
                                KeyIds.N
                            ),
                            Tap(
                                "italic",
                                "Cursiva",
                                "Italic",
                                "format_italic",
                                "fmt",
                                KeyIds.Ctrl,
                                KeyIds.K
                            ),
                            Tap(
                                "under",
                                "Subrayar",
                                "Underline",
                                "format_underlined",
                                "fmt",
                                KeyIds.Ctrl,
                                KeyIds.S
                            ),
                            Shortcut(
                                "talk",
                                "Hablar",
                                "Talk",
                                "record_voice_over",
                                "voice",
                                new HoldAction(KeyChord.FromKeys(KeyIds.Win, KeyIds.H))
                            ),
                            Shortcut(
                                "macro",
                                "Copiar y pegar",
                                "Copy and paste",
                                "playlist_play",
                                "edit",
                                new MacroAction([
                                    new KeysStep(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C)),
                                    new WaitStep(TimeSpan.FromMilliseconds(500)),
                                    new KeysStep(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.V)),
                                ])
                            ),
                            Shortcut(
                                "empty",
                                "",
                                "",
                                "bolt",
                                "edit",
                                new TapAction(KeyChord.Empty, [])
                            )
                        ),
                        Profile(
                            new ProfileId("chrome"),
                            "Navegador",
                            "public",
                            "chrome.exe",
                            Tap(
                                "ccopy",
                                "Copiar",
                                "Copy",
                                "content_copy",
                                "edit",
                                KeyIds.Ctrl,
                                KeyIds.C
                            )
                        ),
                    ]
                )
                .Value,
            SettingsSchema.Defaults
        );

    private static Shortcut Tap(
        string id,
        string es,
        string en,
        string icon,
        string category,
        params KeyId[] keys
    ) => Shortcut(id, es, en, icon, category, new TapAction(KeyChord.FromKeys(keys), []));

    private static Shortcut Shortcut(
        string id,
        string es,
        string en,
        string icon,
        string category,
        ShortcutAction action
    ) =>
        new(
            new ShortcutId(id),
            new LocalizedText([new(LangCode.Es, es), new(LangCode.En, en)]),
            new IconRef(icon),
            AutoIcon: true,
            new CategoryId(category),
            action,
            new ShortcutOptions(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false),
            Origin: null,
            PinnedFrom: null
        );

    private static Profile Profile(
        ProfileId id,
        string name,
        string icon,
        string? process,
        params Shortcut[] shortcuts
    ) =>
        new(
            id,
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef(icon),
            AutoIcon: false,
            process is null
                ? new AppBinding.Manual()
                : new AppBinding.Processes([new ProcessName(process)]),
            InjectionMode.VirtualKey,
            [.. shortcuts],
            Origin: null
        );

    private static EditorCatalogs LoadCatalogs()
    {
        var folder = Path.Combine(RepoPaths.Data, "catalogs");
        return new EditorCatalogs(
            Icons(folder),
            ComboIconTable.Empty,
            new LibraryContent(
                1,
                [
                    new LibrarySection(
                        "edit",
                        [
                            new TemplateShortcut(
                                "paste",
                                new LocalizedText([
                                    new(LangCode.Es, "Pegar"),
                                    new(LangCode.En, "Paste"),
                                ]),
                                new IconRef("content_paste"),
                                new CategoryId("edit"),
                                new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.V), []),
                                false
                            ),
                        ]
                    ),
                ]
            ),
            null,
            KeyLabels(folder),
            Mouse(folder)
        );
    }

    private static IconCatalog Icons(string folder)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "icons.json")));
        var root = json.RootElement;
        return new IconCatalog(
            root.GetProperty("icons")
                .EnumerateArray()
                .Select(icon => new IconEntry(
                    new IconRef(icon.GetProperty("id").GetString()!),
                    [
                        .. icon.GetProperty("tags")
                            .EnumerateObject()
                            .SelectMany(l => l.Value.EnumerateArray().Select(t => t.GetString()!)),
                    ]
                ))
                .ToList(),
            root.GetProperty("featured")
                .EnumerateArray()
                .Select(i => new IconRef(i.GetString()!))
                .ToList(),
            root.GetProperty("profileFeatured")
                .EnumerateArray()
                .Select(i => new IconRef(i.GetString()!))
                .ToList()
        );
    }

    private static KeyLabelCatalog KeyLabels(string folder)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "keys.json")));
        return new KeyLabelCatalog(
            json.RootElement.GetProperty("keys")
                .EnumerateArray()
                .Select(key =>
                    KeyValuePair.Create(
                        new KeyId(key.GetProperty("id").GetString()!),
                        new KeyLabel(
                            Text(key.GetProperty("label")),
                            key.TryGetProperty("short", out var shortLabel)
                                ? Text(shortLabel)
                                : null,
                            key.TryGetProperty("spoken", out var spoken) ? Text(spoken) : null
                        )
                    )
                )
                .ToList()
        );
    }

    private static ValueList<MouseActionInfo> Mouse(string folder)
    {
        var ops = new Dictionary<string, MouseOp>(StringComparer.Ordinal)
        {
            ["rclick"] = MouseOp.RightClick,
            ["dbl"] = MouseOp.DoubleClick,
            ["mid"] = MouseOp.MiddleClick,
            ["drag"] = MouseOp.Drag,
            ["sup"] = MouseOp.ScrollUp,
            ["sdn"] = MouseOp.ScrollDown,
            ["sleft"] = MouseOp.ScrollLeft,
            ["sright"] = MouseOp.ScrollRight,
        };
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "mouse.json")));
        return
        [
            .. json
                .RootElement.GetProperty("actions")
                .EnumerateArray()
                .Select(action => new MouseActionInfo(
                    ops[action.GetProperty("id").GetString()!],
                    new IconRef(action.GetProperty("icon").GetString()!),
                    Text(action.GetProperty("label")),
                    action.TryGetProperty("spoken", out var spoken)
                        ? Text(spoken)
                        : Text(action.GetProperty("label"))
                )),
        ];
    }

    private static LocalizedText Text(JsonElement text) =>
        new(
            text.EnumerateObject()
                .Select(e => KeyValuePair.Create(new LangCode(e.Name), e.Value.GetString()!))
        );

    private sealed class Ids : IIdGenerator
    {
        private int _next;

        public ProfileId NewProfileId() =>
            new(
                "p"
                    + Interlocked
                        .Increment(ref _next)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture)
            );

        public ShortcutId NewShortcutId() =>
            new(
                "s"
                    + Interlocked
                        .Increment(ref _next)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
    }
}
