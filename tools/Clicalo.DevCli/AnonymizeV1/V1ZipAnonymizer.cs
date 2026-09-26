using System.IO.Compression;

namespace Clicalo.DevCli.AnonymizeV1;

/// <summary>
/// Anonymizes the v1 <c>.zip</c> backup: every <c>.json</c> entry through <see cref="V1Anonymizer"/>, in the same order
/// and with the same dates; entries that are not JSON are dropped (they cannot be checked), and an entry name that is
/// not one of the v1 file names becomes <c>entry-N.json</c>.
/// </summary>
internal static class V1ZipAnonymizer
{
    private const long MaxEntryBytes = 50L * 1024 * 1024;

    /// <summary>Anonymizes a zip.</summary>
    /// <param name="zip">The zip bytes.</param>
    /// <param name="publicNames">What may be kept.</param>
    /// <returns>The new zip, the stats of each JSON entry and how many entries were dropped.</returns>
    /// <exception cref="InvalidDataException">The zip or one of its JSON entries cannot be read.</exception>
    public static (byte[] Output, IReadOnlyList<AnonymizeStats> Entries, int Dropped) Anonymize(
        byte[] zip,
        PublicNames publicNames
    )
    {
        using var input = new ZipArchive(
            new MemoryStream(zip, writable: false),
            ZipArchiveMode.Read
        );
        using var buffer = new MemoryStream();
        var stats = new List<AnonymizeStats>();
        var dropped = 0;
        using (var output = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    dropped += entry.FullName.EndsWith('/') ? 0 : 1;
                    continue;
                }

                var (bytes, entryStats) = new V1Anonymizer(publicNames).Anonymize(Read(entry));
                stats.Add(entryStats);
                var name = IsV1FileName(entry.FullName)
                    ? entry.FullName
                    : "entry-"
                        + stats.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ".json";
                var copy = output.CreateEntry(name, CompressionLevel.Optimal);
                copy.LastWriteTime = entry.LastWriteTime;
                using var stream = copy.Open();
                stream.Write(bytes);
            }
        }

        return (buffer.ToArray(), stats, dropped);
    }

    /// <summary>Whether a name is one v1 wrote: <c>profiles.json</c> or <c>profiles.backup.xx.json</c>.</summary>
    private static bool IsV1FileName(string name)
    {
        if (string.Equals(name, "profiles.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        const string Prefix = "profiles.backup.";
        const string Suffix = ".json";
        return name.Length == Prefix.Length + 2 + Suffix.Length
            && name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            && name.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase)
            && char.IsAsciiLetter(name[Prefix.Length])
            && char.IsAsciiLetter(name[Prefix.Length + 1]);
    }

    private static byte[] Read(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var copy = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (copy.Length + read > MaxEntryBytes)
            {
                throw new InvalidDataException("A zip entry is too large for a v1 file.");
            }

            copy.Write(buffer, 0, read);
        }

        return copy.ToArray();
    }
}
