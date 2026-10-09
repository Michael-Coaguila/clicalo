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

    /// <summary>A panel on <paramref name="view"/> of <paramref name="library"/>, with M, 3 columns and automatic rows.</summary>
    public static PanelViewModel Panel(
        ShortcutLibrary library,
        ProfileId view,
        IPanelBodyIntents intents,
        string language = "es"
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
        panel.Apply(PanelProjector.Project(library, view, LangCode.Es, LangCode.Es));
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

    private static Shortcut Tap(string id, string name) =>
        PanelTestData.Shortcut(new ShortcutId(id), name, name, new TapAction(KeyChord.Empty, []));
}
