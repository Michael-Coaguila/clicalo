using System.IO;
using Clicalo.Domain.CommonActions;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Templates;
using Clicalo.Infrastructure.Catalogs;

namespace Clicalo.App.Composition;

/// <summary>
/// The data the panel reads at run time from the folders next to the executable (D17), read once off the UI thread at
/// the start: the key labels of the tiles (R-04), the adaptive common actions (decision D4) and the starter content
/// whose templates the profile suggestion installs (PER-009). Whatever is missing gives its empty form: tiles then show
/// key ids, every shortcut is sent as saved and no profile is suggested.
/// </summary>
/// <param name="KeyLabels">The key labels.</param>
/// <param name="CommonActions">The common actions.</param>
/// <param name="Content">The starter content, or <see langword="null"/> when it could not be read.</param>
internal sealed record RuntimeCatalogs(
    KeyLabelCatalog KeyLabels,
    CommonActionTable CommonActions,
    StarterContent? Content
)
{
    /// <summary>The <c>catalogs</c> folder the project copies next to the executable.</summary>
    public const string FolderName = "catalogs";

    /// <summary>Nothing loaded (before the start reads the folders).</summary>
    public static RuntimeCatalogs Empty { get; } =
        new(KeyLabelCatalog.Empty, CommonActionTable.Empty, null);

    /// <summary>Reads the catalogs and the starter content next to the executable.</summary>
    /// <param name="baseDirectory">The folder of the executable.</param>
    public static RuntimeCatalogs Load(string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(baseDirectory);
        var catalogs = Path.Combine(baseDirectory, FolderName);
        return new RuntimeCatalogs(
            RuntimeCatalogFiles.LoadKeyLabels(catalogs),
            RuntimeCatalogFiles.LoadCommonActions(catalogs),
            ContentFiles.Find(baseDirectory) is { } content
                ? StarterContentFiles.Load(content)
                : null
        );
    }
}
