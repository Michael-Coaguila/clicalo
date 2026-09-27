namespace Clicalo.App.Lifecycle;

/// <summary>What the start reads for (<see cref="StartupReader"/>).</summary>
/// <param name="BaseDirectory">The folder of <c>Clicalo.exe</c>, with <c>i18n</c> and <c>content</c>.</param>
/// <param name="WindowsLanguage">The two-letter language of Windows, read on the UI thread (its culture).</param>
/// <param name="MigrateV1">The v1 file of <c>--migrate-v1</c>, or <see langword="null"/>.</param>
/// <param name="AfterCrash">The crash of <c>--after-crash</c>, or <see langword="null"/>.</param>
internal sealed record StartupRequest(
    string BaseDirectory,
    string WindowsLanguage,
    string? MigrateV1,
    DateTimeOffset? AfterCrash
);
