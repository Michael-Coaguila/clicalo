using System.Globalization;
using System.Runtime.CompilerServices;

namespace Clicalo.TestKit.Snapshots;

/// <summary>
/// Where the files of one snapshot live: <c>&lt;Directory&gt;/&lt;BaseName&gt;.verified.&lt;ext&gt;</c> (versioned),
/// <c>&lt;BaseName&gt;.received.&lt;ext&gt;</c> and <c>&lt;BaseName&gt;.received.&lt;qualifier&gt;.&lt;ext&gt;</c>
/// (written on failure, ignored by git through the <c>*.received.*</c> pattern).
/// </summary>
public sealed record SnapshotLocation
{
    /// <summary>Folder created next to the test source file to hold its snapshots.</summary>
    public const string FolderName = "Snapshots";

    /// <summary>Creates a location, validating that <paramref name="baseName"/> is a plain file-name stem.</summary>
    public SnapshotLocation(string directory, string baseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        if (
            baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || baseName.Contains("..", StringComparison.Ordinal)
        )
        {
            throw new ArgumentException(
                "A snapshot name cannot contain path characters: '" + baseName + "'.",
                nameof(baseName)
            );
        }

        Directory = Path.GetFullPath(directory);
        BaseName = baseName;
    }

    /// <summary>Absolute folder of the snapshot files.</summary>
    public string Directory { get; }

    /// <summary><c>&lt;Test&gt;.&lt;name&gt;</c>, the stem shared by every file of this snapshot.</summary>
    public string BaseName { get; }

    /// <summary>
    /// The location for snapshot <paramref name="name"/> of the calling test:
    /// <c>&lt;test folder&gt;/Snapshots/&lt;TestFile&gt;.&lt;TestMethod&gt;.&lt;name&gt;</c>. Test files hold one class each, so the
    /// file name identifies the class.
    /// </summary>
    public static SnapshotLocation ForTest(
        string name,
        [CallerFilePath] string sourceFilePath = "",
        [CallerMemberName] string testName = ""
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var source = SourcePaths.Resolve(sourceFilePath);
        var directory = Path.Combine(Path.GetDirectoryName(source)!, FolderName);
        var baseName = string.Create(
            CultureInfo.InvariantCulture,
            $"{Path.GetFileNameWithoutExtension(source)}.{testName}.{name}"
        );
        return new SnapshotLocation(directory, baseName);
    }

    /// <summary>The versioned reference file, for example <c>Foo.Bar.menu.verified.txt</c>.</summary>
    public string VerifiedPath(string extension) =>
        Path.Combine(Directory, BaseName + ".verified." + extension);

    /// <summary>The file written when the value does not match, for example <c>Foo.Bar.menu.received.txt</c>.</summary>
    public string ReceivedPath(string extension) =>
        Path.Combine(Directory, BaseName + ".received." + extension);

    /// <summary>An extra failure artifact, for example <c>Foo.Bar.menu.received.diff.png</c>.</summary>
    public string ReceivedPath(string qualifier, string extension) =>
        Path.Combine(Directory, BaseName + ".received." + qualifier + "." + extension);

    /// <summary>Deletes the received files of this snapshot left by a previous failed run.</summary>
    public void DeleteReceivedFiles()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return;
        }

        foreach (
            var file in System.IO.Directory.EnumerateFiles(Directory, BaseName + ".received.*")
        )
        {
            File.Delete(file);
        }
    }

    /// <summary>Writes <paramref name="content"/> to <paramref name="path"/>, creating the folder if needed.</summary>
    public void Write(string path, ReadOnlySpan<byte> content)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, content);
        File.Move(temporary, path, overwrite: true);
    }
}
