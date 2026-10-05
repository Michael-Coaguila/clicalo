using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Domain.Tests.Templates;

/// <summary>
/// A small starter kit built in code: «Basics» (two Always visible shortcuts and two of General), a Word template whose
/// Bold changes with the programs language, a Browser template with two processes and a Chat template that also claims
/// one of them.
/// </summary>
internal static class StarterFixture
{
    public static StarterContent Content { get; } =
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
                    new StarterOption("word", StarterOptionKind.Template, false),
                    new StarterOption("browser", StarterOptionKind.Template, false),
                    new StarterOption("chat", StarterOptionKind.Template, false),
                ]
            ),
            new SeedContent(
                3,
                [Item("dict", Tap(KeyIds.Win, KeyIds.H)), Item("desk", Tap(KeyIds.Win, KeyIds.D))],
                Text("General"),
                new IconRef("apps"),
                [
                    Item("copy", Tap(KeyIds.Ctrl, KeyIds.C)),
                    Item("close", Tap(KeyIds.Alt, KeyIds.F4), true),
                ]
            ),
            [
                new ProfileTemplate(
                    "word",
                    2,
                    Text("Word"),
                    new IconRef("description"),
                    [new ProcessName("winword.exe")],
                    [
                        Item(
                            "bold",
                            new TapAction(
                                Chord(KeyIds.Ctrl, KeyIds.N),
                                [new ChordVariant(LangCode.En, Chord(KeyIds.Ctrl, KeyIds.B))]
                            )
                        ),
                        Item("spell", Tap(KeyIds.F7)),
                    ]
                ),
                new ProfileTemplate(
                    "browser",
                    1,
                    Text("Browser"),
                    new IconRef("public"),
                    [new ProcessName("chrome.exe"), new ProcessName("msedge.exe")],
                    [Item("newtab", Tap(KeyIds.Ctrl, KeyIds.T))]
                ),
                new ProfileTemplate(
                    "chat",
                    1,
                    Text("Chat"),
                    new IconRef("chat"),
                    [new ProcessName("MSEDGE.EXE"), new ProcessName("chat.exe")],
                    [Item("send", Tap(KeyIds.Ctrl, KeyIds.Enter))]
                ),
            ]
        );

    public static KeyChord Chord(params KeyId[] keys) => KeyChord.FromKeys(keys);

    private static TapAction Tap(params KeyId[] keys) => new(Chord(keys), []);

    private static TemplateShortcut Item(string id, ShortcutAction action, bool confirm = false) =>
        new(id, Text(id), new IconRef("bolt"), new CategoryId("edit"), action, confirm);

    private static LocalizedText Text(string text) =>
        LocalizedText.Same(text, LangCode.Es, LangCode.En);
}
