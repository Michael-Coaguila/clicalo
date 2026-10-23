using System.Globalization;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.Panel;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The data of the body tests: a library with General, Word (<c>n</c> tap shortcuts named «Atajo i») and an empty
/// Excel, and an Always visible row of <c>k</c> shortcuts named «Fijo j»; a recorder of the body's intentions.
/// </summary>
internal static class PanelBodyTestData
{
    public static readonly ProfileId Word = new("p-word");
    public static readonly ProfileId Excel = new("p-excel");

    /// <summary>A library with <paramref name="wordCount"/> shortcuts in Word and <paramref name="stripCount"/> in the row.</summary>
    public static ShortcutLibrary Library(int wordCount, int stripCount)
    {
        var word = Enumerable
            .Range(0, wordCount)
            .Select(i => Tap("w" + Number(i), "Atajo " + Number(i)))
            .ToList();
        var strip = Enumerable
            .Range(0, stripCount)
            .Select(i => Tap("a" + Number(i), "Fijo " + Number(i)))
            .ToList();
        var profiles = new ValueList<Profile>([
            Profile(ProfileId.General, "General", "apps", []),
            Profile(Word, "Word", "description", word),
            Profile(Excel, "Excel", "table_chart", []),
        ]);
        return ShortcutLibrary.CreateValidated(new ValueList<Shortcut>([.. strip]), profiles).Value;
    }

    /// <summary>
    /// The library of the gallery (docs/architecture/tooling.md): the shortcuts of the mockup of direction C, with real
    /// names, icons and categories, a Mantener in the row and a macro in Word, so the gallery shows names of every
    /// length, the key lines and the type icons. The ids are those of <see cref="Library"/> («w0»… and «a0»…).
    /// </summary>
    /// <param name="wordCount">How many of the shortcuts of Word it keeps, from the first; all of them by default.</param>
    /// <param name="extraProfiles">How many profiles of other apps it adds after Excel.</param>
    public static ShortcutLibrary GalleryLibrary(
        int wordCount = int.MaxValue,
        int extraProfiles = 0
    )
    {
        (string Name, string Icon, string Category)[] word =
        [
            ("Negrita", "format_bold", "fmt"),
            ("Cursiva", "format_italic", "fmt"),
            ("Subrayar", "format_underlined", "fmt"),
            ("Título 1", "title", "fmt"),
            ("Viñetas", "format_list_bulleted", "fmt"),
            ("Reemplazar", "find_replace", "sel"),
            ("Guardar PDF", "picture_as_pdf", "file"),
            ("Ortografía", "spellcheck", "edit"),
            ("Dictado", "mic", "voice"),
            ("Buscar", "search", "nav"),
            ("Deshacer", "undo", "hist"),
            ("Rehacer", "redo", "hist"),
            ("Copiar formato", "format_paint", "fmt"),
            ("Insertar comentario", "add_comment", "edit"),
        ];
        (string Name, string Icon, string Category)[] strip =
        [
            ("Dictar", "mic", "voice"),
            ("Hablar", "record_voice_over", "voice"),
            ("Silenciar", "mic_off", "voice"),
            ("Escritorio", "desktop_windows", "win"),
        ];
        var shortcuts = word.Take(wordCount)
            .Select(
                (s, i) =>
                    Gallery(
                        "w" + Number(i),
                        s,
                        i == 6
                            ? new MacroAction([
                                .. Enumerable.Repeat<MacroStep>(
                                    new WaitStep(TimeSpan.FromSeconds(1)),
                                    5
                                ),
                            ])
                            : new TapAction(KeyChord.Empty, [])
                    )
            )
            .ToList();
        var row = strip
            .Select(
                (s, i) =>
                    Gallery(
                        "a" + Number(i),
                        s,
                        i == 1 ? new HoldAction(KeyChord.Empty) : new TapAction(KeyChord.Empty, [])
                    )
            )
            .ToList();
        (string Name, string Icon)[] others =
        [
            ("Navegador", "public"),
            ("Correo", "mail"),
            ("Bloc de notas", "edit_note"),
            ("Explorador", "folder"),
        ];
        var profiles = new ValueList<Profile>([
            Profile(ProfileId.General, "General", "apps", []),
            Profile(Word, "Word", "description", shortcuts),
            Profile(Excel, "Excel", "table_chart", []),
            .. others
                .Take(extraProfiles)
                .Select(
                    static (p, i) =>
                        Profile(new ProfileId("p-other" + Number(i)), p.Name, p.Icon, [])
                ),
        ]);
        return ShortcutLibrary.CreateValidated(new ValueList<Shortcut>([.. row]), profiles).Value;
    }

