namespace Clicalo.Application.Ports;

/// <summary>
/// «Reabrir como administrador» on demand (LOG-007, EJE-013, user decision D7, ADR-0027): Windows asks with its UAC
/// every time, and only the installed executable is ever started elevated. The caller ends this instance after
/// <see cref="ElevationOutcome.Started"/>; the elevated one waits for it to end.
/// </summary>
public interface IElevatedRelaunch
{
    /// <summary>Whether this process already runs as administrator.</summary>
    bool IsElevated { get; }

    /// <summary>Checks the executable, asks Windows and starts the elevated instance.</summary>
    /// <param name="cancellationToken">Cancels before Windows is asked.</param>
    Task<ElevationOutcome> RelaunchAsync(CancellationToken cancellationToken);
}
