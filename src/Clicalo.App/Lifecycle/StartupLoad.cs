using Clicalo.Application.Ports;

namespace Clicalo.App.Lifecycle;

/// <summary>The document of this start and whether it still has to be written.</summary>
/// <param name="Load">The document read, created or migrated, with its outcome.</param>
/// <param name="SavePending">
/// The start created or migrated the document but could not write it: the autosave writes it at once and retries
/// (<c>PersistenceScheduler.MarkUnsaved</c>), so leaving without a change never loses it.
/// </param>
internal sealed record StartupLoad(DocumentLoad Load, bool SavePending);
