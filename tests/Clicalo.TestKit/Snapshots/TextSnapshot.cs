using System.Runtime.CompilerServices;
using System.Text;

namespace Clicalo.TestKit.Snapshots;

/// <summary>
/// Golden-file assertions for text: the value is compared with
/// <c>Snapshots/&lt;TestFile&gt;.&lt;TestMethod&gt;.&lt;name&gt;.verified.txt</c> next to the test source file.
/// </summary>
/// <remarks>
/// Text is normalized before comparing and writing: line endings become <c>\n</c> and the text ends with exactly
/// one <c>\n</c>, so files are byte-stable across platforms (see <c>.gitattributes</c>). Files are UTF-8 without BOM.
/// On a mismatch the received text is written as <c>.received.txt</c> and <see cref="SnapshotMismatchException"/>
/// is thrown; with <c>CLICALO_ACCEPT_SNAPSHOTS=1</c> the verified file is overwritten instead.
/// </remarks>
public static class TextSnapshot
{
    /// <summary>Extension of text snapshot files.</summary>
    public const string Extension = "txt";

    private static readonly UTF8Encoding Utf8NoBom = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    /// <summary>Asserts that <paramref name="actual"/> matches the verified snapshot <paramref name="name"/> of the calling test.</summary>
    public static void Match(
        string actual,
        string name,
        [CallerFilePath] string sourceFilePath = "",
        [CallerMemberName] string testName = ""
    ) =>
        Match(
            actual,
            SnapshotLocation.ForTest(name, sourceFilePath, testName),
            SnapshotSettings.CurrentMode
        );

    /// <summary>Asserts against an explicit <paramref name="location"/> and <paramref name="mode"/>.</summary>
    public static void Match(string actual, SnapshotLocation location, SnapshotMode mode)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(location);

        var received = Normalize(actual);
        var verifiedPath = location.VerifiedPath(Extension);
        var verified = File.Exists(verifiedPath)
            ? Normalize(File.ReadAllText(verifiedPath, Utf8NoBom))
            : null;

        if (string.Equals(received, verified, StringComparison.Ordinal))
        {
            location.DeleteReceivedFiles();
            return;
        }

        if (mode == SnapshotMode.Accept)
        {
            location.Write(verifiedPath, Utf8NoBom.GetBytes(received));
            location.DeleteReceivedFiles();
            return;
        }

        var receivedPath = location.ReceivedPath(Extension);
        location.Write(receivedPath, Utf8NoBom.GetBytes(received));
        var detail = verified is null
            ? TextDiff.Preview(received)
            : TextDiff.Describe(verified, received);
        throw SnapshotMismatchException.For(location, verifiedPath, receivedPath, detail);
    }

    /// <summary>Line endings to <c>\n</c> and exactly one trailing <c>\n</c> (none for empty text).</summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var unified = text.ReplaceLineEndings("\n").TrimEnd('\n');
        return unified.Length == 0 ? string.Empty : unified + "\n";
    }
}
