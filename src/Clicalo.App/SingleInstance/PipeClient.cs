using System.Security.Principal;
using Clicalo.Platform.Windows.SingleInstance;
using Microsoft.Win32.SafeHandles;

namespace Clicalo.App.SingleInstance;

/// <summary>
/// What the server of the single-instance pipe learns about a connected client (blueprint §3.4): its Windows session,
/// the user of its token and its integrity level. A value that could not be read is <see langword="null"/>.
/// </summary>
/// <param name="SessionId">The client's session (<c>GetNamedPipeClientSessionId</c>).</param>
/// <param name="User">The user of the client's token.</param>
/// <param name="IntegrityLevel">The mandatory integrity level of the client (<c>0x2000</c> medium).</param>
internal sealed record PipeClient(uint? SessionId, SecurityIdentifier? User, uint? IntegrityLevel)
{
    /// <summary>Reads the client connected to <paramref name="pipe"/>.</summary>
    /// <param name="pipe">The server end of a connected pipe.</param>
    public static PipeClient Of(SafePipeHandle pipe)
    {
        uint? session = PipePeer.TryGetClientSessionId(pipe, out var id) ? id : null;
        if (!PipePeer.TryGetClientProcessId(pipe, out var process))
        {
            return new PipeClient(session, null, null);
        }

        return new PipeClient(
            session,
            ProcessIdentity.User(process),
            ProcessIdentity.IntegrityLevel(process)
        );
    }
}
