using Clicalo.Application.Ports;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// <see cref="IInternalKeyEffects"/> that records what would be injected. By default the rights chord behaves as on
/// Windows: the system consumes it, <c>WM_HOTKEY</c> arrives and Clícalo gains the foreground right.
/// </summary>
internal sealed class FakeInternalKeyEffects(ForegroundWorld world) : IInternalKeyEffects
{
    /// <summary>What sending the rights chord does; by default it delivers <c>WM_HOTKEY</c> and the right.</summary>
    public Func<bool>? OnRightsChord { get; set; }

    /// <summary>Number of rights chords sent.</summary>
    public int RightsChords { get; private set; }

    /// <summary>Number of dictation chords sent.</summary>
    public int DictationChords { get; private set; }

    public ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RightsChords++;
        world.Log.Add("send rights chord");
        var sent = (OnRightsChord ?? DeliverRights)();
        return new ValueTask<bool>(sent);
    }

    public ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DictationChords++;
        world.Log.Add("send dictation chord");
        return new ValueTask<bool>(true);
    }

    /// <summary>The Windows behaviour: <c>WM_HOTKEY</c> reaches Clícalo, which now holds the foreground right.</summary>
    public bool DeliverRights()
    {
        world.Control.HasRights = true;
        world.Hotkey.Arrive();
        return true;
    }
}
