using Clicalo.Application.Ports;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Updates;

namespace Clicalo.Infrastructure.Tests.Updates;

/// <summary>A channel in memory: the versions it offers, what failed and what was applied.</summary>
internal sealed class FakeUpdateClient : IUpdateClient
{
    public bool IsInstalled { get; set; } = true;

    public string CurrentVersion { get; set; } = "2.0.0";

    public string? CurrentNotes { get; set; } = "## es\n- Versión instalada\n## en\n- Installed version\n";

    /// <summary>The versions per channel, as the release feed lists them.</summary>
    public Dictionary<UpdateChannel, List<string>> Feed { get; } =
        new() { [UpdateChannel.Stable] = [], [UpdateChannel.Beta] = [] };

    public UpdateError? FindFails { get; set; }

    public UpdateError? DownloadFails { get; set; }

    public List<(UpdateChannel Channel, string? Exact)> Finds { get; } = [];

    public List<string> Downloaded { get; } = [];

    public List<string> Applied { get; } = [];

    public Task<UpdateOffer?> FindAsync(
        UpdateChannel channel,
        string? exactVersion,
        CancellationToken cancellationToken
    )
    {
        Finds.Add((channel, exactVersion));
        if (FindFails is { } error)
        {
            throw new UpdateFailedException(error);
        }

        var versions = Feed[channel];
        var version = exactVersion is null
            ? versions.OrderBy(v => v, Comparer<string>.Create(VersionOrder.Compare)).LastOrDefault()
            : versions.FirstOrDefault(v => string.Equals(v, exactVersion, StringComparison.Ordinal));
        return Task.FromResult(
            version is null
                ? null
                : new UpdateOffer(
                    version,
                    VersionOrder.Compare(version, CurrentVersion) < 0,
                    "<!-- date: 2026-10-01 -->\n## es\n- Novedad de " + version + "\n## en\n- News of " + version + "\n",
                    null
                )
        );
    }

    public Task DownloadAsync(UpdateOffer offer, Action<int> progress, CancellationToken cancellationToken)
    {
        if (DownloadFails is { } error)
        {
            throw new UpdateFailedException(error);
        }

        progress(50);
        progress(100);
        Downloaded.Add(offer.Version);
        return Task.CompletedTask;
    }

    public void ApplyAfterExit(UpdateOffer offer) => Applied.Add(offer.Version);
}
