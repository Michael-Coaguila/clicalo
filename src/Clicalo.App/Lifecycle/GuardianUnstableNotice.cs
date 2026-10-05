using Clicalo.Application.Coordinators;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// Sentinel died too often and is no longer restarted (<c>Timings.Guardian.RestartLoop</c>, D-22): from then on a death
/// of the process leaves keys down with nobody to release them. The state is logged for the diagnostics and the user is told at once, on the
/// panel and to screen readers, that the key protection is off until Clícalo starts again (REG-03).
/// </summary>
internal sealed partial class GuardianUnstableNotice : IDisposable
{
    private readonly IGuardian _guardian;
    private readonly EngineObserverRelay _relay;
    private readonly ILogger _logger;

    /// <summary>Starts listening to <paramref name="guardian"/>.</summary>
    /// <param name="guardian">The Sentinel supervisor.</param>
    /// <param name="relay">Queues the notice to the panel (its <c>NoticeRaised</c>).</param>
    /// <param name="logger">The host's log.</param>
    public GuardianUnstableNotice(IGuardian guardian, EngineObserverRelay relay, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(relay);
        ArgumentNullException.ThrowIfNull(logger);
        _guardian = guardian;
        _relay = relay;
        _logger = logger;
        _guardian.Unstable += OnUnstable;
    }

    /// <inheritdoc />
    public void Dispose() => _guardian.Unstable -= OnUnstable;

    private void OnUnstable(object? sender, EventArgs e)
    {
        LogGuardianUnstable(_logger);
        _relay.OnNotice(L.GuardianUnstable, NoticeUrgency.Assertive);
    }

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Critical,
        Message = "guardian.unstable: Sentinel is no longer restarted"
    )]
    private static partial void LogGuardianUnstable(ILogger logger);
}
