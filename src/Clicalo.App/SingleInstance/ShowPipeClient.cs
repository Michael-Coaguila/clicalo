using System.IO;
using System.IO.Pipes;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Windows.SingleInstance;

namespace Clicalo.App.SingleInstance;

/// <summary>
/// The client side of the single-instance pipe (blueprint §3.4, ADR-0010): a second start connects, checks with
/// <c>GetNamedPipeServerProcessId</c> that the server runs one of Clícalo's own executables, and only then asks it to
/// show the panel. A server that is not Clícalo means someone took the name: nothing is sent
/// (<see cref="ShowOutcome.Squatted"/>).
/// </summary>
/// <remarks>
/// M2 checks the path of the server's image against this executable (the per-user installation or a build). The
/// pinned publisher check of ADR-0010 and the protected copy of the system component join with
/// <c>Platform.Core/Trust</c> in M5 (ADR-0013). A squatted name is reported to the second start by its exit code only:
/// the log line and the notice of §3.4 come with the IPC of M4 (D-21).
/// </remarks>
internal static class ShowPipeClient
{
    /// <summary>Asks the running instance of <paramref name="identity"/> to show its panel.</summary>
    /// <param name="identity">The names of the instance.</param>
    /// <param name="ownImage">The full path of this executable.</param>
    /// <param name="time">Clock of the request timeout.</param>
    /// <param name="cancellationToken">Abandons the request.</param>
    public static async Task<ShowOutcome> RequestShowAsync(
        InstanceIdentity identity,
        string ownImage,
        TimeProvider time,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrEmpty(ownImage);
        using var timeout = new CancellationTokenSource(Timings.Ipc.IpcRequestTimeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token
        );
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                identity.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous
            );
            await pipe.ConnectAsync(linked.Token).ConfigureAwait(false);
            if (!IsOwnExecutable(pipe, ownImage))
            {
                return ShowOutcome.Squatted;
            }

            pipe.ReadMode = PipeTransmissionMode.Message;
            await pipe.WriteAsync(PipeProtocol.ShowRequest(), linked.Token).ConfigureAwait(false);
            await pipe.FlushAsync(linked.Token).ConfigureAwait(false);
            var buffer = new byte[Timings.Ipc.IpcMaxMessageBytes];
            var read = await pipe.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
            return PipeProtocol.ReadResponse(buffer.AsSpan(0, read)) == PipeStatus.Ok
                ? ShowOutcome.Shown
                : ShowOutcome.Refused;
        }
        catch (OperationCanceledException)
        {
            return ShowOutcome.Unreachable;
        }
        catch (IOException)
        {
            return ShowOutcome.Unreachable;
        }
        catch (UnauthorizedAccessException)
        {
            // The pipe exists but this user may not open it: it is not this user's Clícalo.
            return ShowOutcome.Squatted;
        }
    }

    /// <summary>Whether the server of <paramref name="pipe"/> runs <paramref name="ownImage"/>.</summary>
    private static bool IsOwnExecutable(NamedPipeClientStream pipe, string ownImage) =>
        PipePeer.TryGetServerProcessId(pipe.SafePipeHandle, out var serverProcess)
        && ProcessIdentity.ImagePath(serverProcess) is { } serverImage
        && string.Equals(
            Path.GetFullPath(serverImage),
            Path.GetFullPath(ownImage),
            StringComparison.OrdinalIgnoreCase
        );
}
