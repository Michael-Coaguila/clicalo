namespace Clicalo.Domain.Tests.Generators;

/// <summary>
/// A generated command before it meets a document: <see cref="CommandFactory.Create"/> turns it into a concrete
/// command against the document it applies to, so sequences of seeds stay meaningful while the document changes.
/// </summary>
/// <param name="Kind">Index into <see cref="CommandFactory.Kinds"/>.</param>
/// <param name="A">First selector (usually which shortcut or profile).</param>
/// <param name="B">Second selector (a template, a target or a value).</param>
/// <param name="C">Third selector (usually a position).</param>
/// <param name="Flag">A yes/no choice (take over, invalid value…).</param>
internal sealed record CommandSeed(int Kind, int A, int B, int C, bool Flag);
