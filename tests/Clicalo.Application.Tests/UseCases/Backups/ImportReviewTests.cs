using Clicalo.Application.UseCases.Backups;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Application.Tests.UseCases.Backups;

/// <summary>
/// LOG-008: a backup that is imported or restored is imported content. Its Web, App and Macro shortcuts that the
/// document does not have yet are confirmed one by one; the unconfirmed ones are left out, and nothing else changes.
/// </summary>
[Trait("Req", "LOG-008")]
public sealed class ImportReviewTests
{
    private static readonly Shortcut Copy = Shortcut(
        "copy",
        new TapAction(KeyChord.FromKeys([KeyIds.Ctrl, KeyIds.C]), [])
    );

    private static readonly Shortcut Web = Shortcut(
        "web",
        new UrlAction(new UrlTarget.Valid(new Uri("https://clima.example/")))
    );

    private static readonly Shortcut App = Shortcut(
        "app",
        new AppAction(new AppTarget.Executable("notepad.exe", string.Empty))
    );

    private static readonly Shortcut Macro = Shortcut(
        "macro",
        new MacroAction([new WaitStep(TimeSpan.FromSeconds(1))])
    );

    [Fact]
    public void Web_app_and_macro_shortcuts_the_document_does_not_have_need_confirmation()
    {
        var pending = ImportReview.Pending(Document(Copy), Document(Copy, Web, App, Macro));

        pending.Select(s => s.Id.Value).ShouldBe(["web", "app", "macro"]);
        ImportReview.IsRisky(Copy).ShouldBeFalse();
    }

    [Fact]
    public void A_backup_of_ones_own_data_asks_nothing()
    {
        var current = Document(Copy, Web, App, Macro);
        var backup = Document(
            Copy,
            Web with
            {
                Id = new ShortcutId("web-older-id"),
            },
            App,
            Macro
        );

        ImportReview
            .Pending(current, backup)
            .ShouldBeEmpty("what it would install is already installed");
        ImportReview
            .Confirmed(current, backup, new HashSet<ShortcutId>())
            .Value.ShouldBeSameAs(backup);
    }

    [Fact]
    public void A_changed_address_is_not_the_shortcut_the_person_has()
    {
        var edited = Web with
        {
            Action = new UrlAction(new UrlTarget.Valid(new Uri("https://otra.example/"))),
        };

        ImportReview.Pending(Document(Copy, Web), Document(Copy, edited)).ShouldBe([edited]);
    }

    [Fact]
    public void Only_what_was_confirmed_is_installed()
    {
        var current = Document(Copy);
        var incoming = Document(Copy, Web, App, Macro);

        var reviewed = ImportReview
            .Confirmed(current, incoming, new HashSet<ShortcutId> { Web.Id })
            .Value;

        reviewed
            .Library.EnumerateShortcuts()
            .Select(located => located.Shortcut.Id.Value)
            .ShouldBe(["copy", "web"]);
        reviewed.Validate().ShouldBeEmpty();
        reviewed.Settings.ShouldBe(incoming.Settings);
        ImportReview
            .Confirmed(current, incoming, new HashSet<ShortcutId>())
            .Value.Library.EnumerateShortcuts()
            .ShouldHaveSingleItem();
    }

    private static UserDocument Document(params Shortcut[] shortcuts) =>
        UserDocument.Create(
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
                            [.. shortcuts],
                            null
                        ),
                    ]
                )
                .Value,
            SettingsSchema.Defaults
        );

    private static Shortcut Shortcut(string id, ShortcutAction action) =>
        new(
            new ShortcutId(id),
            LocalizedText.Same(id, LangCode.Es, LangCode.En),
            new IconRef("apps"),
            AutoIcon: false,
            new CategoryId("edit"),
            action,
            new ShortcutOptions(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false),
            Origin: null,
            PinnedFrom: null
        );
}
