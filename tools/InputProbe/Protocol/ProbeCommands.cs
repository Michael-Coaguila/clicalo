namespace Clicalo.Tools.InputProbe.Protocol;

/// <summary>Commands the client writes to the probe, one JSON object per line: <c>{"cmd":"ping","id":7}</c>.</summary>
internal static class ProbeCommands
{
    /// <summary>Name of the command field.</summary>
    public const string CommandField = "cmd";

    /// <summary>Echoes <see cref="ProbeFields.Id"/> back in a <see cref="ProbeEventKinds.Pong"/> event.</summary>
    public const string Ping = "ping";

    /// <summary>
    /// Calls <c>SetForegroundWindow</c> on <see cref="ProbeFields.Window"/> (the probe window when absent) and reports
    /// the outcome in a <see cref="ProbeEventKinds.Foreground"/> event. Windows only honours it when the probe holds
    /// the right to change the foreground, for example after the client called <c>AllowSetForegroundWindow</c>.
    /// </summary>
    public const string Foreground = "foreground";

    /// <summary>Destroys the window and exits.</summary>
    public const string Quit = "quit";
}
