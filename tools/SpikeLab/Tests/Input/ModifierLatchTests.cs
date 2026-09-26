using Clicalo.Tools.SpikeLab.Input;

namespace Clicalo.Tools.SpikeLab.Tests.Input;

public sealed class ModifierLatchTests
{
    [Fact]
    public void Shift_cycles_through_the_three_states_of_a_sticky_key()
    {
        var latch = new ModifierLatch();

        latch.CycleShift().ShouldBe(LatchState.Once);
        latch.CycleShift().ShouldBe(LatchState.Locked);
        latch.CycleShift().ShouldBe(LatchState.Off);
    }

    [Fact]
    public void A_once_Shift_applies_to_the_next_chord_only()
    {
        var latch = new ModifierLatch();
        latch.CycleShift();

        latch.Consume().ShouldBe(LabModifiers.Shift);
        latch.Consume().ShouldBe(LabModifiers.None);
        latch.Shift.ShouldBe(LatchState.Off);
    }

    [Fact]
    public void A_locked_Shift_and_a_held_Ctrl_stay_until_released()
    {
        var latch = new ModifierLatch();
        latch.CycleShift();
        latch.CycleShift();
        latch.ToggleControl().ShouldBeTrue();

        latch.Consume().ShouldBe(LabModifiers.Shift | LabModifiers.Control);
        latch.Consume().ShouldBe(LabModifiers.Shift | LabModifiers.Control);

        latch.Clear();
        latch.IsAnyLatched.ShouldBeFalse();
        latch.Consume().ShouldBe(LabModifiers.None);
    }

    [Fact]
    public void Changed_fires_on_every_change_and_not_on_an_idle_clear()
    {
        var latch = new ModifierLatch();
        var changes = 0;
        latch.Changed += (_, _) => changes++;

        latch.Clear();
        latch.ToggleControl();
        latch.CycleShift();
        latch.Consume();
        latch.Clear();

        changes.ShouldBe(4);
    }
}
