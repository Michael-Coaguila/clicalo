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
}
