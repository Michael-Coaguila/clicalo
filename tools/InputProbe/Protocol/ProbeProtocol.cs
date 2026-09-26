namespace Clicalo.Tools.InputProbe.Protocol;

/// <summary>
/// Wire contract between InputProbe and its client. This folder is compiled into the probe and linked into
/// <c>Clicalo.TestKit.Windows</c>, so both sides share a single definition of every name.
/// </summary>
/// <remarks>
/// Transport: the client creates a duplex named pipe and starts the probe with <c>--pipe &lt;name&gt;</c>. The probe
/// connects, writes one UTF-8 JSON object per line (events) and reads one JSON object per line (commands).
/// Closing the pipe from the client side makes the probe exit.
/// </remarks>
internal static class ProbeProtocol
{
    /// <summary>Protocol version announced by the ready event. Bump it on any incompatible change.</summary>
    public const int Version = 1;

    /// <summary>Command-line switch followed by the pipe name (without the <c>\\.\pipe\</c> prefix).</summary>
    public const string PipeSwitch = "--pipe";

    /// <summary>Exit code for a normal shutdown (quit command, pipe closed or window closed).</summary>
    public const int ExitOk = 0;

    /// <summary>Exit code for missing or malformed command-line arguments.</summary>
    public const int ExitUsage = 2;

    /// <summary>Exit code when the pipe could not be connected in time.</summary>
    public const int ExitPipe = 3;

    /// <summary>Exit code when the window or the Raw Input registration could not be created.</summary>
    public const int ExitWindow = 4;
}
