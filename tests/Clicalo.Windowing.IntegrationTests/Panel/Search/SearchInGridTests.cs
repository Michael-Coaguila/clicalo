using Clicalo.Application.Ports;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Messages;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Search;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;

namespace Clicalo.Windowing.IntegrationTests.SearchPanel;

/// <summary>
/// The results of the search in the grid of the composed panel, headless (BUS-005, PAN-008): while the search has
/// text they replace the list in view and name their origin under the name; without text the profile comes back.
/// </summary>
[Trait("Req", "BUS-005")]
public sealed class SearchInGridTests
{
    private readonly SearchViewModel _search;
    private readonly PanelViewModel _panel;

    public SearchInGridTests()
    {
        _search = new SearchViewModel(
            new PanelSearch(
                new SearchTestWorld.RefusingForeground(),
                new PanelEngineInbox(),
                () => 3,
                TimeProvider.System,
                keyboard: null
            ),
            PanelTestData.Localization("es"),
            () => new WindowToken(0x10),
            static _ => null,
            static (Message _) => { }
        );
        var library = SearchTestWorld.Library();
        _search.ApplyLibrary(library);
        _panel = PanelBodyTestData.Panel(library, SearchTestWorld.Word, new RecordingBodyIntents());
    }

    [Fact]
    [Trait("Req", "PAN-008")]
    public async Task Results_replace_the_grid_and_show_their_origin_while_the_search_has_text()
    {
        await _search.OpenAsync(SearchTrigger.Touch);
        _search.Query = "dicta";

        _panel.ApplySearch([.. _search.Results], _search.NoResultsText);
        _panel.ApplyContext(PanelBodyContext.Idle with { SearchingWithText = true });

        _panel
            .Tiles.Select(static t => (t.AccessibleName, t.Keys))
            .ShouldBe([("Dictar", "Siempre visible"), ("Dictado", "Word")]);
        _panel.Layers.AlwaysVisibleRow.ShouldBeFalse();
        _panel.ShowsNoResults.ShouldBeFalse();

        _panel.ApplyContext(PanelBodyContext.Idle);

        _panel.Tiles.Select(static t => t.AccessibleName).ShouldBe(["Negrita", "Dictado"]);
    }

    [Fact]
    public async Task A_search_that_finds_nothing_says_so_in_place_of_the_grid()
    {
        await _search.OpenAsync(SearchTrigger.Touch);
        _search.Query = "zzz";

        _panel.ApplySearch([.. _search.Results], _search.NoResultsText);
        _panel.ApplyContext(PanelBodyContext.Idle with { SearchingWithText = true });

        _panel.Tiles.ShouldBeEmpty();
        _panel.ShowsNoResults.ShouldBeTrue();
        _panel.NoResultsText.ShouldBe("Nada coincide. Prueba con otra palabra.");
        _panel.Layers.EmptyProfile.ShouldBeFalse();
    }
}
