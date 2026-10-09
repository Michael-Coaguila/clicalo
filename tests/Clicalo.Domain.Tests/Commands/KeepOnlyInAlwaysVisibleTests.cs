using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Tests.Generators;
using static Clicalo.Domain.Tests.Library.LibraryBuilder;

namespace Clicalo.Domain.Tests.Commands;

/// <summary>
/// «Dejar solo en «Siempre visible»» (REP-005): keeps the appearance in Always visible or moves the open one there,
/// and deletes from the profiles only the appearances with the same name in some language (decision DIS-49).
/// </summary>
[Trait("Req", "REP-005")]
public sealed class KeepOnlyInAlwaysVisibleTests
{
    private static readonly DomainContext Context = new(
        DomainGen.Now,
        new SequentialIds(),
        LangCode.Es,
        LangCode.Es
    );

    [Fact]
    public void The_appearance_in_Always_visible_stays_and_the_profiles_lose_theirs_with_the_same_name()
    {
        var document = Document(
            [Named("a", "Copiar", "Copy", KeyIds.Ctrl, KeyIds.C)],
            Profile(
                "word",
                "Word",
                "winword.exe",
                Tap("w1", "Copiar", KeyIds.Ctrl, KeyIds.C),
                Named("w2", "Otra", "Copy", KeyIds.Ctrl, KeyIds.C),
                Tap("w3", "Cuadro", KeyIds.Ctrl, KeyIds.C)
            )
        );

        KeepOnlyInAlwaysVisible
            .KeptIn(document.Library, new ShortcutId("w1"))
            .ShouldBe(new ShortcutId("a"));
        var change = new KeepOnlyInAlwaysVisible(new ShortcutId("w1"))
            .Apply(document, Context)
            .Value;

        Ids(change.Next.Library.AlwaysVisible).ShouldBe(["a"]);
        Ids(Word(change.Next).Shortcuts)
            .ShouldBe(["w3"], "«Copy» matches in English; «Cuadro» stays");
        change.Undo.ShouldBe(new UndoIntent.Record(L.Moved.Key, null));
    }

    [Fact]
    public void Without_one_in_Always_visible_the_open_appearance_moves_there_and_remembers_its_profile()
    {
        var document = Document(
            [],
            Profile("word", "Word", "winword.exe", Tap("w1", "Copiar", KeyIds.Ctrl, KeyIds.C)),
            Profile("chrome", "Chrome", "chrome.exe", Tap("c1", "Copiar", KeyIds.C, KeyIds.Ctrl))
        );

        var next = new KeepOnlyInAlwaysVisible(new ShortcutId("w1"))
            .Apply(document, Context)
            .Value.Next;

        Ids(next.Library.AlwaysVisible).ShouldBe(["w1"]);
        next.Library.AlwaysVisible[0].PinnedFrom.ShouldBe(new ProfileId("word"));
        next.Library.TryGetShortcut(new ShortcutId("c1"), out _).ShouldBeFalse();
    }

    [Fact]
    public void A_shortcut_without_a_combination_cannot_be_kept() =>
        new KeepOnlyInAlwaysVisible(new ShortcutId("gone"))
            .Apply(Document([]), Context)
            .IsFailure.ShouldBeTrue();

    private static UserDocument Document(
        ReadOnlySpan<Shortcut> always,
        params ReadOnlySpan<Profile> profiles
    ) => UserDocument.Create(BuildLibrary(always, [], profiles), SettingsSchema.Defaults);

    private static Profile Word(UserDocument document) =>
        document.Library.Profiles.Items.Single(p => p.Id == new ProfileId("word"));

    private static List<string> Ids(ValueList<Shortcut> shortcuts) =>
        [.. shortcuts.Select(s => s.Id.Value)];
}
