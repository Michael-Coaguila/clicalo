using Clicalo.Domain.Document;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Persistence.Dto;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>
/// <see cref="OnboardingState"/> ↔ <see cref="OnboardingDto"/> (BIE-*, ADR-0028). The answers of schema 1.1 are read
/// only when they are complete: without <c>baseline</c> (or with one of its values missing or unknown) there are no
/// answers, and a repeated welcome falls back to what it can read from the settings, as with a 1.0 document. Unknown
/// options of step 1 and empty kit ids are dropped.
/// </summary>
internal static class OnboardingMapper
{
    /// <summary>The welcome state of <paramref name="dto"/>.</summary>
    /// <param name="dto">The persisted state, or <see langword="null"/>.</param>
    public static OnboardingState Decode(OnboardingDto? dto) =>
        new(dto?.Completed ?? false) { Answers = DecodeAnswers(dto) };

    /// <summary>The persisted form of <paramref name="state"/>, with <paramref name="keep"/> as its unknown members.</summary>
    /// <param name="state">The welcome state.</param>
    /// <param name="keep">Members of a later minor to keep.</param>
    public static OnboardingDto Encode(
        OnboardingState state,
        Dictionary<string, System.Text.Json.JsonElement>? keep
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        var answers = state.Answers;
        return new OnboardingDto
        {
            Completed = state.Completed,
            Uses = answers is null
                ? null
                : [.. answers.Uses.Select(PersistedNames.WelcomeAnswer.Name)],
            Kit = answers is null ? null : [.. answers.Kit],
            Baseline = answers is null
                ? null
                : new WelcomeBaselineDto
                {
                    TouchPreset = answers.Baseline.TouchPreset,
                    Size = PersistedNames.Size.Name(answers.Baseline.Size),
                    VoiceNumbers = answers.Baseline.VoiceNumbers,
                    NoKeyboardUser = answers.Baseline.NoKeyboardUser,
                },
            Extra = keep,
        };
    }

    private static WelcomeAnswers? DecodeAnswers(OnboardingDto? dto)
    {
        if (
            dto?.Baseline
                is not {
                    TouchPreset: { Length: > 0 } preset,
                    VoiceNumbers: { } voice,
                    NoKeyboardUser: { } noKeyboard,
                } baseline
            || !PersistedNames.Size.TryParse(baseline.Size, out PanelSize size)
        )
        {
            return null;
        }

        var uses = new List<WelcomeAnswer>();
        foreach (var name in dto.Uses ?? [])
        {
            if (PersistedNames.WelcomeAnswer.TryParse(name, out var use))
            {
                uses.Add(use);
            }
        }

        return WelcomeAnswers.Create(
            uses,
            dto.Kit ?? [],
            new WelcomeBaseline(preset, size, voice, noKeyboard)
        );
    }
}
