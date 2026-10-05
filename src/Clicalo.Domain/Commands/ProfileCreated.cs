using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>A profile was created with a new id (CreateProfile).</summary>
/// <param name="Id">The new id.</param>
public sealed record ProfileCreated(ProfileId Id) : DomainEvent;
