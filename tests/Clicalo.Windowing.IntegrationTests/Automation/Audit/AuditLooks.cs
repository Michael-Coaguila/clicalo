using Clicalo.Domain.Settings;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>What each <see cref="AuditLook"/> is: its theme and its text size.</summary>
internal static class AuditLooks
{
    /// <summary>The looks that are not the design as drawn: large text in the light and in the contrast theme.</summary>
    public static IReadOnlyList<AuditLook> LargeText { get; } =
    [AuditLook.LightLargeText, AuditLook.ContrastLargeText];

    /// <summary>The text scale of <paramref name="look"/>, in percent.</summary>
    public static int TextScalePercent(AuditLook look) =>
        look == AuditLook.Dark ? TypeScale.MinScalePercent : TypeScale.MaxScalePercent;

    /// <summary>The theme the person chose in <paramref name="look"/>.</summary>
    public static ThemeChoice Choice(AuditLook look) =>
        look switch
        {
            AuditLook.Dark => ThemeChoice.Dark,
            AuditLook.LightLargeText => ThemeChoice.Light,
            AuditLook.ContrastLargeText => ThemeChoice.HighContrast,
            _ => throw new ArgumentOutOfRangeException(nameof(look), look, message: null),
        };

    /// <summary>A theme service of the current thread with <paramref name="look"/>, over a system with no contrast theme.</summary>
    public static ThemeService Theme(AuditLook look) =>
        new(new FakeSystemTheme(), Choice(look), TextScalePercent(look), reduceMotion: true);
}