    /// <summary>The key lines of the gallery: the combinations of the mockup of direction C, «Ctrl+Shift+L» style.</summary>
    public static TileKeyLine GalleryKeys(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        var line = shortcut.Id.Value switch
        {
            "w0" => "Ctrl+N",
            "w1" => "Ctrl+K",
            "w2" => "Ctrl+S",
            "w3" => "Ctrl+Alt+1",
            "w4" => "Ctrl+Shift+L",
            "w5" => "Ctrl+L",
            "w6" => "5 pasos",
            "w7" => "F7",
            "w8" => "Win+H",
            "w9" => "Ctrl+B",
            "w10" => "Ctrl+Z",
            "w11" => "Ctrl+Y",
            "w12" => "Ctrl+Shift+C",
            "w13" => "Ctrl+Alt+M",
            _ => string.Empty,
        };
        return new TileKeyLine(line, line);
    }

    /// <summary>A panel on <paramref name="view"/> of <paramref name="library"/>, with M, 3 columns and automatic rows.</summary>
    public static PanelViewModel Panel(
        ShortcutLibrary library,
        ProfileId view,
        IPanelBodyIntents intents,
        string language = "es",
        Func<Shortcut, TileKeyLine>? keys = null
    )
    {
        var controller = new PanelInteractionController(
            new PanelEngineInbox(),
            () => 1,
            TimeProvider.System
        );
        var panel = new PanelViewModel(
            controller,
            PanelTestData.Localization(language),
            PanelDesktopFixture.Touch,
            _ => null,
            intents,
            null
        );
        panel.Apply(PanelProjector.Project(library, view, LangCode.Es, LangCode.Es, keys));
        return panel;
    }

    /// <summary>The engine holding a contact on <paramref name="shortcut"/>: the panic strip shows.</summary>
    public static EngineSnapshot Holding(ShortcutId shortcut) =>
        EngineSnapshot.Empty with
        {
            Held = new ValueList<PressedItem>([
                new PressedItem(
                    HolderId.ForContact(4),
                    HoldOrigin.Contact,
                    shortcut,
                    4,
                    [],
                    MouseButtons.None,
                    0,
                    null
                ),
            ]),
            Version = 1,
        };

    private static string Number(int i) => i.ToString(CultureInfo.InvariantCulture);

    private static Profile Profile(
        ProfileId id,
        string name,
        string icon,
        IEnumerable<Shortcut> shortcuts
    ) =>
        new(
            id,
            new LocalizedText([
                KeyValuePair.Create(LangCode.Es, name),
                KeyValuePair.Create(LangCode.En, name),
            ]),
            new IconRef(icon),
            AutoIcon: false,
            new AppBinding.Manual(),
            InjectionMode.VirtualKey,
            new ValueList<Shortcut>([.. shortcuts]),
            Origin: null
        );

    private static Shortcut Gallery(
        string id,
        (string Name, string Icon, string Category) shortcut,
        ShortcutAction action
    ) =>
        PanelTestData.Shortcut(new ShortcutId(id), shortcut.Name, shortcut.Name, action) with
        {
            Icon = new IconRef(shortcut.Icon),
            Category = new CategoryId(shortcut.Category),
        };

    private static Shortcut Tap(string id, string name) =>
        PanelTestData.Shortcut(new ShortcutId(id), name, name, new TapAction(KeyChord.Empty, []));
}
