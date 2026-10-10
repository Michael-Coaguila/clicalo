using Clicalo.Application.Store;
using Clicalo.Application.Tests.Store;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Tests.Generators;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.UseCases.Welcome;

/// <summary>
/// A small starter kit: «Basics» (two Always visible shortcuts and one of General) marked by default, Word
/// (winword.exe) and Navegador (chrome.exe and msedge.exe) unmarked.
/// </summary>
internal static class WelcomeTestData
{
    public static readonly StarterContent Content = new(
        new StarterKit(
            1,
            [
                new StarterOption("basics", StarterOptionKind.Basics, true),
                new StarterOption("word", StarterOptionKind.Template, false),
                new StarterOption("browser", StarterOptionKind.Template, false),
            ]
        ),
        new SeedContent(
            1,
            [Item("copy", KeyIds.Ctrl, KeyIds.C), Item("paste", KeyIds.Ctrl, KeyIds.V)],
            LocalizedText.Same("General", LangCode.Es, LangCode.En),
            new IconRef("apps"),
            [Item("undo", KeyIds.Ctrl, KeyIds.Z)]
        ),
        [
            Template("word", "description", ["winword.exe"], Item("bold", KeyIds.Ctrl, KeyIds.N)),
            Template(
                "browser",
                "public",
                ["chrome.exe", "msedge.exe"],
                Item("reload", KeyIds.Ctrl, KeyIds.R)
            ),
        ]
    );

    /// <summary>A first start: General and Always visible empty and the welcome not finished.</summary>
    public static UserDocument FirstStart() =>
        FirstDocument
            .Create(Content, StarterSelection.Empty, SettingsSchema.Defaults, new SequentialIds())
            .Value;

    /// <summary>A store with <paramref name="document"/>, its own ids and a fake clock.</summary>
    public static DocumentStore Store(UserDocument document) =>
        new(
            document,
            new SequentialIds(),
            new FakeBackupService(),
            new FakeTimeProvider(DomainGen.Now)
        );

    private static ProfileTemplate Template(
        string id,
        string icon,
        string[] processes,
        params TemplateShortcut[] shortcuts
    ) =>
        new(
            id,
            1,
            LocalizedText.Same(id, LangCode.Es, LangCode.En),
            new IconRef(icon),
            [.. processes.Select(p => new ProcessName(p))],
            [.. shortcuts]
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
}
