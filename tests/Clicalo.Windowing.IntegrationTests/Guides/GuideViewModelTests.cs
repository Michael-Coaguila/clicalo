using Clicalo.Domain.Guides;
using Clicalo.Presentation.Guides;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;

namespace Clicalo.Windowing.IntegrationTests.Guides;

/// <summary>
/// The guide on show (R-39), headless: where each guide is, what it says, the step that waits for an action, the step
/// left out without its control, and what is remembered when a guide ends. No window.
/// </summary>
[Trait("Req", "PES-015")]
public sealed class GuideViewModelTests
{
    private readonly List<GuideId> _done = [];

    [Fact]
    public void The_guide_of_the_panel_starts_at_the_tiles_and_ends_at_minimize()
    {
        var guide = Create("es");

        guide.Show(GuideId.Panel);

        guide.IsShown.ShouldBeTrue();
        guide.Name.ShouldBe("Guía del panel");
        guide.StepLabel.ShouldBe("1 / 10");
        guide.Title.ShouldBe("Tus atajos");
        guide.Target.ShouldBe(GuideTarget.PanelGrid);
        guide.NextLabel.ShouldBe("Siguiente");
        guide.SkipLabel.ShouldBe("Omitir");
        guide.Steps.Count.ShouldBe(10);

        for (var i = 1; i < 10; i++)
        {
            guide.Next();
        }

        guide.Target.ShouldBe(GuideTarget.PanelMinimize);
        guide.Title.ShouldBe("Minimizar");
        guide.NextLabel.ShouldBe("Entendido");
        _done.ShouldBeEmpty();
        guide.Next();
        guide.IsShown.ShouldBeFalse();
        _done.ShouldBe([GuideId.Panel]);
    }

    [Fact]
    public void A_step_that_waits_goes_on_when_the_person_does_it_and_only_then()
    {
        var guide = Create("es");
        guide.Show(GuideId.Panel);
        guide.Next();
        guide.Target.ShouldBe(GuideTarget.PanelFrequents);

        // A tile is never waited for; the wrong action changes nothing.
        guide.Signal(GuideSignal.Minimized);
        guide.Target.ShouldBe(GuideTarget.PanelFrequents);

        guide.Signal(GuideSignal.FrequentsShown);
        guide.Target.ShouldBe(GuideTarget.PanelProfile);
        guide.Signal(GuideSignal.ProfileButtonUsed);
        guide.Target.ShouldBe(GuideTarget.PanelAutoFixed);
    }

    [Fact]
    public void Without_a_second_page_the_step_of_the_pager_is_left_out()
    {
        var guide = Create("es");

        guide.Show(GuideId.Panel, static target => target != GuideTarget.PanelPager);
        guide.StepLabel.ShouldBe("1 / 9");
        for (var i = 0; i < 4; i++)
        {
            guide.Next();
        }

        guide.Target.ShouldBe(GuideTarget.PanelSearch);
        guide.StepLabel.ShouldBe("5 / 9");
    }

    [Fact]
    public void A_hidden_guide_comes_back_where_it_was_and_skip_ends_it()
    {
        var guide = Create("es");
        guide.Show(GuideId.Panel);
        guide.Next();
        guide.Next();

        // Quick settings open where the guide goes: it steps aside and comes back on the same step.
        guide.Show(null);
        guide.IsShown.ShouldBeFalse();
        guide.Show(GuideId.Panel);
        guide.Target.ShouldBe(GuideTarget.PanelProfile);

        guide.Skip();
        guide.IsShown.ShouldBeFalse();
        _done.ShouldBe([GuideId.Panel]);

        // Shown again after General asks for it, it starts over.
        guide.Show(GuideId.Panel);
        guide.Target.ShouldBe(GuideTarget.PanelGrid);
    }

    [Fact]
    public void Minimize_ends_the_guide_of_the_panel_and_the_bubble_has_its_own()
    {
        var guide = Create("es");
        guide.Show(GuideId.Panel);
        for (var i = 1; i < 10; i++)
        {
            guide.Next();
        }

        guide.Signal(GuideSignal.Minimized);
        _done.ShouldBe([GuideId.Panel]);

        guide.Show(GuideId.Bubble);
        guide.Name.ShouldBe("Guía de la burbuja");
        guide.StepLabel.ShouldBe("1 / 1");
        guide.NextLabel.ShouldBe("Entendido");
        guide.Signal(GuideSignal.Restored);
        _done.ShouldBe([GuideId.Panel, GuideId.Bubble]);
    }

    [Theory]
    [InlineData(GuideId.Shortcuts, "Guía: Atajos", "Perfiles")]
    [InlineData(GuideId.Templates, "Guía: Plantillas", "Plantillas")]
    [InlineData(GuideId.General, "Guía: General y panel", "La vista")]
    [InlineData(GuideId.Touch, "Guía: Precisión táctil", "Cuánto perdona")]
    [InlineData(GuideId.System, "Guía: Sistema", "Tres pestañas")]
    [InlineData(GuideId.About, "Guía: Clícalo y contacto", "Opinión y contacto")]
    [InlineData(GuideId.Tab, "Guía de la pestaña", "Plegar")]
    public void Every_guide_is_named_as_its_screen(GuideId id, string name, string firstTitle)
    {
        var guide = Create("es");

        guide.Show(id);

        guide.Name.ShouldBe(name);
        guide.Title.ShouldBe(firstTitle);
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    public void The_texts_follow_the_language_of_the_interface()
    {
        var localization = PanelTestData.Localization("es");
        var guide = new GuideViewModel(localization, _done.Add);
        guide.Show(GuideId.Shortcuts);

        localization.TrySetLanguage("en").ShouldBeTrue();
        guide.Relocalize();

        guide.Name.ShouldBe("Guide: Shortcuts");
        guide.Title.ShouldBe("Profiles");
        guide.SkipLabel.ShouldBe("Skip");
    }

    private GuideViewModel Create(string language) =>
        new(PanelTestData.Localization(language), _done.Add);
}
