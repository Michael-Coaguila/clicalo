using Clicalo.Application.UseCases.Welcome;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
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

    [Fact]
    [Trait("Req", "BIE-010")]
    public void An_effect_only_reaches_a_setting_that_still_has_what_the_welcome_left()
    {
        // The welcome left: strong preset, L, voice off. Then the person turned voice on and chose S by hand.
        var left = WelcomeEffects.Plan(
            SettingsSchema.Defaults,
            null,
            new HashSet<WelcomeUse> { WelcomeUse.Tremor },
            hadTremor: false
        );
        left.Changes.ShouldBe([WelcomeSetting.TouchPreset, WelcomeSetting.Size]);
        left.Kept.ShouldBeEmpty();
        var byHand = left.Settings with { Size = PanelSize.Small, VoiceNumbers = true };

        var plan = WelcomeEffects.Plan(
            byHand,
            left.Baseline,
            new HashSet<WelcomeUse> { WelcomeUse.NoKeyboard },
            hadTremor: true
        );

        plan.Changes.ShouldBe([WelcomeSetting.TouchPreset, WelcomeSetting.NoKeyboard]);
        plan.Kept.ShouldBe([WelcomeSetting.Size, WelcomeSetting.VoiceNumbers]);
        plan.Settings.Touch.Preset.ShouldBe(TouchPresets.MildTremor.Id);
        plan.Settings.NoKeyboardUser.ShouldBeTrue();
        plan.Settings.Size.ShouldBe(PanelSize.Small, "changed by hand");
        plan.Settings.VoiceNumbers.ShouldBeTrue("changed by hand");
        plan.Baseline.ShouldBe(
            new WelcomeBaseline(TouchPresets.MildTremor.Id, PanelSize.Large, false, true),
            "what was changed by hand keeps its old baseline, so it is still recognized next time"
        );
    }

    [Fact]
    [Trait("Req", "BIE-005")]
    [Trait("Req", "EC-BIE-01")]
    public void Unmarking_tremor_takes_back_the_size_the_welcome_had_set()
    {
        var withTremor = WelcomeEffects.Plan(
            SettingsSchema.Defaults,
            null,
            new HashSet<WelcomeUse> { WelcomeUse.Tremor },
            hadTremor: false
        );

        var without = WelcomeEffects.Plan(
            withTremor.Settings,
            withTremor.Baseline,
            new HashSet<WelcomeUse>(),
            hadTremor: true
        );

        without.Settings.Size.ShouldBe(SettingsSchema.Defaults.Size);
        without.Settings.Touch.Preset.ShouldBe(TouchPresets.Standard.Id);
    }

    [Fact]
    [Trait("Req", "BIE-005")]
    public void A_preset_that_does_not_change_keeps_the_values_the_person_tuned()
    {
        var tuned = SettingsSchema.Defaults with
        {
            Touch = SettingsSchema.Defaults.Touch with
            {
                Preset = TouchPresets.MildTremor.Id,
                HitSlopPx = SettingsSchema.Defaults.Touch.HitSlopPx + 1,
            },
        };

        var plan = WelcomeEffects.Plan(
            tuned,
            null,
            new HashSet<WelcomeUse> { WelcomeUse.Touch },
            hadTremor: false
        );

        plan.Settings.Touch.ShouldBe(tuned.Touch);
        plan.Changes.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "BIE-005")]
    public void Without_a_keyboard_the_library_and_the_ai_come_before_typing()
    {
        WelcomeEffects.PrefersLibraryOverTyping(SettingsSchema.Defaults).ShouldBeFalse();
        WelcomeEffects
            .PrefersLibraryOverTyping(
                WelcomeEffects.Apply(
                    SettingsSchema.Defaults,
                    new HashSet<WelcomeUse> { WelcomeUse.NoKeyboard }
                )
            )
            .ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "BIE-010")]
    public void The_recorded_answers_map_one_to_one_to_the_options()
    {
        var all = Enum.GetValues<WelcomeUse>();

        var answers = WelcomeAnswers.Create(
            WelcomeEffects.ToAnswers(all),
            ["basics"],
            WelcomeBaseline.Of(SettingsSchema.Defaults)
        );

        answers.Uses.Select(a => a.ToString()).ShouldBe(all.Select(u => u.ToString()));
        WelcomeEffects.UsesOf(answers).ShouldBe(all, ignoreOrder: true);
    }
}
