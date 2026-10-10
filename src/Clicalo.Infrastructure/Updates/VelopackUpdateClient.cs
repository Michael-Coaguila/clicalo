using Clicalo.Application.Ports;
using Clicalo.Domain.Settings;
using Velopack;
using Velopack.Exceptions;
using Velopack.Locators;
using Velopack.Sources;

namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// <see cref="IUpdateClient"/> over Velopack (ADR-0012 as amended by ADR-0027): the GitHub Releases of the public
/// repository over HTTPS (<see cref="GithubSource"/>; Beta also reads pre-releases), the explicit channel of the
/// settings, the SHA checksum Velopack checks after every download, and the updater (<c>Update.exe</c>) that waits for
/// this process to end. There is no code signature nor signed manifest in 2.0 (user decision D6).
/// </summary>
/// <param name="repository">The repository URL.</param>
/// <param name="enabled">False never looks for the installation: the copy behaves as not installed.</param>
internal sealed class VelopackUpdateClient(string repository, bool enabled) : IUpdateClient
{
    private readonly UpdateManager? _installed = enabled ? Probe(repository) : null;

    /// <inheritdoc />
    public bool IsInstalled => _installed?.IsInstalled ?? false;

    /// <inheritdoc />
    public string CurrentVersion => _installed?.CurrentVersion?.ToString() ?? ThisAssembly();

    /// <inheritdoc />
    public string? CurrentNotes
    {
        get
        {
            try
            {
                return IsInstalled && VelopackLocator.IsCurrentSet
                    ? VelopackLocator.Current.GetLatestLocalFullPackage()?.NotesMarkdown
                    : null;
            }
            catch (Exception ex)
                when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return null;
            }
        }
    }

    /// <inheritdoc />
    public async Task<UpdateOffer?> FindAsync(
        UpdateChannel channel,
        string? exactVersion,
        CancellationToken cancellationToken
    )
    {
        var manager = Manager(channel, exactVersion);
        UpdateInfo? info;
        try
        {
            info = await manager
                .CheckForUpdatesAsync()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            throw new UpdateFailedException(UpdateError.Offline, ex);
        }

        if (info is null)
        {
            return null;
        }

        var target = info.TargetFullRelease;
        return new UpdateOffer(
            target.Version.ToString(),
            info.IsDowngrade,
            target.NotesMarkdown,
            new Pending(manager, info)
        );
    }

    /// <inheritdoc />
    public async Task DownloadAsync(
        UpdateOffer offer,
        Action<int> progress,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(offer);
        var pending = (Pending)offer.Handle!;
        try
        {
            await pending
                .Manager.DownloadUpdatesAsync(pending.Info, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ChecksumFailedException ex)
        {
            throw new UpdateFailedException(UpdateError.Damaged, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateFailedException(UpdateError.Offline, ex);
        }
        catch (IOException ex) when ((ex.HResult & 0xFFFF) is 0x70 or 0x27)
        {
            throw new UpdateFailedException(UpdateError.NoSpace, ex);
        }
    }

    /// <inheritdoc />
    public void ApplyAfterExit(UpdateOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var pending = (Pending)offer.Handle!;
        pending.Manager.WaitExitThenApplyUpdates(
            pending.Info.TargetFullRelease,
            silent: true,
            restart: true
        );
    }

    private UpdateManager Manager(UpdateChannel channel, string? exactVersion)
    {
        IUpdateSource source = new GithubSource(
            repository,
            accessToken: null,
            prerelease: channel == UpdateChannel.Beta
        );
        if (exactVersion is not null)
        {
            source = new ExactVersionSource(source, exactVersion);
        }

        return new UpdateManager(
            source,
            new UpdateOptions
            {
                ExplicitChannel = UpdateChannels.NameOf(channel),
                AllowVersionDowngrade = exactVersion is not null,
            }
        );
    }

    private static UpdateManager? Probe(string repository)
    {
        try
        {
            return new UpdateManager(
                new GithubSource(repository, accessToken: null, prerelease: false)
            );
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Not installed, or the install folder cannot be read: no updates.
            return null;
        }
    }

    private static string ThisAssembly()
    {
        var version = typeof(VelopackUpdateClient).Assembly.GetName().Version;
        return version is null ? "0.0.0" : version.ToString(3);
    }

    /// <summary>What a found version needs to download and apply.</summary>
    private sealed record Pending(UpdateManager Manager, UpdateInfo Info);
}
