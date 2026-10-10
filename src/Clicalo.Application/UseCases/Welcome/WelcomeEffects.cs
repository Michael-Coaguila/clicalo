using System.Collections.Immutable;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Application.UseCases.Welcome;

/// <summary>
/// What the answers of «¿Cómo usas tu equipo?» change (BIE-005, docs/06). Pure:
/// <list type="bullet">
/// <item>the touch preset: Temblor fuerte with tremor; otherwise Temblor leve with touch or without a keyboard;
/// otherwise Estándar, with its four values (TAC-001);</item>
/// <item>numbers for voice = Control por voz; <c>noKeyboardUser</c> = No puedo usar el teclado;</item>
/// <item>size L with tremor; otherwise the size stays, or goes back to the default when it was the welcome that had
/// made it L (EC-BIE-01).</item>
/// </list>
/// An effect only reaches a setting the person did not change by hand after the welcome last set it
/// (<see cref="Plan"/>, BIE-010).
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

    /// <summary>The settings with every effect of <paramref name="uses"/>, as a first welcome applies them.</summary>
    /// <param name="settings">The current settings.</param>
    /// <param name="uses">The marked options.</param>
    public static UserSettings Apply(UserSettings settings, IReadOnlySet<WelcomeUse> uses) =>
        Plan(settings, null, uses, hadTremor: false).Settings;

    /// <summary>
    /// What passing step 1 with <paramref name="uses"/> does to <paramref name="settings"/> (BIE-005, BIE-010): each of
    /// the four settings takes its effect only while it still holds the value of <paramref name="baseline"/> (what the
    /// welcome last left there); one the person changed by hand afterwards is kept and reported. Without a baseline
    /// (a first welcome, or a document that never recorded one) every effect applies.
    /// </summary>
    /// <param name="settings">The current settings.</param>
    /// <param name="baseline">What the welcome last left in the four settings, or <see langword="null"/>.</param>
    /// <param name="uses">The marked options.</param>
    /// <param name="hadTremor">
    /// Whether the answers the baseline comes from had tremor: unmarking it then takes the size back to the default.
    /// </param>
    public static WelcomeStepPlan Plan(
        UserSettings settings,
        WelcomeBaseline? baseline,
        IReadOnlySet<WelcomeUse> uses,
        bool hadTremor
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(uses);
        var preset = PresetFor(uses);
        var size =
            uses.Contains(WelcomeUse.Tremor) ? Domain.Settings.PanelSize.Large
            : hadTremor ? SettingsSchema.Defaults.Size
            : settings.Size;
        var voice = uses.Contains(WelcomeUse.Voice);
        var noKeyboard = uses.Contains(WelcomeUse.NoKeyboard);
        var samePreset = Same(settings.Touch.Preset, preset.Id);

        var changes = new List<WelcomeSetting>();
        var kept = new List<WelcomeSetting>();
        var next = settings;
        var left = baseline ?? WelcomeBaseline.Of(settings);

        // Whether the effect reaches the setting; one that would change is reported as changed or as kept.
        bool Reaches(WelcomeSetting setting, bool untouched, bool differs)
        {
            if (differs)
            {
                (untouched ? changes : kept).Add(setting);
            }

            return untouched;
        }

        if (
            Reaches(
                WelcomeSetting.TouchPreset,
                baseline is null || Same(settings.Touch.Preset, baseline.TouchPreset),
                !samePreset
            )
        )
        {
            next = next with
            {
                Touch = samePreset
                    ? settings.Touch
                    : new TouchFilterSettings(
                        preset.Id,
                        preset.Debounce,
                        preset.HitSlopPx,
                        preset.CancelMovePx,
                        preset.MinContact
                    ),
            };
            left = left with { TouchPreset = preset.Id };
        }

        if (
            Reaches(
                WelcomeSetting.Size,
                baseline is null || settings.Size == baseline.Size,
                settings.Size != size
            )
        )
        {
            next = next with { Size = size };
            left = left with { Size = size };
        }

        if (
            Reaches(
                WelcomeSetting.VoiceNumbers,
                baseline is null || settings.VoiceNumbers == baseline.VoiceNumbers,
                settings.VoiceNumbers != voice
            )
        )
        {
            next = next with { VoiceNumbers = voice };
            left = left with { VoiceNumbers = voice };
        }

        if (
            Reaches(
                WelcomeSetting.NoKeyboard,
                baseline is null || settings.NoKeyboardUser == baseline.NoKeyboardUser,
                settings.NoKeyboardUser != noKeyboard
            )
        )
        {
            next = next with { NoKeyboardUser = noKeyboard };
            left = left with { NoKeyboardUser = noKeyboard };
        }

        return new WelcomeStepPlan(
            next,
            left,
            ValueListBuilder.From(changes),
            ValueListBuilder.From(kept)
        );
    }

    /// <summary>
    /// The answers that <paramref name="settings"/> reflect, for a repeated welcome on a document that never recorded
    /// them (written before schema 1.1, BIE-010): without a keyboard, voice and tremor (the strong preset). Touch and
    /// mouse leave no trace of their own (the mild preset is also the default), so they are not marked.
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

        if (Same(settings.Touch.Preset, TouchPresets.StrongTremor.Id))
        {
            _ = uses.Add(WelcomeUse.Tremor);
        }

        return uses.ToImmutable();
    }

    /// <summary>The marked options a document recorded (<see cref="WelcomeAnswers.Uses"/>).</summary>
    /// <param name="answers">The recorded answers.</param>
    public static ImmutableHashSet<WelcomeUse> UsesOf(WelcomeAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        return [.. answers.Uses.Items.Select(FromAnswer)];
    }

    /// <summary>The marked options as the document records them.</summary>
    /// <param name="uses">The marked options.</param>
    public static IEnumerable<WelcomeAnswer> ToAnswers(IEnumerable<WelcomeUse> uses)
    {
        ArgumentNullException.ThrowIfNull(uses);
        return uses.Select(use =>
            use switch
            {
                WelcomeUse.Touch => WelcomeAnswer.Touch,
                WelcomeUse.Voice => WelcomeAnswer.Voice,
                WelcomeUse.NoKeyboard => WelcomeAnswer.NoKeyboard,
                WelcomeUse.Tremor => WelcomeAnswer.Tremor,
                _ => WelcomeAnswer.Mouse,
            }
        );
    }

    /// <summary>
    /// «No puedo usar el teclado» (BIE-005, EDI-010): recording a combination with a physical keyboard is not offered,
    /// and creating a shortcut starts from the library and the AI instead of an empty editor. The setting is changed
    /// later in General (GEN-014).
    /// </summary>
    /// <param name="settings">The current settings.</param>
    public static bool PrefersLibraryOverTyping(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.NoKeyboardUser;
    }

    private static WelcomeUse FromAnswer(WelcomeAnswer answer) =>
        answer switch
        {
            WelcomeAnswer.Touch => WelcomeUse.Touch,
            WelcomeAnswer.Voice => WelcomeUse.Voice,
            WelcomeAnswer.NoKeyboard => WelcomeUse.NoKeyboard,
            WelcomeAnswer.Tremor => WelcomeUse.Tremor,
            _ => WelcomeUse.Mouse,
        };

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
