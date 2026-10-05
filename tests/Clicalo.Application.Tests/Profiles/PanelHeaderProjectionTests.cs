using Clicalo.Application.Profiles;
using Clicalo.Application.Tests.Localization;
using Clicalo.Application.Tests.Store;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Tests.Generators;

namespace Clicalo.Application.Tests.Profiles;

/// <summary>
/// The header (CAB-001 to CAB-003): the dynamic title, the «auto» dot, Auto/Fixed in words as well as color, and the
/// Auto/Fixed notices of PER-006 with their real texts.
/// </summary>
public sealed class PanelHeaderProjectionTests
{
    private static readonly ProfileId Word = new("word");

    private static readonly ShortcutLibrary Library = ShortcutLibrary
        .CreateValidated(
            [],
            [
                DomainGen.General(StoreSamples.Tap("undo", "Deshacer", KeyIds.Ctrl, KeyIds.Z)),
                new Profile(
                    Word,
                    LocalizedText.Same("Word", LangCode.Es, LangCode.En),
                    new IconRef("description"),
                    true,
                    new AppBinding.Processes([new ProcessName("winword.exe")]),
                    InjectionMode.VirtualKey,
                    [StoreSamples.Tap("bold", "Negrita", KeyIds.Ctrl, KeyIds.N)],
                    null
                ),
            ]
        )
        .Value;

    [Fact]
    [Trait("Req", "CAB-002")]
    public void A_profile_shows_its_icon_and_name_and_the_dot_when_it_is_the_active_app()
    {
        var header = Project(new ViewTarget.Profile(Word), locked: false, activeApp: Word);

        header.Kind.ShouldBe(HeaderTitleKind.Profile);
        header.Title.ShouldBeNull();
        header.ProfileName.ShouldBe("Word");
        header.Icon.ShouldBe(new IconRef("description"));
        header.ShowsActiveAppDot.ShouldBeTrue();
        Project(new ViewTarget.Profile(Word), false, activeApp: null)
            .ShowsActiveAppDot.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "CAB-002")]
    public void Frequents_and_a_search_with_text_change_the_title()
    {
        var frequents = Project(new ViewTarget.Frequents(), false, activeApp: Word);
        frequents.Kind.ShouldBe(HeaderTitleKind.Frequents);
        frequents.Title.ShouldBe(L.Freq);
        frequents.Icon.ShouldBe(PanelHeaderProjection.FrequentsIcon);
        frequents.ShowsActiveAppDot.ShouldBeFalse();

        var search = Project(new ViewTarget.Profile(Word), false, activeApp: Word, searching: true);
        search.Kind.ShouldBe(HeaderTitleKind.Search);
        search.Title.ShouldBe(L.SearchA);
        search.ShowsActiveAppDot.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "CAB-001")]
    public void Auto_Fixed_hides_only_while_the_search_has_text()
    {
        Project(new ViewTarget.Profile(Word), false, null).ShowsAutoFixed.ShouldBeTrue();
        Project(new ViewTarget.Frequents(), false, null).ShowsAutoFixed.ShouldBeTrue();
        Project(new ViewTarget.Profile(Word), false, null, searching: true)
            .ShowsAutoFixed.ShouldBeFalse();
    }

    [Theory]
    [Trait("Req", "CAB-003")]
    [Trait("Req", "ACC-003")]
    [InlineData(
        false,
        "autorenew",
        "Auto (azul): cambia con la app activa. Toca para fijar el perfil actual."
    )]
    [InlineData(
        true,
        "lock",
        "Fijo (rojo): el perfil no cambia al cambiar de app. Toca para volver a Auto."
    )]
    public void Auto_Fixed_is_said_by_its_icon_and_its_name_not_only_its_color(
        bool locked,
        string icon,
        string name
    )
    {
        var header = Project(new ViewTarget.Profile(Word), locked, null);

        header.IsFixed.ShouldBe(locked);
        header.AutoFixedIcon.ShouldBe(new IconRef(icon));
        Spanish(header.AutoFixedName).ShouldBe(name);
    }

    [Fact]
    [Trait("Req", "PER-006")]
    public void The_notices_name_the_profile_as_the_prototype_does()
    {
        Spanish(Notice(new ProfileNotice.Locked(Word))).ShouldBe("Fijo en: Word");
        Spanish(Notice(new ProfileNotice.FollowingApp(Word)))
            .ShouldBe("Auto: sigue la app activa: Word");
        Spanish(Notice(new ProfileNotice.FollowingApp(ProfileId.General)))
            .ShouldBe("Auto: sigue la app activa");
    }

    private static PanelHeaderModel Project(
        ViewTarget view,
        bool locked,
        ProfileId? activeApp,
        bool searching = false
    ) =>
        PanelHeaderProjection.Project(
            new ProfileState(view, locked, LastProfile: null),
            Library,
            activeApp,
            searching,
            LangCode.Es,
            LangCode.Es
        );

    private static Message Notice(ProfileNotice notice) =>
        PanelHeaderProjection.NoticeMessage(notice, Library, LangCode.Es, LangCode.Es);

    private static string Spanish(Message message) =>
        I18nRepository.Context("es").Current.Format(message);
}
