using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Tests.Generators;

namespace Clicalo.Domain.Tests.Library;

/// <summary>Small hand-made shortcuts, profiles and libraries for table tests.</summary>
internal static class LibraryBuilder
{
    public static Shortcut Shortcut(string id, string name, ShortcutAction action) =>
        new(
            new ShortcutId(id),
            new LocalizedText([new(LangCode.Es, name), new(LangCode.En, name)]),
            new IconRef("bolt"),
            true,
            new CategoryId("edit"),
            action,
            new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
            null,
            null
        );

    public static Shortcut Tap(string id, string name, params ReadOnlySpan<KeyId> keys) =>
        Shortcut(id, name, new TapAction(KeyChord.FromKeys(keys), []));

    public static Shortcut Named(
        string id,
        string es,
        string en,
        params ReadOnlySpan<KeyId> keys
    ) =>
        new(
            new ShortcutId(id),
            new LocalizedText([new(LangCode.Es, es), new(LangCode.En, en)]),
            new IconRef("bolt"),
            true,
            new CategoryId("edit"),
            new TapAction(KeyChord.FromKeys(keys), []),
            new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
            null,
            null
        );

    public static Profile Profile(
        string id,
        string name,
        string? process,
        params ReadOnlySpan<Shortcut> shortcuts
    ) =>
        new(
            new ProfileId(id),
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef("apps"),
            true,
            process is null
                ? new AppBinding.Manual()
                : new AppBinding.Processes([new ProcessName(process)]),
            InjectionMode.VirtualKey,
            [.. shortcuts],
            null
        );

    public static ShortcutLibrary BuildLibrary(
        ReadOnlySpan<Shortcut> always,
        ReadOnlySpan<Shortcut> general,
        params ReadOnlySpan<Profile> profiles
    ) =>
        ShortcutLibrary
            .CreateValidated([.. always], [DomainGen.General(general), .. profiles])
            .Value;

    /// <summary>Always visible: copy; General: undo; Word (winword.exe): bold, save; Chrome (chrome.exe): new tab.</summary>
    public static ShortcutLibrary Sample() =>
        BuildLibrary(
            [Tap("copy", "Copiar", KeyIds.Ctrl, KeyIds.C)],
            [Tap("undo", "Deshacer", KeyIds.Ctrl, KeyIds.Z)],
            Profile(
                "word",
                "Word",
                "WINWORD.EXE",
                Tap("bold", "Negrita", KeyIds.Ctrl, KeyIds.N),
                Tap("save", "Guardar", KeyIds.Ctrl, KeyIds.G)
            ),
            Profile(
                "chrome",
                "Navegador",
                "chrome.exe",
                Tap("newtab", "Pestaña nueva", KeyIds.Ctrl, KeyIds.T)
            )
        );
}
