using System.Text;

namespace Clicalo.DevCli.Tests;

/// <summary>A throwaway folder under the system temp directory, laid out like the repository root.</summary>
internal sealed class TemporaryRepository : IDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public TemporaryRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), "clicalo-devcli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Write("Clicalo.slnx", "<Solution />\n");
    }

    public string Root { get; }

    /// <summary>Writes a UTF-8 file (without byte order mark unless <paramref name="bom"/>) and returns its path.</summary>
    public string Write(string relativePath, string content, bool bom = false)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, bom ? new UTF8Encoding(true) : Utf8NoBom);
        return path;
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                // git marks its objects read-only.
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder must not fail a test.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}
