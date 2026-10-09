using Clicalo.Domain.Settings;

namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// What <see cref="UpdateService"/> needs from the installer (Velopack, ADR-0027): read a channel, download with the
/// verification of the package and hand the installation over to the updater once this process ends. Tests use a fake.
/// </summary>
internal interface IUpdateClient
{
    /// <summary>Whether this copy was installed with the installer.</summary>
    bool IsInstalled { get; }

    /// <summary>The running version.</summary>
    string CurrentVersion { get; }

    /// <summary>The release notes of the installed package (Markdown), or null.</summary>
    string? CurrentNotes { get; }

    /// <summary>
    /// The newest version of <paramref name="channel"/> (<paramref name="exactVersion"/> null), or exactly
    /// <paramref name="exactVersion"/> (the rollback, the only downgrade); null when there is none.
    /// </summary>
    /// <param name="channel">The channel of the settings.</param>
    /// <param name="exactVersion">The version to go back to, or null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="UpdateFailedException">The channel could not be read.</exception>
    Task<UpdateOffer?> FindAsync(
        UpdateChannel channel,
        string? exactVersion,
        CancellationToken cancellationToken
    );

    /// <summary>Downloads <paramref name="offer"/> and verifies its checksum.</summary>
    /// <param name="offer">What <see cref="FindAsync"/> found.</param>
    /// <param name="progress">Percent, 0 to 100.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <exception cref="UpdateFailedException">The download failed or the package is damaged.</exception>
    Task DownloadAsync(UpdateOffer offer, Action<int> progress, CancellationToken cancellationToken);

    /// <summary>Starts the updater, which waits for this process to end, installs and starts the new version.</summary>
    /// <param name="offer">The downloaded package.</param>
    void ApplyAfterExit(UpdateOffer offer);
}
