using Clicalo.Architecture.Tests.Fixtures.Modules.Domain.Library;

namespace Clicalo.Architecture.Tests.Fixtures.Modules.Domain.Keys;

/// <summary>Fixture: Keys may not use Library (Library depends on Keys).</summary>
public sealed class KeyId
{
    public Shortcut? Owner { get; set; }
}
