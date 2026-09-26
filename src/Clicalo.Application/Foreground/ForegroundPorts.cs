using Clicalo.Application.Ports;

namespace Clicalo.Application.Foreground;

/// <summary>
/// The ports <see cref="ForegroundOrchestrator"/> works through (blueprint §3.6, deviation D-13). The composition root
/// passes the real adapters (Platform.Windows and UI.Wpf); tests pass fakes.
/// </summary>
public sealed record ForegroundPorts
{
    /// <summary>The only way to change the foreground (<c>SetForegroundWindow</c>).</summary>
    public required IForegroundControl Control { get; init; }

    /// <summary>The verified external foreground and its changes.</summary>
    public required IForegroundMonitor Monitor { get; init; }

    /// <summary>Makes a surface activatable while a text or keyboard lease targets it.</summary>
    public required ISurfaceActivationStyle SurfaceStyle { get; init; }

    /// <summary>Maps a lease target window to its surface.</summary>
    public required ISurfaceLookup Surfaces { get; init; }

    /// <summary>Step 2 of the ladder: the reserved chord registered with <c>RegisterHotKey</c>.</summary>
    public required IInternalRightsHotkey RightsHotkey { get; init; }

    /// <summary>Sends the reserved chord (step 2) as an internal effect.</summary>
    public required IInternalKeyEffects KeyEffects { get; init; }
}
