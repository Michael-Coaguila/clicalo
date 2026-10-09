using Clicalo.Application.UseCases.Welcome;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Settings;
using PanelSize = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Application.Tests.UseCases.Welcome;

/// <summary>The effects of «¿Cómo usas tu equipo?» (BIE-005, docs/06).</summary>
[Trait("Req", "BIE-005")]
public sealed class WelcomeEffectsTests
{
    [Theory]
    [InlineData(new WelcomeUse[0], "standard")]
    [InlineData(new[] { WelcomeUse.Mouse }, "standard")]
    [InlineData(new[] { WelcomeUse.Voice }, "standard")]
    [InlineData(new[] { WelcomeUse.Touch }, "mild-tremor")]
    [InlineData(new[] { WelcomeUse.NoKeyboard }, "mild-tremor")]
    [InlineData(new[] { WelcomeUse.Touch, WelcomeUse.Tremor }, "strong-tremor")]
    [InlineData(new[] { WelcomeUse.Tremor }, "strong-tremor")]
    public void The_touch_preset_follows_tremor_then_touch_or_no_keyboard(
        WelcomeUse[] uses,
        string preset
    )
    {
        WelcomeEffects.PresetFor(uses.ToHashSet()).Id.ShouldBe(preset);
    }

    [Fact]
    public void Tremor_sets_the_strong_preset_values_and_size_l()
    {
        var before = SettingsSchema.Defaults with { Size = PanelSize.Small };

        var after = WelcomeEffects.Apply(before, new HashSet<WelcomeUse> { WelcomeUse.Tremor });

        after.Touch.ShouldBe(
            new TouchFilterSettings(
                TouchPresets.StrongTremor.Id,
                TouchPresets.StrongTremor.Debounce,
                TouchPresets.StrongTremor.HitSlopPx,
                TouchPresets.StrongTremor.CancelMovePx,
                TouchPresets.StrongTremor.MinContact
            )
        );
        after.Size.ShouldBe(PanelSize.Large);
        after.VoiceNumbers.ShouldBeFalse();
        after.NoKeyboardUser.ShouldBeFalse();
    }

    [Fact]
    public void Voice_and_no_keyboard_set_their_switches_and_keep_the_size()
    {
        var before = SettingsSchema.Defaults with { Size = PanelSize.Small };

        var after = WelcomeEffects.Apply(
            before,
            new HashSet<WelcomeUse> { WelcomeUse.Voice, WelcomeUse.NoKeyboard }
        );

        after.VoiceNumbers.ShouldBeTrue();
        after.NoKeyboardUser.ShouldBeTrue();
        after.Touch.Preset.ShouldBe(TouchPresets.MildTremor.Id);
        after.Size.ShouldBe(PanelSize.Small);
    }

    [Fact]
    [Trait("Req", "BIE-010")]
    public void A_repeated_welcome_reads_the_answers_the_settings_keep()
    {
        var settings = WelcomeEffects.Apply(
            SettingsSchema.Defaults,
            new HashSet<WelcomeUse> { WelcomeUse.Voice, WelcomeUse.NoKeyboard, WelcomeUse.Tremor }
        );

        WelcomeEffects
            .Read(settings)
            .ShouldBe(
                [WelcomeUse.Voice, WelcomeUse.NoKeyboard, WelcomeUse.Tremor],
                ignoreOrder: true
            );
        WelcomeEffects.Read(SettingsSchema.Defaults).ShouldBeEmpty();
    }
}
