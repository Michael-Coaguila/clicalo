using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// The expected failures of the v1 conversion. Every one writes nothing and offers «Retry migration» (MIG-004,
/// BIE-002). The codes are stable; the text is a placeholder until the migration keys of catalog §9 exist in
/// <c>data/i18n</c> (the app package adds them).
/// </summary>
internal static class V1Failures
{
    /// <summary>The converted library or document broke an invariant.</summary>
    public const string InvalidCode = "migration.v1.invalid";

    /// <summary>The converted counts differ from the counts read (MIG-004).</summary>
    public const string CountMismatchCode = "migration.v1.count_mismatch";

    /// <summary>The converted library or document broke an invariant.</summary>
    public static Failure Invalid { get; } = Create(InvalidCode);

    /// <summary>The converted counts differ from the counts read (MIG-004).</summary>
    public static Failure CountMismatch { get; } = Create(CountMismatchCode);

    private static Failure Create(string code) =>
        new(
            code,
            L.Retry,
            FailureSeverity.Warning,
            FailureRecovery.Retry,
            FailureAnnouncement.Polite
        );
}
