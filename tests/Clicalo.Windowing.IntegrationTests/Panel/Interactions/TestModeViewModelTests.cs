using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.TestMode;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>
/// Test mode headless (TAC-008, CUA-009, INV-7): 30 s with a countdown, ✓ or ⊘ for 700 ms with its notice, the engine
/// told to send nothing, and the two ways it ends.
/// </summary>
[Trait("Req", "TAC-008")]
public sealed class TestModeViewModelTests
{
    private readonly InteractionsWorld _world = new();
    private readonly List<TestMarkChangedEventArgs> _marks = [];

    public TestModeViewModelTests() => _world.TestMode.MarkChanged += (_, e) => _marks.Add(e);

    [Fact]
    public void Starting_tells_the_engine_shows_the_sticky_notice_and_counts_down()
    {
        var test = _world.TestMode;

        test.Start();

        test.IsOn.ShouldBeTrue();
        _world.Engine.Events.ShouldBe([new EngineEvent.SetTestMode(true)]);
        _world
            .Text(_world.Notices.Sticky!)
            .ShouldBe("Modo prueba: toca botones para ver si el toque cuenta. No se envía nada.");
        test.IndicatorText.ShouldBe("Modo prueba · 30 s");

        _world.Time.Advance(TimeSpan.FromSeconds(1));
        test.IndicatorText.ShouldBe("Modo prueba · 29 s");
        _world.Time.Advance(TimeSpan.FromSeconds(28));
        test.IndicatorText.ShouldBe("Modo prueba · 1 s");
    }

    [Fact]
    [Trait("Req", "CUA-009")]
    public void An_accepted_touch_is_marked_for_700_ms_and_says_it_was_not_sent()
    {
        var test = _world.TestMode;
        test.Start();

        test.OnCounted(InteractionsWorld.Bold).ShouldBeTrue();

        test.MarkOf(InteractionsWorld.Bold).ShouldBe(TestModeMark.Counted);
        TestModeViewModel.MarkIcon(TestModeMark.Counted).ShouldBe("check");
        test.MarkText(TestModeMark.Counted).ShouldBe("Toque registrado (no se envió)");
        _world.Text(_world.Notices.Timed.Single()).ShouldBe("Toque registrado (no se envió)");
        _marks.Single().Mark.ShouldBe(TestModeMark.Counted);

        _world.Time.Advance(Timings.TestMode.TestMarkDuration - TimeSpan.FromMilliseconds(1));
        test.MarkOf(InteractionsWorld.Bold).ShouldNotBeNull();
        _world.Time.Advance(TimeSpan.FromMilliseconds(1));
        test.MarkOf(InteractionsWorld.Bold).ShouldBeNull();
        _marks[^1].Mark.ShouldBeNull();
        _world.Engine.Events.Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "TAC-002")]
    public void A_short_or_double_touch_is_marked_ignored_with_its_reason()
    {
        var test = _world.TestMode;
        test.Start();

        test.OnIgnored(InteractionsWorld.Bold, IgnoreReason.TooShort);
        test.OnIgnored(InteractionsWorld.Dictation, IgnoreReason.Debounced);
        test.OnIgnored(InteractionsWorld.Dictate, IgnoreReason.Palm);

        TestModeViewModel.MarkIcon(test.MarkOf(InteractionsWorld.Bold)!.Value).ShouldBe("block");
        _world
            .Notices.Timed.Select(_world.Text)
            .ShouldBe([
                "Ignorado: contacto demasiado breve",
                "Ignorado: doble toque demasiado rápido",
            ]);
        _world.Notices.Timed.ShouldAllBe(static n => n.Tone == NoticeTone.Warning);
        test.MarkOf(InteractionsWorld.Dictate).ShouldBeNull();
    }

    [Fact]
    public void After_thirty_seconds_it_ends_by_itself_and_the_engine_sends_again()
    {
        var test = _world.TestMode;
        test.Start();
        test.OnCounted(InteractionsWorld.Bold);

        _world.Time.Advance(Timings.TestMode.TestModeDuration);

        test.IsOn.ShouldBeFalse();
        test.IndicatorText.ShouldBeEmpty();
        test.MarkOf(InteractionsWorld.Bold).ShouldBeNull();
        _world.Engine.Events[^1].ShouldBe(new EngineEvent.SetTestMode(false));
        _world.Text(_world.Notices.Timed[^1]).ShouldBe("Modo prueba terminado");
        test.OnCounted(InteractionsWorld.Bold).ShouldBeFalse();
    }

    [Fact]
    public void Switching_it_off_by_hand_removes_the_notice_and_ends_at_once()
    {
        var test = _world.TestMode;
        test.Start();

        test.Toggle();

        test.IsOn.ShouldBeFalse();
        _world.Notices.Sticky.ShouldBeNull();
        _world.Notices.Timed.ShouldBeEmpty();
        _world.Engine.Events.ShouldBe([
            new EngineEvent.SetTestMode(true),
            new EngineEvent.SetTestMode(false),
        ]);

        _world.Time.Advance(Timings.TestMode.TestModeDuration);
        _world.Engine.Events.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    public void The_countdown_changes_language_in_place()
    {
        var test = _world.TestMode;
        test.Start();

        _world.Localization.TrySetLanguage("en");
        test.Relocalize();

        test.IndicatorText.ShouldBe("Test mode · 30 s");
    }
}
