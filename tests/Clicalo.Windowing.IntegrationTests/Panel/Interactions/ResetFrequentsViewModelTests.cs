using Clicalo.Domain.Commands;
using Clicalo.Domain.Timing;
using Clicalo.Presentation.Panel.ContextMenu;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>«Reiniciar Frecuentes» headless (FRE-004, REG-04): two taps, a 3.5 s window and undo that restores the three.</summary>
[Trait("Req", "FRE-004")]
public sealed class ResetFrequentsViewModelTests
{
    private readonly InteractionsWorld _world = new();
    private readonly ResetFrequentsViewModel _reset;

    public ResetFrequentsViewModelTests()
    {
        _world.Store.Dispatch(new PinToFrequents(InteractionsWorld.Bold));
        _world.Store.Dispatch(new HideFromFrequents(InteractionsWorld.Dictation));
        _world.Store.Dispatch(new RecordUsage(InteractionsWorld.Dictate));
        _reset = new ResetFrequentsViewModel(
            _world.Store,
            _world.Confirm,
            _world.Localization,
            _world.Notices,
            _world.Time,
            InteractionsWorld.Post
        );
    }

    [Fact]
    [Trait("Req", "REG-04")]
    public void The_first_tap_arms_for_three_and_a_half_seconds_and_resets_nothing()
    {
        _reset.Label.ShouldBe("Reiniciar Frecuentes");
        _reset.ButtonText.ShouldBe("Reiniciar Frecuentes");

        _reset.Tap();

        _reset.IsArmed.ShouldBeTrue();
        _reset.ButtonText.ShouldBe("Confirmar");
        _world.Store.Current.Frequents.Pins.ShouldBe([InteractionsWorld.Bold]);

        _world.Time.Advance(Timings.Confirmation.DestructiveConfirmWindow);
        _reset.IsArmed.ShouldBeFalse();
        _reset.ButtonText.ShouldBe("Reiniciar Frecuentes");
    }

    [Fact]
    [Trait("Req", "REG-07")]
    public void The_second_tap_empties_usage_pins_and_hidden_and_undo_restores_the_three()
    {
        var before = _world.Store.Current.Frequents;

        _reset.Tap();
        _reset.Tap();

        var after = _world.Store.Current.Frequents;
        after.Pins.ShouldBeEmpty();
        after.Hidden.ShouldBeEmpty();
        after.Usage.Entries.ShouldBeEmpty();
        _world.Text(_world.Notices.Timed.Single()).ShouldBe("Frecuentes reiniciado");
        _world.Notices.Timed.Single().CanUndo.ShouldBeTrue();

        _world.Store.Undo().IsSuccess.ShouldBeTrue();
        _world.Store.Current.Frequents.Pins.ShouldBe(before.Pins);
        _world.Store.Current.Frequents.Hidden.ShouldBe(before.Hidden);
        _world.Store.Current.Frequents.Usage.ShouldBe(before.Usage);
    }
}
