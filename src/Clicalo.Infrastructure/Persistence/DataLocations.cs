namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// Where the data lives (blueprint §6.5): <c>%AppData%\Clicalo\</c> in production (Roaming; survives uninstalling),
/// <c>%TEMP%\clicalo-dev</c> with <c>cl run</c>, a temporary folder in tests.
/// </summary>
/// <param name="Root">The data folder.</param>
public sealed record DataLocations(string Root)
{
    /// <summary><c>clicalo.json</c>.</summary>
    public string Document => Path.Combine(Root, "clicalo.json");

    /// <summary><c>clicalo.json.prev</c>, kept by <c>ReplaceFileW</c>.</summary>
    public string DocumentPrevious => Path.Combine(Root, "clicalo.json.prev");

    /// <summary><c>usage.json</c>.</summary>
    public string Usage => Path.Combine(Root, "usage.json");

    /// <summary><c>usage.json.prev</c>.</summary>
    public string UsagePrevious => Path.Combine(Root, "usage.json.prev");

    /// <summary><c>backups\</c>, with one folder per kind.</summary>
    public string Backups => Path.Combine(Root, "backups");

    /// <summary><c>quarantine\</c>: unreadable documents, never deleted.</summary>
    public string Quarantine => Path.Combine(Root, "quarantine");
}
