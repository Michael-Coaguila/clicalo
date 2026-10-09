using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Clicalo.Application.Ports;

namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// <see cref="UpdateState"/> as a small JSON file next to the emergency copy (local, not roaming: the installed version
/// belongs to this machine), written with the atomic writer like every data file (blueprint §6.5).
/// </summary>
/// <param name="path">The file, <c>update.json</c>.</param>
/// <param name="writer">The atomic writer.</param>
internal sealed class UpdateStateFile(string path, IAtomicFileWriter writer) : IUpdateStateStore
{
    private const int MaxBytes = 4096;

    /// <inheritdoc />
    public UpdateState? Load()
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxBytes)
            {
                return null;
            }

            return Parse(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(UpdateState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        _ = await writer.WriteAsync(path, Write(state), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the JSON of a state; null when it is not one.</summary>
    /// <param name="utf8">The file.</param>
    internal static UpdateState? Parse(ReadOnlySpan<byte> utf8)
    {
        try
        {
            if (JsonNode.Parse(utf8) is not JsonObject json)
            {
                return null;
            }

            var last = (string?)json["lastRunVersion"];
            if (string.IsNullOrWhiteSpace(last))
            {
                return null;
            }

            var previous = (string?)json["previousVersion"];
            var updatedAt = (string?)json["updatedAtUtc"];
            return new UpdateState(
                last,
                string.IsNullOrWhiteSpace(previous) ? null : previous,
                DateTimeOffset.TryParse(
                    updatedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var at
                )
                    ? at
                    : null
            );
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The JSON of <paramref name="state"/>.</summary>
    /// <param name="state">The state.</param>
    internal static byte[] Write(UpdateState state)
    {
        var json = new JsonObject
        {
            ["lastRunVersion"] = state.LastRunVersion,
            ["previousVersion"] = state.PreviousVersion,
            ["updatedAtUtc"] = state.UpdatedAt?.UtcDateTime.ToString(
                "O",
                CultureInfo.InvariantCulture
            ),
        };
        return Encoding.UTF8.GetBytes(json.ToJsonString());
    }
}
