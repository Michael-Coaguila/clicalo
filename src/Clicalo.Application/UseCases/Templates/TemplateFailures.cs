using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.UseCases.Templates;

/// <summary>The failures of Plantillas itself; store and command failures pass through unchanged.</summary>
internal static class TemplateFailures
{
    public const string NothingToInstallCode = "templates.nothing_to_install";

    public const string NoNameCode = "templates.blank.no_name";

    /// <summary>The final button with nothing checked (PLA-017 keeps it disabled).</summary>
    public static Failure NothingToInstall() =>
        new(
            NothingToInstallCode,
            L.PvEmpty,
            FailureSeverity.Info,
            FailureRecovery.None,
            FailureAnnouncement.Polite
        );

    /// <summary>[blankCreate] without a name (PLA-010 keeps it disabled).</summary>
    public static Failure NoName() =>
        new(NoNameCode, L.BlankPh, FailureSeverity.Info, FailureRecovery.None, FailureAnnouncement.Polite);
}
