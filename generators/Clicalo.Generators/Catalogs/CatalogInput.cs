namespace Clicalo.Generators.Catalogs;

/// <summary>
/// Everything one generated output needs: whether the project asked for it, its main catalog and an optional
/// companion catalog (for example keys.win32.json for keys.json). Value equality keeps the pipeline incremental.
/// </summary>
/// <param name="Enabled">The project uses the Domain generator profile.</param>
/// <param name="PrimaryName">File name of the main catalog, reported when it is missing.</param>
/// <param name="Primary">The main catalog, or <see langword="null"/> when it is not in the compilation.</param>
/// <param name="Companion">The companion catalog, when the output has one and it is present.</param>
internal sealed record CatalogInput(
    bool Enabled,
    string PrimaryName,
    CatalogFile? Primary,
    CatalogFile? Companion
);
