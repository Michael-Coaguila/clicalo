using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>A shortcut was created with a new id (CreateShortcut, DuplicateShortcut): the editor switches to it.</summary>
/// <param name="Id">The new id.</param>
/// <param name="List">Where it was created.</param>
public sealed record ShortcutCreated(ShortcutId Id, ListRef List) : DomainEvent;
