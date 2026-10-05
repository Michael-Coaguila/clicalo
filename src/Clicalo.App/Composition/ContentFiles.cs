using System.IO;
using Clicalo.Infrastructure.Catalogs;

namespace Clicalo.App.Composition;

/// <summary>
/// The <c>content</c> folder next to the executable: the project copies the starter content of <c>data/content</c>
/// there on build and on publish (<c>starter.json</c>, <c>seed.json</c> and <c>templates</c>; D17: content is data
/// loaded at run time).
/// </summary>
internal static class ContentFiles
{
    private const string FolderName = "content";

    /// <summary>The folder with the starter content, or <see langword="null"/> when there is none.</summary>
    /// <param name="baseDirectory">The folder of the executable.</param>
    public static string? Find(string baseDirectory)
    {
        var shipped = Path.Combine(baseDirectory, FolderName);
        return StarterContentFiles.Exists(shipped) ? shipped : null;
    }
}
