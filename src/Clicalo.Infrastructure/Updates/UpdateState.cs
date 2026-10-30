namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// What the updates remember on this machine (<c>%LocalAppData%\Clicalo\update.json</c>): the version of the last run,
/// to notice the first run of a new version («Actualizado»), and the version before it with the moment of the update,
/// for «Volver a la versión anterior» (ACT-005).
/// </summary>
/// <param name="LastRunVersion">The version that ran last.</param>
/// <param name="PreviousVersion">The version before the last update; null after a rollback or a first install.</param>
/// <param name="UpdatedAt">When the last update was first run.</param>
/// <param name="DeclinedVersion">
/// The version the person went back from with [Volver]: it is still offered, but never installed by itself.
/// </param>
internal sealed record UpdateState(
    string LastRunVersion,
    string? PreviousVersion,
    DateTimeOffset? UpdatedAt,
    string? DeclinedVersion = null
);
