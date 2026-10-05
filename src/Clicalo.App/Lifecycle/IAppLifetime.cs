namespace Clicalo.App.Lifecycle;

/// <summary>
/// The only way Clícalo ends (blueprint §4.4: <c>Environment.Exit</c> and <c>Application.Shutdown</c> are banned
/// outside <c>App/Lifecycle</c>). <see cref="ExitAsync"/> guarantees «Release all» before the process ends
/// (SEG-006, SEG-007): the engine releases everything, the document is flushed, and whatever could not be released in
/// time stays down until Sentinel releases it, seeing the process exit with code 0 (ADR-0023).
/// </summary>
internal interface IAppLifetime
{
    /// <summary>Releases everything, flushes the document, closes every window and ends the process.</summary>
    Task ExitAsync();
}
