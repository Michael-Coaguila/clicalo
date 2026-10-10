namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>The settings step 1 of the welcome changes, as the welcome left them (BIE-010, schema 1.1, ADR-0028).</summary>
internal sealed record WelcomeBaselineDto
{
    public string? TouchPreset { get; init; }

    public string? Size { get; init; }

    public bool? VoiceNumbers { get; init; }

    public bool? NoKeyboardUser { get; init; }
}
