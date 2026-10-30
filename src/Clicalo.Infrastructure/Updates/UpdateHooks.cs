using Clicalo.Domain.Settings;

namespace Clicalo.Infrastructure.Updates;

/// <summary>What <see cref="UpdateService"/> asks of the rest of the app (the composition root builds it).</summary>
/// <param name="Settings">The update settings of the document (ACT-002).</param>
/// <param name="IdleFor">How long the panel has gone without use (ACT-003: install only after 5 min).</param>
/// <param name="BackupBeforeInstall">
/// Writes a <c>pre-update</c> backup of the document and flushes it (ACT-003, DAT-006); false when it could not.
/// </param>
/// <param name="ExitForInstall">Ends this instance cleanly (release all, flush, exit code 0) so the updater can run.</param>
public sealed record UpdateHooks(
    Func<UpdateSettings> Settings,
    Func<TimeSpan> IdleFor,
    Func<CancellationToken, Task<bool>> BackupBeforeInstall,
    Func<Task> ExitForInstall
);
