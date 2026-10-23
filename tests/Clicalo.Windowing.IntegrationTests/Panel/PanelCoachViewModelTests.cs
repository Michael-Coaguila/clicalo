using Clicalo.Presentation.Panel;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The guide of the Full and Compact views (R-39), headless: its three steps from «Qué ver» to the header, the labels of
/// its buttons, its name and its intents, in both languages. No window.
/// </summary>
[Trait("Req", "PES-015")]
public sealed class PanelCoachViewModelTests
{
    private int _next;
    private int _skip;

    [Fact]
    public void The_guide_walks_from_what_to_show_to_the_tools_of_the_header()
    {
        var coach = Create("es");

        coach.Apply(0, shown: true);
        coach.IsShown.ShouldBeTrue();
        coach.Name.ShouldBe("Guía del panel");
        coach.StepLabel.ShouldBe("1 / 3");
        coach.Title.ShouldBe("Qué ver");
        coach.Text.ShouldStartWith("★ muestra tus Frecuentes");
        coach.NextLabel.ShouldBe("Siguiente");
        coach.SkipLabel.ShouldBe("No volver a mostrar");

        coach.Apply(1, shown: true);
        coach.StepLabel.ShouldBe("2 / 3");
        coach.Title.ShouldBe("Tus atajos");
        coach.Text.ShouldContain("flechas de abajo pasan de página");

        coach.Apply(2, shown: true);
        coach.StepLabel.ShouldBe("3 / 3");
        coach.Title.ShouldBe("Arriba: herramientas");
        coach.Text.ShouldStartWith("Buscar, editar los atajos, Ajustes rápidos y minimizar");
        coach.NextLabel.ShouldBe("Entendido");
        coach.Name.ShouldBe("Guía del panel");

        coach.Apply(2, shown: false);
        coach.IsShown.ShouldBeFalse();
    }

    [Fact]
    public void Every_step_is_offered_for_the_size_of_the_longest_in_its_language()
    {
        var coach = Create("en");

        coach
            .Steps.Select(static s => s.Title)
            .ShouldBe(["What to show", "Your shortcuts", "At the top: tools"]);
        coach.Steps.ShouldAllBe(static s => s.Text.Length > 0);
        coach.Name.ShouldBe("Panel guide");
        coach.SkipLabel.ShouldBe("Don’t show again");
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    public void The_texts_follow_the_language_of_the_interface()
    {
        var localization = PanelTestData.Localization("es");
        var coach = new PanelCoachViewModel(localization, () => _next++, () => _skip++);
        coach.Apply(1, shown: true);

        localization.TrySetLanguage("en").ShouldBeTrue();
        coach.Relocalize();

        coach.Title.ShouldBe("Your shortcuts");
        coach.NextLabel.ShouldBe("Next");
    }

    [Fact]
    public void A_step_out_of_range_shows_the_nearest_one()
    {
        var coach = Create("es");

        coach.Apply(7, shown: true);
        coach.StepLabel.ShouldBe("3 / 3");
        coach.Apply(-1, shown: true);
        coach.StepLabel.ShouldBe("1 / 3");
    }

    [Fact]
    public void Next_and_skip_go_to_the_composition()
    {
        var coach = Create("es");

        coach.Next();
        coach.Skip();
        coach.Skip();

        _next.ShouldBe(1);
        _skip.ShouldBe(2);
    }

    private PanelCoachViewModel Create(string language) =>
        new(PanelTestData.Localization(language), () => _next++, () => _skip++);
}
