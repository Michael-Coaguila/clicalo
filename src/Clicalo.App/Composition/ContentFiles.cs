using System.IO;
using Clicalo.Infrastructure.Content;

namespace Clicalo.App.Composition;

/// <summary>
/// The <c>content</c> folder next to the executable: the project copies <c>data/content/seed.json</c> there on build and
/// on publish (D17: content is data loaded at run time).
/// </summary>
internal static class ContentFiles
{
    private const string FolderName = "content";

    /// <summary>The folder with <c>seed.json</c>, or <see langword="null"/> when there is none.</summary>
    /// <param name="baseDirectory">The folder of the executable.</param>
    public static string? Find(string baseDirectory)
    {
        var shipped = Path.Combine(baseDirectory, FolderName);
        return File.Exists(Path.Combine(shipped, SeedDocument.FileName)) ? shipped : null;
    }
}
