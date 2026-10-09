using Clicalo.Application.Localization;
using Clicalo.Application.Ports;

namespace Clicalo.App.Composition;

/// <summary>
/// What the start learns before the rest of the graph can be built: the document read from disk and the interface
/// language it asks for. Set once by the lifecycle, read by the lazy registrations that depend on it.
/// </summary>
internal sealed class StartupSlot
{
    private DocumentLoad? _load;
    private LocalizationContext? _localization;

    /// <summary>The loaded document; set before anything that depends on it is resolved.</summary>
    public DocumentLoad Load
    {
        get => _load ?? throw new InvalidOperationException("The document is not loaded yet.");
        set =>
            _load = _load is null
                ? value
                : throw new InvalidOperationException("The document was already loaded.");
    }

    /// <summary>The key labels, the common actions and the starter content; empty until the start reads them.</summary>
    public RuntimeCatalogs Catalogs { get; set; } = RuntimeCatalogs.Empty;

    /// <summary>The interface language; set right after the document.</summary>
    public LocalizationContext Localization
    {
        get =>
            _localization
            ?? throw new InvalidOperationException("The language files are not loaded yet.");
        set =>
            _localization = _localization is null
                ? value
                : throw new InvalidOperationException("The language files were already loaded.");
    }
}
