using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Sharing;
using Clicalo.TestKit.Time;

namespace Clicalo.Infrastructure.Tests.Sharing;

/// <summary>The adapter of the Control Center over the shared profile format (DAT-007, LOG-006, DAT-004).</summary>
[Trait("Req", "DAT-007")]
public sealed class ProfileSharingTests
{
    [Fact]
    [Trait("Req", "DAT-004")]
    public void A_shared_profile_comes_back_for_the_preview_with_new_ids()
    {
        var sharing = new ProfileSharing(TestTime.CreateProvider(), new Ids());
        var profile = new Profile(
            new ProfileId("notes"),
            LocalizedText.Same("Notas", LangCode.Es, LangCode.En),
            new IconRef("edit_note"),
            false,
            new AppBinding.Processes([new ProcessName("notepad.exe")]),
            InjectionMode.VirtualKey,
            [
                new Shortcut(
                    new ShortcutId("save"),
                    LocalizedText.Same("Guardar", LangCode.Es, LangCode.En),
                    new IconRef("save"),
                    false,
                    new CategoryId("file"),
                    new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.S), []),
                    new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
                    null,
                    null
                ),
            ],
            null
        );

        var file = sharing.Export(profile, includeTextsInClear: false);
        var shared = sharing.Import(file.Content).Value;

        file.FileName.ShouldBe("clicalo-perfil-notes.json");
        shared.UnavailableTexts.ShouldBe(0);
        shared.Profile.Id.Value.ShouldBe("p1");
        shared.Profile.Shortcuts.ShouldHaveSingleItem().Id.Value.ShouldBe("s1");
        shared.Profile.Name.ShouldBe(profile.Name);
    }

    [Fact]
    [Trait("Req", "LOG-006")]
    public void Anything_else_is_refused_with_its_message()
    {
        var sharing = new ProfileSharing(TestTime.CreateProvider(), new Ids());

        sharing.Import("{\"type\":\"backup\"}"u8.ToArray()).IsSuccess.ShouldBeFalse();
    }

    private sealed class Ids : IIdGenerator
    {
        private int _profiles;
        private int _shortcuts;

        public ProfileId NewProfileId() => new("p" + ++_profiles);

        public ShortcutId NewShortcutId() => new("s" + ++_shortcuts);
    }
}
