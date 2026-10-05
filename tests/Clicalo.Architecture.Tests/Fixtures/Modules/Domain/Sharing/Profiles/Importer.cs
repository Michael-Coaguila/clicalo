using Clicalo.Architecture.Tests.Fixtures.Modules.Domain.Keys;

namespace Clicalo.Architecture.Tests.Fixtures.Modules.Domain.Sharing.Profiles;

/// <summary>Fixture: a dotted module that reaches Keys only through Library (transitive, allowed).</summary>
public sealed class Importer
{
    public KeyId? Last { get; set; }
}
