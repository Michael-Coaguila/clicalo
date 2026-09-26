using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Tests.Generators;

namespace Clicalo.Application.Tests.Store;

/// <summary>
/// A small document: Always visible «copy»; General «undo»; Word (winword.exe) «bold» and «save».
/// </summary>
internal static class StoreSamples
{
    public static readonly ShortcutId Copy = new("copy");
    public static readonly ShortcutId Bold = new("bold");
    public static readonly ShortcutId Save = new("save");
    public static readonly ProfileId Word = new("word");

    public static UserDocument Document() =>
        UserDocument.Create(
            ShortcutLibrary
                .CreateValidated(
                    [Tap("copy", "Copiar", KeyIds.Ctrl, KeyIds.C)],
                    [
                        DomainGen.General(Tap("undo", "Deshacer", KeyIds.Ctrl, KeyIds.Z)),
                        new Profile(
                            Word,
                            LocalizedText.Same("Word", LangCode.Es, LangCode.En),
                            new IconRef("description"),
                            true,
                            new AppBinding.Processes([new ProcessName("winword.exe")]),
                            InjectionMode.VirtualKey,
                            [
                                Tap("bold", "Negrita", KeyIds.Ctrl, KeyIds.N),
                                Tap("save", "Guardar", KeyIds.Ctrl, KeyIds.G),
                            ],
                            null
                        ),
                    ]
                )
                .Value,
            SettingsSchema.Defaults
        );

    public static Shortcut Tap(string id, string name, params ReadOnlySpan<KeyId> keys) =>
        new(
            new ShortcutId(id),
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            true,
            new CategoryId("edit"),
            new TapAction(KeyChord.FromKeys(keys), []),
            new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
            null,
            null
        );

    public static Shortcut Named(this UserDocument document, ShortcutId id, string name) =>
        document.Library.TryGetShortcut(id, out var shortcut)
            ? shortcut with
            {
                Name = LocalizedText.Same(name, LangCode.Es, LangCode.En),
            }
            : throw new InvalidOperationException(id.Value);
}
