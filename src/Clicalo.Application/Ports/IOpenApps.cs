using System.Collections.Immutable;

namespace Clicalo.Application.Ports;

/// <summary>
/// The apps open on the desktop (ATJ-006, PRB-003): visible top-level windows of other processes, one per process,
/// without Clícalo's own windows, the shell and the touch keyboard. Reads the desktop when asked; never polls.
/// </summary>
public interface IOpenApps
{
    /// <summary>The open apps, ordered by name.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<ImmutableArray<OpenApp>> ListAsync(CancellationToken cancellationToken);
}
