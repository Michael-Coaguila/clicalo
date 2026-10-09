using Clicalo.Domain.CommonActions;
using Clicalo.Domain.Keys;

namespace Clicalo.Infrastructure.Catalogs;

/// <summary>
/// The catalogs of the <c>catalogs</c> folder that ships next to the executable and that the panel reads at run time
/// (D17): the key labels (R-04) and the adaptive common actions (decision D4). A file that is missing or cannot be read
/// gives the empty catalog, never an exception: tiles then show key ids and every shortcut is sent as saved.
/// </summary>
public static class RuntimeCatalogFiles
{
    /// <summary>The key labels of <paramref name="folder"/>, or <see cref="KeyLabelCatalog.Empty"/>.</summary>
    /// <param name="folder">The catalogs folder.</param>
    public static KeyLabelCatalog LoadKeyLabels(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return ReadFile(Path.Combine(folder, KeyLabelsReader.FileName)) is { } bytes
            ? KeyLabelsReader.Read(bytes) ?? KeyLabelCatalog.Empty
            : KeyLabelCatalog.Empty;
    }

    /// <summary>The common actions of <paramref name="folder"/>, or <see cref="CommonActionTable.Empty"/>.</summary>
    /// <param name="folder">The catalogs folder.</param>
    public static CommonActionTable LoadCommonActions(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return ReadFile(Path.Combine(folder, CommonActionsReader.FileName)) is { } bytes
            ? CommonActionsReader.Read(bytes) ?? CommonActionTable.Empty
            : CommonActionTable.Empty;
    }

    private static byte[]? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
