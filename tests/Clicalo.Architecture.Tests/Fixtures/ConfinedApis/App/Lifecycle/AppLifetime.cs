namespace Clicalo.Architecture.Tests.Fixtures.ConfinedApis.App.Lifecycle;

/// <summary>Fixture: the one namespace allowed to end the process.</summary>
public static class AppLifetime
{
    public static void Exit() => Environment.Exit(0);
}
