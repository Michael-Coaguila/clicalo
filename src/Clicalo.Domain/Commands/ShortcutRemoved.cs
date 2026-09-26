using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>A shortcut was removed (DeleteShortcut, DeleteDuplicate, DiscardDraft): the editor and the panel drop it.</summary>
/// <param name="Id">The removed shortcut.</param>
public sealed record ShortcutRemoved(ShortcutId Id) : DomainEvent;
