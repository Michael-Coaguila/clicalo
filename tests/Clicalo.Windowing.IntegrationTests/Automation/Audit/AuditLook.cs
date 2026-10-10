namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>
/// The theme and the text size a view is audited with (TEM-001, CUA-011). What changes the layout is the text size
/// and, in high contrast, the thicker borders; the two looks with large text are the worst case of each.
/// </summary>
public enum AuditLook
{
    /// <summary>Dark theme, text at 100 %: the design as drawn.</summary>
    Dark,

    /// <summary>Light theme, text at its largest (150 %).</summary>
    LightLargeText,

    /// <summary>The app's high contrast theme (2 px borders), text at its largest (150 %).</summary>
    ContrastLargeText,
}
