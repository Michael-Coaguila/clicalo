using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Document;

/// <summary>
/// The values the welcome left, when it ended, in the settings that the answers of step 1 change (BIE-005, BIE-010,
/// ADR-0028). A repeated welcome compares them with the current settings: a setting whose current value is still the
/// one of the baseline was not changed by hand afterwards, so the new answers may change it; any other one was, and the
/// welcome leaves it alone and says so.
/// </summary>
/// <param name="TouchPreset">The touch preset id (<c>touch.preset</c>, TAC-001).</param>
/// <param name="Size">The panel size (<c>size</c>, GEN-004).</param>
/// <param name="VoiceNumbers">Numbers for voice (<c>voiceNumbers</c>).</param>
/// <param name="NoKeyboardUser">The user cannot use the keyboard (<c>noKeyboardUser</c>).</param>
public sealed record WelcomeBaseline(
    string TouchPreset,
    PanelSize Size,
    bool VoiceNumbers,
    bool NoKeyboardUser
)
{
    /// <summary>The baseline of <paramref name="settings"/>: what they hold now in the four settings.</summary>
    /// <param name="settings">The settings when the welcome ends.</param>
    public static WelcomeBaseline Of(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            settings.Touch.Preset,
            settings.Size,
            settings.VoiceNumbers,
            settings.NoKeyboardUser
        );
    }
}
