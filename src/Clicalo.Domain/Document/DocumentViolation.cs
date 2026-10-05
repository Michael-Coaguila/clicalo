namespace Clicalo.Domain.Document;

/// <summary>A broken invariant found by <see cref="UserDocument.Validate"/>.</summary>
/// <param name="Invariant">Which one.</param>
/// <param name="Detail">Ids and paths involved; never user text (LOG-001).</param>
public sealed record DocumentViolation(DocumentInvariant Invariant, string Detail);
