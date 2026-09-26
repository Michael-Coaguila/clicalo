using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>A process was bound to a profile (BindProcess), possibly taken from another one (ATJ-007).</summary>
/// <param name="Profile">The profile that follows the process now.</param>
/// <param name="Process">The process.</param>
/// <param name="TakenFrom">The profile that had it, or <see langword="null"/>.</param>
public sealed record ProcessBound(ProfileId Profile, ProcessName Process, ProfileId? TakenFrom)
    : DomainEvent;
