using System.Collections.Immutable;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Settings;

namespace Clicalo.Application.UseCases.Welcome;

/// <summary>
/// What the answers of «¿Cómo usas tu equipo?» change (BIE-005, docs/06). Pure:
/// <list type="bullet">
/// <item>the touch preset: Temblor fuerte with tremor; otherwise Temblor leve with touch or without a keyboard;
/// otherwise Estándar, with its four values (TAC-001);</item>
/// <item>numbers for voice = Control por voz; <c>noKeyboardUser</c> = No puedo usar el teclado;</item>
/// <item>size L with tremor; otherwise the size stays.</item>
/// </list>
/// </summary>
public static class WelcomeEffects
{
    /// <summary>The touch preset of <paramref name="uses"/>.</summary>
    /// <param name="uses">The marked options.</param>
    public static TouchPreset PresetFor(IReadOnlySet<WelcomeUse> uses)
    {
        ArgumentNullException.ThrowIfNull(uses);
        if (uses.Contains(WelcomeUse.Tremor))
        {
            return TouchPresets.StrongTremor;
        }

        return uses.Contains(WelcomeUse.Touch) || uses.Contains(WelcomeUse.NoKeyboard)
            ? TouchPresets.MildTremor
            : TouchPresets.Standard;
    }

    /// <summary>The settings with the effects of <paramref name="uses"/>.</summary>
    /// <param name="settings">The current settings.</param>
    /// <param name="uses">The marked options.</param>
    public static UserSettings Apply(UserSettings settings, IReadOnlySet<WelcomeUse> uses)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var preset = PresetFor(uses);
        return settings with
        {
            Touch = new TouchFilterSettings(
                preset.Id,
                preset.Debounce,
                preset.HitSlopPx,
                preset.CancelMovePx,
                preset.MinContact
            ),
            VoiceNumbers = uses.Contains(WelcomeUse.Voice),
            NoKeyboardUser = uses.Contains(WelcomeUse.NoKeyboard),
            Size = uses.Contains(WelcomeUse.Tremor)
                ? Domain.Settings.PanelSize.Large
                : settings.Size,
        };
    }

    /// <summary>
    /// The answers that <paramref name="settings"/> reflect, shown marked when the welcome is repeated (BIE-010):
    /// without a keyboard, voice and tremor (the strong preset). Touch and mouse leave no trace of their own (the mild
    /// preset is also the default), and the answers are not stored apart, so they are not marked. A new user sees
    /// nothing marked (BIE-005).
    /// </summary>
    /// <param name="settings">The current settings.</param>
    public static ImmutableHashSet<WelcomeUse> Read(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var uses = ImmutableHashSet.CreateBuilder<WelcomeUse>();
        if (settings.NoKeyboardUser)
        {
            _ = uses.Add(WelcomeUse.NoKeyboard);
        }

        if (settings.VoiceNumbers)
        {
            _ = uses.Add(WelcomeUse.Voice);
        }

        if (
            string.Equals(
                settings.Touch.Preset,
                TouchPresets.StrongTremor.Id,
                StringComparison.Ordinal
            )
        )
        {
            _ = uses.Add(WelcomeUse.Tremor);
        }

        return uses.ToImmutable();
    }
}
