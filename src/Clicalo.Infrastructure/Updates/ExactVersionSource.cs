using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// A channel reduced to the full package of one version: how «Volver a la versión anterior» asks Velopack for an older
/// version (with <c>AllowVersionDowngrade</c> only in that flow, ACT-005).
/// </summary>
/// <param name="inner">The channel.</param>
/// <param name="version">The only version it offers.</param>
internal sealed class ExactVersionSource(IUpdateSource inner, string version) : IUpdateSource
{
    /// <inheritdoc />
    public async Task<VelopackAssetFeed> GetReleaseFeed(
        IVelopackLogger logger,
        string? appId,
        string channel,
        Guid? stagingId = null,
        VelopackAsset? latestLocalRelease = null
    )
    {
        var feed = await inner
            .GetReleaseFeed(logger, appId, channel, stagingId, latestLocalRelease)
            .ConfigureAwait(false);
        return new VelopackAssetFeed
        {
            Assets =
            [
                .. feed.Assets.Where(asset =>
                    asset.Type == VelopackAssetType.Full
                    && string.Equals(
                        asset.Version.ToString(),
                        version,
                        StringComparison.OrdinalIgnoreCase
                    )
                ),
            ],
        };
    }

    /// <inheritdoc />
    public Task DownloadReleaseEntry(
        IVelopackLogger logger,
        VelopackAsset releaseEntry,
        string localFile,
        Action<int> progress,
        CancellationToken cancelToken = default
    ) => inner.DownloadReleaseEntry(logger, releaseEntry, localFile, progress, cancelToken);
}
