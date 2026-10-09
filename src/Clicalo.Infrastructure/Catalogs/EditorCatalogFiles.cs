using Clicalo.Application.UseCases.Editor;
using Clicalo.Application.UseCases.Library;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Infrastructure.Catalogs;

/// <summary>
/// The data of the shortcut editor from the <c>catalogs</c> and <c>content</c> folders next to the executable (D17),
/// read once off the UI thread when the Control Center first opens. Whatever is missing gives its empty form.
/// </summary>
public static class EditorCatalogFiles
{
    /// <summary>The icon library.</summary>
    public const string IconsFileName = "icons.json";

    /// <summary>The mouse actions.</summary>
    public const string MouseFileName = "mouse.json";

    /// <summary>The «Añadir atajo» library, in the content folder.</summary>
    public const string LibraryFileName = "library.json";

    /// <summary>The icons of combinations of each programs language: <c>combo-icons.&lt;lang&gt;.json</c>.</summary>
    public const string ComboIconsPattern = "combo-icons.*.json";

    /// <summary>Reads the editor's data.</summary>
    /// <param name="catalogs">The <c>catalogs</c> folder.</param>
    /// <param name="content">The <c>content</c> folder, or null when it is not there.</param>
    /// <param name="starter">The starter content the panel already read, or null.</param>
    /// <param name="keyLabels">The key labels the panel already read.</param>
    public static EditorCatalogs Load(
        string catalogs,
        string? content,
        StarterContent? starter,
        KeyLabelCatalog keyLabels
    )
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(keyLabels);
        var icons = Read(Path.Combine(catalogs, IconsFileName)) is { } iconBytes
            ? EditorCatalogsReader.ReadIcons(iconBytes)
            : null;
        var combos = new List<(LangCode, KeyChord, Domain.Catalog.IconRef)>();
        if (Directory.Exists(catalogs))
        {
            foreach (
                var file in Directory
                    .EnumerateFiles(catalogs, ComboIconsPattern)
                    .Order(StringComparer.Ordinal)
            )
            {
                if (
                    Read(file) is { } bytes
                    && EditorCatalogsReader.ReadComboIcons(bytes) is { } entries
                )
                {
                    combos.AddRange(entries);
                }
            }
        }

        var mouse = Read(Path.Combine(catalogs, MouseFileName)) is { } mouseBytes
            ? EditorCatalogsReader.ReadMouse(mouseBytes)
            : null;
        var library =
            content is not null && Read(Path.Combine(content, LibraryFileName)) is { } libraryBytes
                ? EditorCatalogsReader.ReadLibrary(libraryBytes)
                : null;
        return new EditorCatalogs(
            icons ?? IconCatalog.Empty,
            new ComboIconTable(combos),
            library ?? LibraryContent.Empty,
            starter,
            keyLabels,
            mouse is null ? [] : [.. mouse]
        );
    }

    private static byte[]? Read(string path)
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
