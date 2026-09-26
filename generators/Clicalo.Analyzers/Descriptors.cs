using Microsoft.CodeAnalysis;

namespace Clicalo.Analyzers;

/// <summary>Every diagnostic the Clícalo rules can report. Each id has its section in docs/guides/analyzers.md.</summary>
internal static class Descriptors
{
    private const string HelpBase =
        "https://github.com/Michael-Coaguila/clicalo/blob/main/docs/guides/analyzers.md#";

    // ---- CLC0001 · Windowing (REG-01) ------------------------------------------------------------------------

    private const string WindowingTitle = "Non-activating windows are shown only with ShowPassive";

    public static readonly DiagnosticDescriptor ActivatingCall = new(
        DiagnosticIds.NonActivatingWindow,
        WindowingTitle,
        "'{0}()' can activate '{1}', which is a NonActivatingWindow; show it with ShowPassive() and never activate or focus it (REG-01)",
        DiagnosticCategories.Windowing,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Panel surfaces derive from NonActivatingWindow and must never take the foreground or the keyboard focus. "
            + "Activate, Show, ShowDialog and Focus go through the activating path of WPF; ShowPassive shows the window with SWP_NOACTIVATE.",
        helpLinkUri: HelpBase + "clc0001"
    );

    public static readonly DiagnosticDescriptor ActivatingProperty = new(
        DiagnosticIds.NonActivatingWindow,
        WindowingTitle,
        "Setting '{0}' on '{1}' can activate a NonActivatingWindow; keep ShowActivated false and use ShowPassive() or HidePassive() (REG-01)",
        DiagnosticCategories.Windowing,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Setting ShowActivated to anything but false, or Visibility to anything but Hidden or Collapsed, "
            + "shows the window through the activating path of WPF.",
        helpLinkUri: HelpBase + "clc0001"
    );

    // ---- CLC0003 · Privacy (LOG-001) -------------------------------------------------------------------------

    public static readonly DiagnosticDescriptor SensitiveData = new(
        DiagnosticIds.SensitiveData,
        "Sensitive values never reach logs or exceptions",
        "'{0}' has the sensitive type '{1}' and must not reach '{2}'; log an identifier or a length instead (LOG-001)",
        DiagnosticCategories.Privacy,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Values whose type carries [Sensitive] (SecretText, Sensitive<T>, window titles, searches, keys) must not be passed, "
            + "interpolated or concatenated into logging APIs, trace and ETW sinks, or exception constructors.",
        helpLinkUri: HelpBase + "clc0003"
    );

    // ---- CLC0004 · Timing (NFR-020) --------------------------------------------------------------------------

    public static readonly DiagnosticDescriptor DurationLiteral = new(
        DiagnosticIds.DurationLiteral,
        "Durations come from Timings",
        "The duration '{0}' used for '{1}' is a literal; use a constant generated in Clicalo.Domain.Timings from data/catalogs/timings.json (NFR-020)",
        DiagnosticCategories.Timing,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every time threshold is defined once in timings.json and generated as a constant. "
            + "Application and Presentation code must not write durations or duration thresholds as numeric literals or local constants.",
        helpLinkUri: HelpBase + "clc0004"
    );

    // ---- CLC0006 · Presentation (IDI-002, TEM-002) -----------------------------------------------------------

    private const string PresentationTitle = "Visible text and colors come from data";

    private const string PresentationDescription =
        "Product text lives only in data/i18n and colors only in the theme tokens. "
        + "Presentation and UI code must not assign literal text to visible properties or write literal colors.";

    public static readonly DiagnosticDescriptor LiteralText = new(
        DiagnosticIds.PresentationLiteral,
        PresentationTitle,
        "The literal text \"{0}\" reaches '{1}'; resolve a MessageKey from data/i18n instead (IDI-002)",
        DiagnosticCategories.Presentation,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: PresentationDescription,
        helpLinkUri: HelpBase + "clc0006"
    );

    public static readonly DiagnosticDescriptor LiteralColor = new(
        DiagnosticIds.PresentationLiteral,
        PresentationTitle,
        "The literal color '{0}' bypasses the theme; use a token from data/tokens instead (TEM-002)",
        DiagnosticCategories.Presentation,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: PresentationDescription,
        helpLinkUri: HelpBase + "clc0006"
    );

    // ---- CLC0010 · Safety (REG-04) ---------------------------------------------------------------------------

    private const string SafetyTitle =
        "Destructive commands are dispatched only with a confirmation token";

    private const string SafetyDescription =
        "Nothing destructive happens with a single tap. A command that implements IDestructiveCommand travels with the "
        + "ConfirmationToken that only TwoStepConfirm issues on the second tap.";

    public static readonly DiagnosticDescriptor MissingConfirmationToken = new(
        DiagnosticIds.DestructiveCommand,
        SafetyTitle,
        "The destructive command '{0}' is passed to '{1}' without a ConfirmationToken; use the overload that takes the token issued by TwoStepConfirm (REG-04)",
        DiagnosticCategories.Safety,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: SafetyDescription,
        helpLinkUri: HelpBase + "clc0010"
    );

    public static readonly DiagnosticDescriptor ForgedConfirmationToken = new(
        DiagnosticIds.DestructiveCommand,
        SafetyTitle,
        "'{0}' produces a ConfirmationToken outside TwoStepConfirm; only TwoStepConfirm may issue one (REG-04)",
        DiagnosticCategories.Safety,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: SafetyDescription,
        helpLinkUri: HelpBase + "clc0010"
    );
}
