using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Tests.Settings;

/// <summary>
/// The AI provider setting (ADR-0033): Anthropic by default, as before schema 1.2, and a consent that belongs to the
/// provider, so writing another provider clears it and writing the same one keeps it.
/// </summary>
[Trait("Req", "PLA-004")]
public sealed class AiProviderSettingTests
{
    private static readonly UserSettings Consented = SettingsSchema.Defaults with
    {
        Ai = SettingsSchema.Defaults.Ai with { Consent = true },
    };

    [Fact]
    public void The_default_is_anthropic()
    {
        SettingsSchema.Defaults.Ai.Provider.ShouldBe(AiProvider.Anthropic);
        SettingsSchema.Find(SettingPaths.AiProvider).ShouldNotBeNull().Undoable.ShouldBeTrue();
    }

    [Fact]
    public void Another_provider_clears_the_consent()
    {
        SettingsSchema
            .Write(Consented, SettingPaths.AiProvider, AiProvider.Gemini, out var written)
            .ShouldBe(SettingWriteStatus.Written);

        written.Ai.Provider.ShouldBe(AiProvider.Gemini);
        written.Ai.Consent.ShouldBeFalse();
    }

    [Fact]
    public void The_same_provider_keeps_the_consent()
    {
        _ = SettingsSchema.Write(
            Consented,
            SettingPaths.AiProvider,
            AiProvider.Anthropic,
            out var written
        );

        written.Ai.Consent.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "REG-07")]
    public void Undoing_the_change_brings_back_the_provider_and_its_consent()
    {
        _ = SettingsSchema.Write(
            Consented,
            SettingPaths.AiProvider,
            AiProvider.Gemini,
            out var changed
        );

        SettingsSchema.WithUndoableFrom(changed, Consented).Ai.ShouldBe(Consented.Ai);
    }

    [Fact]
    [Trait("Req", "DAT-003")]
    public void An_unknown_provider_is_repaired_to_the_default()
    {
        _ = SettingsSchema.Write(
            Consented,
            SettingPaths.AiProvider,
            (AiProvider)42,
            out var written
        );

        written.Ai.Provider.ShouldBe(AiProvider.Anthropic);
    }
}
