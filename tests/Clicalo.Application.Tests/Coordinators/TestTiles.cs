using Clicalo.Application.Coordinators;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Tests.Coordinators;

/// <summary>
/// Shortcuts of the three M2 kinds for the controller tests. The combination is empty: the controller never reads it
/// (the engine plans the keys), and <c>KeyChord.Create</c> belongs to the domain package.
/// </summary>
internal static class TestTiles
{
    public static readonly ProfileId Word = new("p-word");

    public static TileBinding Tap(string id = "copy") =>
        Bind(id, new TapAction(KeyChord.Empty, []), InjectionMode.VirtualKey);

    public static TileBinding Hold(string id = "ptt") =>
        Bind(id, new HoldAction(KeyChord.Empty), InjectionMode.ScanCode);

    public static TileBinding Toggle(string id = "shift") =>
        Bind(id, new ToggleAction(KeyChord.Empty), InjectionMode.VirtualKey);

    public static Shortcut Shortcut(string id, ShortcutAction action) =>
        new(
            new ShortcutId(id),
            LocalizedText.Same(id, LangCode.Es, LangCode.En),
            new IconRef("keyboard"),
            AutoIcon: false,
            new CategoryId("edit"),
            action,
            new ShortcutOptions(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false),
            Origin: null,
            PinnedFrom: null
        );

    private static TileBinding Bind(string id, ShortcutAction action, InjectionMode mode) =>
        new(Shortcut(id, action), Word, mode);
}
