using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Presentation.Panel.Search;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel.Search;
using Clicalo.Windowing.IntegrationTests.Controls;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;

namespace Clicalo.Windowing.IntegrationTests.SearchPanel;

/// <summary>
/// The search row and the suggestion card of the panel, laid out without a window (nothing here needs a desktop): their
/// sizes (BUS-001, REG-02), their UI Automation names (REG-06) and how they follow their view models.
/// </summary>
public sealed class SearchSurfaceTests
{
    [Fact]
    [Trait("Req", "BUS-001")]
    [Trait("Req", "BUS-003")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    public void The_search_row_shows_while_open_with_a_44_px_field_and_the_dictate_button() =>
        WpfThread.Invoke(() =>
        {
            var search = Search();
            var bar = new SearchBar(search);
            using var service = BaseControlTests.Host(bar);
            bar.Visibility.ShouldBe(Visibility.Collapsed);

            search.OpenAsync(SearchTrigger.Touch).IsCompleted.ShouldBeTrue();
            bar.UpdateLayout();

            bar.Visibility.ShouldBe(Visibility.Visible);
            bar.Field.ActualHeight.ShouldBe(SearchBar.FieldHeight);
            TouchTarget
                .IsLargeEnough(
                    new Size(bar.DictateButton.ActualWidth, bar.DictateButton.ActualHeight)
                )
                .ShouldBeTrue();
            AutomationProperties.GetName(bar.Field).ShouldBe(search.Placeholder);
            UIElementAutomationPeer
                .CreatePeerForElement(bar.DictateButton)
                .GetName()
                .ShouldBe("Dictar");
        });

    [Fact]
    [Trait("Req", "BUS-005")]
    public void Typing_searches_and_closing_empties_the_field() =>
        WpfThread.Invoke(() =>
        {
            var search = Search();
            var bar = new SearchBar(search);
            using var service = BaseControlTests.Host(bar);
            _ = search.OpenAsync(SearchTrigger.Touch);

            bar.Field.Text = "negr";
            search.Query.ShouldBe("negr");
            search.Results.ShouldHaveSingleItem().AccessibleName.ShouldBe("Negrita");

            search.CloseAsync().IsCompleted.ShouldBeTrue();
            bar.Field.Text.ShouldBeEmpty();
            bar.Visibility.ShouldBe(Visibility.Collapsed);
        });

    [Fact]
    [Trait("Req", "PER-009")]
    [Trait("Req", "REG-02")]
    public void The_suggestion_card_names_the_app_and_its_buttons_answer_on_44_px() =>
        WpfThread.Invoke(() =>
        {
            var suggestion = Suggestion();
            var card = new SuggestionCard(suggestion);
            using var service = BaseControlTests.Host(card);
            card.Visibility.ShouldBe(Visibility.Collapsed);

            suggestion.Apply(new ProcessName("excel.exe"), searching: false);
            card.UpdateLayout();

            card.Visibility.ShouldBe(Visibility.Visible);
            card.CreateButton.Content.ShouldBe("Crear perfil");
            card.NotNowButton.Content.ShouldBe("Ahora no");
            foreach (var button in new[] { card.CreateButton, card.NotNowButton })
            {
                TouchTarget
                    .IsLargeEnough(new Size(button.ActualWidth, button.ActualHeight))
                    .ShouldBeTrue();
            }

            card.NotNowButton.RaiseEvent(
                new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)
            );
            card.Visibility.ShouldBe(Visibility.Collapsed);
        });

    internal static SearchViewModel Search()
    {
        var search = new SearchViewModel(
            new PanelSearch(
                new SearchTestWorld.RefusingForeground(),
                new PanelEngineInbox(),
                () => 1,
                TimeProvider.System,
                keyboard: null
            ),
            PanelTestData.Localization("es"),
            () => WindowToken.None,
            static _ => null,
            static _ => { }
        );
        search.ApplyLibrary(SearchTestWorld.Library());
        return search;
    }

    internal static SuggestionViewModel Suggestion() =>
        new(
            new DocumentStore(
                SearchTestWorld.Document(),
                new SequentialTestIds(),
                new SearchTestWorld.NoBackups(),
                TimeProvider.System
            ),
            SuggestionViewModelTests.Content,
            PanelTestData.Localization("es"),
            new SequentialTestIds(),
            static () => new ProfileState(new ViewTarget.Profile(ProfileId.General), false, null),
            static (SuggestionAccepted _) => { },
            static _ => { }
        );
}
