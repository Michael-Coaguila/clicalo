using System.Globalization;
using Clicalo.Domain.Errors;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// Moves an unreadable document to <c>quarantine\clicalo.&lt;date&gt;.json.corrupt</c> (blueprint §6.5, DAT-003,
/// REG-08): never deletes it and never overwrites an earlier quarantined file.
/// </summary>
public sealed class QuarantineStore
{
    private readonly DataLocations _locations;
    private readonly TimeProvider _time;
    private readonly IAtomicFileSystem _files;

    /// <summary>Creates the store.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="time">Clock of the file names.</param>
    public QuarantineStore(DataLocations locations, TimeProvider time)
        : this(locations, time, AtomicFile.Disk) { }

    /// <summary>Creates the store over other file operations (S11).</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="time">Clock of the file names.</param>
    /// <param name="files">The file operations.</param>
    internal QuarantineStore(DataLocations locations, TimeProvider time, IAtomicFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(files);
        _locations = locations;
        _time = time;
        _files = files;
    }

    /// <summary>Moves <paramref name="path"/> into quarantine and returns the new path.</summary>
    /// <param name="path">The unreadable file.</param>
    public Result<string> Move(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var stamp = _time
            .GetUtcNow()
            .UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        try
        {
            _files.CreateDirectory(_locations.Quarantine);
            for (var copy = 0; ; copy++)
            {
                var suffix =
                    copy == 0 ? string.Empty : "-" + copy.ToString(CultureInfo.InvariantCulture);
                var target = Path.Combine(
                    _locations.Quarantine,
                    name + "." + stamp + suffix + extension + ".corrupt"
                );
                if (_files.Exists(target))
                {
                    continue;
                }

                _files.Move(path, target);
                return Results.Ok(target);
            }
        }
        catch (Exception ex) when (TransientIo.IsIo(ex))
        {
            return Results.Fail<string>(PersistenceFailures.Quarantine());
        }
    }
}
