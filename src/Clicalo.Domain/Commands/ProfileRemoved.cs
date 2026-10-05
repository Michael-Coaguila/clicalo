using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>A profile was removed (DeleteProfile): the panel and the editor leave it (PER-008).</summary>
/// <param name="Id">The removed profile.</param>
public sealed record ProfileRemoved(ProfileId Id) : DomainEvent;
