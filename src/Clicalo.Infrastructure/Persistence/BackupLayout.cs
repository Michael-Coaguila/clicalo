using System.Globalization;
using System.Text.RegularExpressions;
using Clicalo.Domain.Document;
using Clicalo.Domain.Timing;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// Where backups live and how they are named (blueprint §6.5, §6.8): <c>backups\&lt;kind&gt;\clicalo.&lt;UTC&gt;.&lt;seq&gt;.json</c>
/// with one folder per kind (<c>auto</c> keeps 12, <c>manual</c> all, each <c>pre-*</c> 10). The id of a backup is
/// <c>&lt;kind&gt;/&lt;file&gt;</c>; <c>seq</c> is shared by every kind and orders them newest first.
/// </summary>
internal static partial class BackupLayout
{
    private const string StampFormat = "yyyyMMdd'T'HHmmss'Z'";

    private static readonly (BackupKind Kind, string Folder)[] Folders =
    [
        (BackupKind.Auto, "auto"),
        (BackupKind.Manual, "manual"),
        (BackupKind.PreUpdate, "pre-update"),
        (BackupKind.PreMigrate, "pre-migrate"),
        (BackupKind.PreRestore, "pre-restore"),
        (BackupKind.PreImportReplace, "pre-import"),
        (BackupKind.PreResetFrequents, "pre-reset"),
        (BackupKind.PreRepair, "pre-repair"),
    ];

    /// <summary>Every kind, each with its own folder.</summary>
    public static IEnumerable<BackupKind> FolderKinds => Folders.Select(f => f.Kind);

    /// <summary>The folder name of <paramref name="kind"/>.</summary>
    /// <param name="kind">A kind.</param>
    public static string Folder(BackupKind kind) =>
        Folders.FirstOrDefault(f => f.Kind == kind).Folder
        ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown backup kind.");

    /// <summary>The full path of the folder of <paramref name="kind"/>.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="kind">A kind.</param>
    public static string FolderPath(DataLocations locations, BackupKind kind) =>
        Path.Combine(locations.Backups, Folder(kind));

    /// <summary>How many backups of <paramref name="kind"/> are kept, or <see langword="null"/> for all of them.</summary>
    /// <param name="kind">A kind.</param>
    public static int? Retention(BackupKind kind) =>
        kind switch
        {
            BackupKind.Auto => Timings.Backups.AutoBackupRetention,
            BackupKind.Manual => null,
            _ => Timings.Backups.OperationBackupRetention,
        };

    /// <summary>The file name of a backup taken at <paramref name="at"/> with <paramref name="seq"/>.</summary>
    /// <param name="at">When.</param>
    /// <param name="seq">Its sequence number.</param>
    public static string FileName(DateTimeOffset at, long seq) =>
        "clicalo."
        + at.UtcDateTime.ToString(StampFormat, CultureInfo.InvariantCulture)
        + "."
        + seq.ToString("D6", CultureInfo.InvariantCulture)
        + ".json";

    /// <summary>The id of a backup file.</summary>
    /// <param name="kind">Its kind.</param>
    /// <param name="fileName">Its file name.</param>
    public static string Id(BackupKind kind, string fileName) => Folder(kind) + "/" + fileName;

    /// <summary>Reads an id; only the names this layout produces are accepted (no other path can be named).</summary>
    /// <param name="id">A backup id.</param>
    /// <param name="entry">What it names.</param>
    public static bool TryParse(string? id, out Entry entry)
    {
        entry = default;
        if (id is null)
        {
            return false;
        }

        var match = IdPattern().Match(id);
        if (!match.Success)
        {
            return false;
        }

        var folder = match.Groups["folder"].Value;
        var kind = Folders.FirstOrDefault(f =>
            string.Equals(f.Folder, folder, StringComparison.Ordinal)
        );
        if (kind.Folder is null)
        {
            return false;
        }

        var at = DateTimeOffset.ParseExact(
            match.Groups["stamp"].Value,
            StampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );
        entry = new Entry(
            kind.Kind,
            at,
            long.Parse(match.Groups["seq"].Value, CultureInfo.InvariantCulture),
            folder + "/" + match.Groups["file"].Value
        );
        return true;
    }

    /// <summary>The full path of the backup named by <paramref name="entry"/>.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="entry">A parsed id.</param>
    public static string PathOf(DataLocations locations, Entry entry) =>
        Path.Combine(locations.Backups, entry.Id.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Every backup file of the folder kinds, parsed.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="files">The file operations.</param>
    public static IReadOnlyList<Entry> Scan(DataLocations locations, IAtomicFileSystem files)
    {
        var entries = new List<Entry>();
        foreach (var (kind, folder) in Folders)
        {
            foreach (
                var path in files.Files(Path.Combine(locations.Backups, folder), "clicalo.*.json")
            )
            {
                if (
                    TryParse(folder + "/" + Path.GetFileName(path), out var entry)
                    && entry.Kind == kind
                )
                {
                    entries.Add(entry);
                }
            }
        }

        return entries;
    }

    /// <summary>A backup id, parsed.</summary>
    /// <param name="Kind">Its kind.</param>
    /// <param name="CreatedAt">When it was taken (to the second).</param>
    /// <param name="Seq">Its sequence number.</param>
    /// <param name="Id">The id.</param>
    internal readonly record struct Entry(
        BackupKind Kind,
        DateTimeOffset CreatedAt,
        long Seq,
        string Id
    );

    [GeneratedRegex(
        @"^(?<folder>[a-z-]+)/(?<file>clicalo\.(?<stamp>\d{8}T\d{6}Z)\.(?<seq>\d{6,18})\.json)$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex IdPattern();
}
