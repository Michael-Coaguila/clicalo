using Clicalo.Domain.Commands;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Tests.Generators;

/// <summary>Domain contexts for applying commands in tests.</summary>
internal static class Contexts
{
    /// <summary>A context at <see cref="DomainGen.Now"/> with fresh sequential ids.</summary>
    public static DomainContext Fresh() =>
        new(DomainGen.Now, new SequentialIds(), LangCode.Es, LangCode.Es);
}
