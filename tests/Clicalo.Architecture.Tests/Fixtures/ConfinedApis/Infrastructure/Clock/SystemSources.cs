namespace Clicalo.Architecture.Tests.Fixtures.ConfinedApis.Infrastructure.Clock;

/// <summary>Fixture: an adapter, allowed to read the system clock and create ids.</summary>
public static class SystemSources
{
    public static DateTimeOffset Now() => DateTimeOffset.UtcNow;

    public static Guid NewId() => Guid.NewGuid();
}
