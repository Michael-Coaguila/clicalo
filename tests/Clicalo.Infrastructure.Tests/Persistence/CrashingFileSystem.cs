using Clicalo.Infrastructure.Persistence;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// The disk, with the process «dying» at a chosen point of the write protocol (S11): every step of
/// <see cref="IAtomicFileSystem"/> is split into the points where a real crash can stop it, with the partial effect a
/// crash leaves on disk (an empty or half-written temporary file; <c>ReplaceFileW</c> stopped after dropping the old
/// backup or after renaming the target). At the chosen point it throws <see cref="SimulatedCrash"/>, which the writer
/// does not handle (it is not an I/O error): the test then «restarts» with healthy file operations.
/// </summary>
internal sealed class CrashingFileSystem : IAtomicFileSystem
{
    private readonly IAtomicFileSystem _disk = AtomicFile.Disk;
    private int _point;

    /// <summary>Creates the file system.</summary>
    /// <param name="crashAt">The 1-based point that crashes; <see cref="int.MaxValue"/> only counts.</param>
    public CrashingFileSystem(int crashAt) => CrashAt = crashAt;

    /// <summary>The point that crashes.</summary>
    public int CrashAt { get; }

    /// <summary>The points passed so far.</summary>
    public int Points => _point;

    public bool Exists(string path) => _disk.Exists(path);

    public byte[]? ReadAllBytesOrNull(string path) => _disk.ReadAllBytesOrNull(path);

    public void CreateDirectory(string path)
    {
        Point();
        _disk.CreateDirectory(path);
    }

    public void WriteThrough(string path, ReadOnlySpan<byte> content)
    {
        Point();
        File.WriteAllBytes(path, []);
        Point();
        File.WriteAllBytes(path, content[..(content.Length / 2)].ToArray());
        Point();
        _disk.WriteThrough(path, content);
        Point();
    }

    public void Replace(string target, string replacement, string backup)
    {
        Point();
        if (File.Exists(backup))
        {
            File.Delete(backup);
        }

        Point();
        File.Move(target, backup);
        Point();
        File.Move(replacement, target);
        Point();
    }

    public void Move(string source, string target)
    {
        Point();
        _disk.Move(source, target);
        Point();
    }

    public void Delete(string path)
    {
        Point();
        _disk.Delete(path);
    }

    public IReadOnlyList<string> Files(string directory, string pattern) =>
        _disk.Files(directory, pattern);

    private void Point()
    {
        if (++_point == CrashAt)
        {
            throw new SimulatedCrash();
        }
    }

    /// <summary>The process died here.</summary>
    internal sealed class SimulatedCrash : Exception
    {
        public SimulatedCrash()
            : base("Simulated crash (S11).") { }
    }
}
