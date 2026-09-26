namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// Where the data lives (blueprint §6.5): <c>%AppData%\Clicalo\</c> in production (Roaming; survives uninstalling),
/// <c>%TEMP%\clicalo-dev</c> with <c>cl run</c>, a temporary folder in tests.
/// </summary>
/// <param name="Root">The data folder.</param>
public sealed record DataLocations(string Root)
{
    /// <summary>
    /// The local (non-roaming) folder, <c>%LocalAppData%\Clicalo\</c> in production: the emergency copy lives there
    /// so a Roaming folder locked by a sync client cannot block it too. <see langword="null"/> uses <see cref="Root"/>.
    /// </summary>
    public string? LocalRoot { get; init; }

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

    /// <summary><c>logs\</c>, with <c>clicalo.log</c> and its four rotations (LOG-001).</summary>
    public string Logs => Path.Combine(Root, "logs");

    /// <summary><c>logs\clicalo.log</c>: the current log file keeps this fixed name (blueprint §9.4).</summary>
    public string LogFile => Path.Combine(Logs, "clicalo.log");

    /// <summary>
    /// <c>pending\</c> under <see cref="LocalRoot"/>: the emergency copy written when saving keeps failing (§6.5).
    /// </summary>
    public string Pending => Path.Combine(LocalRoot ?? Root, "pending");

    /// <summary><c>pending\clicalo.json</c>: the newest document that could not be saved in its place.</summary>
    public string PendingDocument => Path.Combine(Pending, "clicalo.json");

    /// <summary>The production locations of the current Windows user.</summary>
    public static DataLocations ForCurrentUser() =>
        new(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Clicalo"
            )
        )
        {
            LocalRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Clicalo"
            ),
        };
}
