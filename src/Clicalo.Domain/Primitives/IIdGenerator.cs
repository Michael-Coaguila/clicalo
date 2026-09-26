namespace Clicalo.Domain.Primitives;

/// <summary>
/// Source of new opaque ids (DAT-004, invariant I1). The adapter uses random ids; tests use a deterministic one. The
/// Domain never calls <c>Guid.NewGuid</c> (banned outside adapters).
/// </summary>
public interface IIdGenerator
{
    /// <summary>A profile id never used before in this document.</summary>
    ProfileId NewProfileId();

    /// <summary>A shortcut id never used before in this document.</summary>
    ShortcutId NewShortcutId();
}
