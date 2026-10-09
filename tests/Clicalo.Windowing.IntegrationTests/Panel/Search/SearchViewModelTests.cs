using Clicalo.Application.Ports;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Search;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;

namespace Clicalo.Windowing.IntegrationTests.SearchPanel;

/// <summary>
/// The search view model, headless (no window, no desktop): what the field shows, the results with their profile of
/// origin and the notices. The foreground owner refuses every lease here; the lease cycle itself is tested in
/// Application (<c>PanelSearchTests</c>).
/// </summary>
[Trait("Req", "BUS-005")]
public sealed class SearchViewModelTests
{
    private readonly SearchTestWorld.RefusingForeground _foreground = new();
    private readonly PanelEngineInbox _engine = new();
    private readonly List<Message> _notices = [];
    private readonly SearchViewModel _search;

    public SearchViewModelTests()
    {
        _search = new SearchViewModel(
            new PanelSearch(_foreground, _engine, () => 3, TimeProvider.System, keyboard: null),
            PanelTestData.Localization("es"),
            () => new WindowToken(0x10),
            static _ => null,
            _notices.Add
        );
        _search.ApplyLibrary(SearchTestWorld.Library());
    }

    [Fact]
    [Trait("Req", "BUS-001")]
    public async Task Opening_shows_an_empty_field_and_the_panel_as_without_searching()
    {
        await _search.OpenAsync(SearchTrigger.Touch);

        _search.IsOpen.ShouldBeTrue();
        _search.Query.ShouldBeEmpty();
        _search.IsSearching.ShouldBeFalse();
        _search.Results.ShouldBeEmpty();
        _search.ShowNoResults.ShouldBeFalse();
        _search.Placeholder.ShouldBe("Buscar acción en todos los perfiles…");
        _search.DictateName.ShouldBe("Dictar");
        _search.SearchName.ShouldBe("Buscar");
    }

    [Fact]
    [Trait("Req", "BUS-002")]
    public async Task When_Windows_refuses_the_keyboard_the_panel_says_so()
    {
        var focused = 0;
        _search.FocusFieldRequested += (_, _) => focused++;

        await _search.OpenAsync(SearchTrigger.Touch);

        _foreground.Requests.ShouldBe(1);
        focused.ShouldBe(0);
        _notices.ShouldBe([L.SearchDenied]);
    }

    [Fact]
    [Trait("Req", "BUS-004")]
    public async Task Results_name_their_profile_of_origin_instead_of_the_keys()
    {
        await _search.OpenAsync(SearchTrigger.Touch);

        _search.Query = "dicta";

        _search.IsSearching.ShouldBeTrue();
        _search
            .Results.Select(static r => (r.AccessibleName, r.Origin))
            .ShouldBe([("Dictar", "Siempre visible"), ("Dictado", "Word")]);
        _search.Results[0].Binding.OriginProfile.ShouldBeNull();
        _search.Results[1].Binding.OriginProfile.ShouldBe(SearchTestWorld.Word);
        _search.Results.ShouldAllBe(static r => r.Behavior == TileBehavior.Tap);
    }

    [Fact]
    public async Task Nothing_matching_shows_no_results()
    {
        await _search.OpenAsync(SearchTrigger.Touch);

        _search.Query = "zzz";

        _search.Results.ShouldBeEmpty();
        _search.ShowNoResults.ShouldBeTrue();
        _search.NoResultsText.ShouldBe("Nada coincide. Prueba con otra palabra.");
    }

    [Fact]
    [Trait("Req", "BUS-001")]
    public async Task Closing_or_switching_apps_empties_the_search()
    {
        await _search.OpenAsync(SearchTrigger.Touch);
        _search.Query = "negr";

        _search.OnAppChanged();

        _search.IsOpen.ShouldBeFalse();
        _search.Query.ShouldBeEmpty();
        _search.Results.ShouldBeEmpty();
        _search.ShowNoResults.ShouldBeFalse();
    }

    [Fact]
    public async Task A_result_without_the_keyboard_runs_with_its_profile_of_origin()
    {
        await _search.OpenAsync(SearchTrigger.Touch);
        _search.Query = "negr";

        _search.Results.ShouldHaveSingleItem().Invoke();

        var activation = _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<EngineEvent.Activation>();
        activation.Shortcut.Id.Value.ShouldBe("bold");
        activation.OriginProfile.ShouldBe(SearchTestWorld.Word);
        activation.Request.Phase.ShouldBe(ActivationPhase.Invoke);
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    public void Texts_follow_the_interface_language()
    {
        var localization = PanelTestData.Localization("es");
        var search = new SearchViewModel(
            new PanelSearch(_foreground, _engine, () => 3, TimeProvider.System, keyboard: null),
            localization,
            () => WindowToken.None,
            static _ => null,
            _notices.Add
        );

        localization.TrySetLanguage("en").ShouldBeTrue();
        search.Relocalize();

        search.Placeholder.ShouldBe("Search actions in all profiles…");
        search.DictateName.ShouldBe("Dictate");
    }
}
