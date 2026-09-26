using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>
/// The simulated input receiver of SEG-007 («un receptor de entrada simulado»): the keys and buttons the system sees
/// down (<c>D</c> of blueprint §7.5), and every anomaly an injected batch could cause.
/// </summary>
internal sealed class Receiver
{
    private readonly HashSet<InjectedKey> _keys = [];
    private readonly List<string> _anomalies = [];
    private readonly HashSet<InjectedKey> _tolerated = [];
    private MouseButtons _toleratedButtons;

    public IReadOnlySet<InjectedKey> Keys => _keys;

    public MouseButtons Buttons { get; private set; }

    public bool IsEmpty => _keys.Count == 0 && Buttons == MouseButtons.None;

    /// <summary>A key pressed twice, a release of a key that is not down, a button released twice.</summary>
    public IReadOnlyList<string> Anomalies => _anomalies;

    public List<InjectedEvent> Log { get; } = [];

    /// <summary>
    /// Keys of a batch that <c>SendInput</c> took only in part: the engine releases them blindly, and a release of a key
    /// that is up is harmless, so it is no anomaly.
    /// </summary>
    public void Tolerate(IEnumerable<InjectedEvent> presses)
    {
        foreach (var press in presses)
        {
            if (press.Kind is InjectedEventKind.KeyDown or InjectedEventKind.KeyUp)
            {
                _tolerated.Add(press.Key);
            }
            else if (press.Kind is InjectedEventKind.MouseDown or InjectedEventKind.MouseUp)
            {
                _toleratedButtons |= press.Button;
            }
        }
    }

    public void Apply(IEnumerable<InjectedEvent> events)
    {
        foreach (var e in events)
        {
            Log.Add(e);
            switch (e.Kind)
            {
                case InjectedEventKind.KeyDown:
                    if (!_keys.Add(e.Key) && !_tolerated.Contains(e.Key))
                    {
                        _anomalies.Add("pressed twice: " + e.Key);
                    }

                    break;
                case InjectedEventKind.KeyUp:
                    if (!_keys.Remove(e.Key) && !_tolerated.Contains(e.Key))
                    {
                        _anomalies.Add("released while up: " + e.Key);
                    }

                    break;
                case InjectedEventKind.MouseDown:
                    if ((Buttons & e.Button) != MouseButtons.None)
                    {
                        _anomalies.Add("button pressed twice: " + e.Button);
                    }

                    Buttons |= e.Button;
                    break;
                case InjectedEventKind.MouseUp:
                    if (
                        (Buttons & e.Button) == MouseButtons.None
                        && (_toleratedButtons & e.Button) == MouseButtons.None
                    )
                    {
                        _anomalies.Add("button released while up: " + e.Button);
                    }

                    Buttons &= ~e.Button;
                    break;
            }
        }
    }
}
