using Clicalo.TestKit.Windows.Input;
using Clicalo.Tools.SpikeLab.Input;

namespace Clicalo.Tools.SpikeLab.Tests.Input;

public sealed class LabChordTests
{
    [Fact]
    public void A_chord_is_one_balanced_batch_with_left_modifiers_released_last()
    {
        var batch = LabChord
            .Of(LabModifiers.Control | LabModifiers.Shift, VirtualKeyCode.B)
            .ToBatch();

        batch
            .Select(stroke => stroke.ToString())
            .ShouldBe([
                "VK LeftControl down",
                "VK LeftShift down",
                "VK B down",
                "VK B up",
                "VK LeftShift up",
                "VK LeftControl up",
            ]);
        Should.NotThrow(() => KeyStrokeBatch.Validate(batch));
        Should.NotThrow(() => LabKeyInjector.EnsureLeftHandOnly(batch));
    }

    [Fact]
    public void The_rights_chord_is_left_Ctrl_Alt_Shift_and_F24_and_never_AltGr()
    {
        var batch = LabInternalKeyEffects.RightsChord.ToBatch();

        batch
            .Select(stroke => stroke.VirtualKey)
            .Distinct()
            .ShouldBe([
                VirtualKeyCode.LeftControl,
                VirtualKeyCode.LeftMenu,
                VirtualKeyCode.LeftShift,
                LabChord.F24Key,
            ]);
        batch.ShouldNotContain(stroke => stroke.VirtualKey == VirtualKeyCode.RightMenu);
        batch.ShouldNotContain(stroke => stroke.VirtualKey == VirtualKeyCode.RightControl);
        LabInternalKeyEffects.RightsChord.Describe().ShouldBe("Ctrl+Alt+Mayús+F24");
    }

    [Fact]
    public void Win_H_uses_the_left_Windows_key() =>
        LabInternalKeyEffects
            .DictationChord.ToBatch()
            .Select(stroke => stroke.ToString())
            .ShouldBe(["VK LeftWindows down", "VK H down", "VK H up", "VK LeftWindows up"]);

    [Theory]
    [InlineData(VirtualKeyCode.RightMenu)]
    [InlineData(VirtualKeyCode.RightControl)]
    [InlineData(VirtualKeyCode.LeftControl)]
    [InlineData(VirtualKeyCode.Menu)]
    [InlineData(VirtualKeyCode.None)]
    public void A_modifier_key_is_never_the_key_of_a_chord(VirtualKeyCode key) =>
        Should.Throw<ArgumentException>(() => LabChord.Of(LabModifiers.None, key));

    [Fact]
    public void Latched_modifiers_are_added_to_the_chord() =>
        LabChord
            .Ctrl(VirtualKeyCode.S)
            .With(LabModifiers.Shift)
            .Describe()
            .ShouldBe("Ctrl+Mayús+S");

    [Fact]
    public void Navigation_keys_have_readable_names() =>
        LabChord.Ctrl(LabChord.HomeKey).Describe().ShouldBe("Ctrl+Inicio");
}
