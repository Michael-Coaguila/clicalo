using Clicalo.Application.Store;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Templates;
using Clicalo.Presentation.Panel.Search;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;

namespace Clicalo.Windowing.IntegrationTests.SearchPanel;

/// <summary>The suggestion card's view model, headless: its texts, «Ahora no» for the session and «Crear perfil».</summary>
[Trait("Req", "PER-009")]
public sealed class SuggestionViewModelTests
{
    private static readonly ProcessName Excel = new("excel.exe");

    internal static readonly StarterContent Content = new(
        new StarterKit(1, [new StarterOption("excel", StarterOptionKind.Template, false)]),
        new SeedContent(
            1,
            [],
            LocalizedText.Same("General", LangCode.Es, LangCode.En),
            new IconRef("apps"),
            []
        ),
        [
            new ProfileTemplate(
                "excel",
                1,
                LocalizedText.Same("Excel", LangCode.Es, LangCode.En),
                new IconRef("table"),
                [Excel],
                [
                    new TemplateShortcut(
                        "sum",
                        LocalizedText.Same("Autosuma", LangCode.Es, LangCode.En),
                        new IconRef("functions"),
                        new CategoryId("edit"),
                        new TapAction(KeyChord.FromKeys(KeyIds.Alt, KeyIds.F2), []),
                        false
                    ),
                ]
            ),
        ]
    );

    private readonly DocumentStore _store = new(
        SearchTestWorld.Document(),
        new SequentialTestIds(),
        new SearchTestWorld.NoBackups(),
        TimeProvider.System
    );

    private readonly List<SuggestionAccepted> _accepted = [];
    private readonly SuggestionViewModel _card;

    public SuggestionViewModelTests() =>
        _card = new SuggestionViewModel(
            _store,
            Content,
            PanelTestData.Localization("es"),
            new SequentialTestIds(),
            static () => new ProfileState(new ViewTarget.Profile(ProfileId.General), false, null),
            _accepted.Add,
            static _ => throw new InvalidOperationException("no failure expected")
        );

    [Fact]
    public void An_app_with_a_template_and_no_profile_shows_the_card_with_its_texts()
    {
        _card.Apply(Excel, searching: false);

        _card.IsVisible.ShouldBeTrue();
        _card.AppName.ShouldBe("Excel");
        _card.Message.ShouldBe("no tiene perfil. ¿Creo uno con atajos listos?");
        _card.CreateText.ShouldBe("Crear perfil");
        _card.NotNowText.ShouldBe("Ahora no");

        _card.Apply(Excel, searching: true);
        _card.IsVisible.ShouldBeFalse("a search with text hides it (PAN-008)");
    }

    [Fact]
    public void Not_now_dismisses_the_app_until_Clicalo_closes()
    {
        _card.Apply(Excel, searching: false);

        _card.NotNow();
        _card.Apply(new ProcessName("winword.exe"), searching: false);
        _card.Apply(Excel, searching: false);

        _card.IsVisible.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PER-007")]
    public void Create_installs_the_profile_shows_it_in_auto_and_hides_the_card()
    {
        _card.Apply(Excel, searching: false);

        _card.Create();

        var accepted = _accepted.ShouldHaveSingleItem();
        _store.Current.Library.ProfileFor(Excel).ShouldNotBeNull().Id.ShouldBe(accepted.Profile);
        accepted.Transition.State.View.ShouldBe(new ViewTarget.Profile(accepted.Profile));
        accepted.Notice.ShouldBe(L.InstalledApp(app: "Excel"));
        _card.IsVisible.ShouldBeFalse();
    }
}
