using System.Collections.Immutable;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;

namespace Clicalo.Application.Store;

/// <summary>A published document change (blueprint §6.4): the save scheduler routes it by slice.</summary>
/// <param name="before">The previous document.</param>
/// <param name="after">The new document.</param>
/// <param name="slices">The slices that changed.</param>
/// <param name="events">What the command did.</param>
/// <param name="origin">What produced it.</param>
public sealed class DocumentChangedEventArgs(
    UserDocument before,
    UserDocument after,
    DocumentSlices slices,
    ImmutableArray<DomainEvent> events,
    ChangeOrigin origin
) : EventArgs
{
    /// <summary>The previous document.</summary>
    public UserDocument Before { get; } = before;

    /// <summary>The new document.</summary>
    public UserDocument After { get; } = after;

    /// <summary>The slices that changed.</summary>
    public DocumentSlices Slices { get; } = slices;

    /// <summary>What the command did.</summary>
    public ImmutableArray<DomainEvent> Events { get; } = events;

    /// <summary>What produced it.</summary>
    public ChangeOrigin Origin { get; } = origin;
}
