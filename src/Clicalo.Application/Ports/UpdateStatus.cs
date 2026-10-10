using System.Collections.Immutable;

namespace Clicalo.Application.Ports;

/// <summary>The state of the updates, as the «Actualizaciones» tab shows it (ACT-001).</summary>
/// <param name="Phase">The card in view.</param>
/// <param name="CurrentVersion">The running version.</param>
/// <param name="NewVersion">The version found or being installed; null for none.</param>
/// <param name="Percent">The progress of the download, 0 to 100.</param>
/// <param name="Error">Why the last attempt failed, with <see cref="UpdatePhase.Failed"/>.</param>
/// <param name="LastChecked">When the channel was last read without error; null for never.</param>
/// <param name="RollbackVersion">The version [Volver] goes back to; null hides the row (ACT-005).</param>
/// <param name="Notes">«Novedades», newest first (ACT-004).</param>
public sealed record UpdateStatus(
    UpdatePhase Phase,
    string CurrentVersion,
    string? NewVersion,
    int Percent,
    UpdateError Error,
    DateTimeOffset? LastChecked,
    string? RollbackVersion,
    ImmutableArray<ReleaseNotes> Notes
)
{
    /// <summary>
    /// The operation of the two-tap confirmation of [Volver] (REG-04): the subject's target is the version it goes
    /// back to, and <see cref="IUpdateService.RollbackAsync"/> only accepts a token for it.
    /// </summary>
    public const string RollbackOperation = "RollbackVersion";

    /// <summary>Whether the side menu shows its counter (ACT-001: while there is a new version).</summary>
    public bool HasNewVersion => Phase == UpdatePhase.Found;

    /// <summary>A copy that was not installed with the installer: nothing to update.</summary>
    /// <param name="currentVersion">The running version.</param>
    public static UpdateStatus Unavailable(string currentVersion) =>
        new(UpdatePhase.Unavailable, currentVersion, null, 0, UpdateError.None, null, null, []);
}
