using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;

namespace Clicalo.Platform.Windows.Foreground;

/// <summary>
/// Adapter of <see cref="ITouchKeyboard"/> (blueprint §3.6 step 2): <c>IInputPaneInterop</c> for the occluded area
/// and <c>ITipInvocation::Toggle</c> to show the touch keyboard; Windows dictation (Win+H) through
/// <see cref="IInternalKeyEffects"/>, never through its own <c>SendInput</c>.
/// </summary>
/// <remarks>
/// <para>
/// Showing: on Windows 11, <c>InputPane.TryShow</c> for the window (through <c>IInputPaneInterop</c>); on Windows 10,
/// or when that is refused, <c>ITipInvocation::Toggle</c>, only while the keyboard is hidden (a toggle would hide it).
/// Hiding only touches a keyboard Clícalo showed.
/// </para>
/// <para>
/// The occluded area is read from <c>IFrameworkInputPane::Location</c> (screen, physical pixels). While Clícalo has
/// shown the keyboard, it is sampled every <c>Timings.Foreground.TouchKeyboardPoll</c> and
/// <see cref="OccludedAreaChanged"/> is raised on each change, so the panel can move above it (EC-BUS-01); the
/// sampling stops when the keyboard closes. The real keyboard is tested by hand (spike S4).
/// </para>
/// </remarks>
public sealed class TouchKeyboard : ITouchKeyboard, IDisposable
{
    private readonly Lock _gate = new();
    private readonly TimeProvider _clock;
    private ITimer? _sampler;
    private PhysicalRect _lastArea;
    private nint _shownFor;
    private bool _shownByToggle;
    private bool _seenOpen;

    /// <summary>Creates the adapter; <paramref name="keyEffects"/> sends Win+H.</summary>
    public TouchKeyboard(IInternalKeyEffects keyEffects)
        : this(keyEffects, TimeProvider.System) { }

    /// <summary>Creates the adapter; <paramref name="timeProvider"/> paces the sampling of the occluded area.</summary>
    public TouchKeyboard(IInternalKeyEffects keyEffects, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(keyEffects);
        ArgumentNullException.ThrowIfNull(timeProvider);
        KeyEffects = keyEffects;
        _clock = timeProvider;
    }

    /// <inheritdoc />
    public event EventHandler? OccludedAreaChanged;

    /// <inheritdoc />
    public PhysicalRect OccludedArea => TouchKeyboardInterop.Location();

    /// <summary>Sends the dictation chord.</summary>
    public IInternalKeyEffects KeyEffects { get; }

    /// <inheritdoc />
    public ValueTask<bool> ShowKeyboardAsync(
        WindowToken window,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (window.IsNone)
        {
            return ValueTask.FromResult(false);
        }

        var shown =
            TouchKeyboardInterop.HasInputPaneForWindows
            && TouchKeyboardInterop.TryShowOrHide(window.Handle, show: true);
        var byToggle = false;
        if (!shown && TouchKeyboardInterop.Location().IsEmpty)
        {
            shown = byToggle = TouchKeyboardInterop.Toggle();
        }

        if (shown)
        {
            StartSampling(window.Handle, byToggle);
        }

        return ValueTask.FromResult(shown);
    }

    /// <inheritdoc />
    public ValueTask HideKeyboardAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        nint window;
        bool byToggle;
        lock (_gate)
        {
            window = _shownFor;
            byToggle = _shownByToggle;
        }

        if (window == 0)
        {
            return ValueTask.CompletedTask;
        }

        var hidden = !byToggle && TouchKeyboardInterop.TryShowOrHide(window, show: false);
        if (!hidden && !TouchKeyboardInterop.Location().IsEmpty)
        {
            _ = TouchKeyboardInterop.Toggle();
        }

        StopSampling();
        Sample();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> StartDictationAsync(CancellationToken cancellationToken) =>
        KeyEffects.SendDictationChordAsync(cancellationToken);

    /// <summary>Stops sampling the occluded area.</summary>
    public void Dispose() => StopSampling();

    private void StartSampling(nint window, bool byToggle)
    {
        lock (_gate)
        {
            _shownFor = window;
            _shownByToggle = byToggle;
            _seenOpen = false;
            _sampler ??= _clock.CreateTimer(
                static state => ((TouchKeyboard)state!).Sample(),
                this,
                Timings.Foreground.TouchKeyboardPoll,
                Timings.Foreground.TouchKeyboardPoll
            );
        }
    }

    private void StopSampling()
    {
        ITimer? sampler;
        lock (_gate)
        {
            sampler = _sampler;
            _sampler = null;
            _shownFor = 0;
        }

        sampler?.Dispose();
    }

    private void Sample()
    {
        var area = TouchKeyboardInterop.Location();
        bool changed;
        bool closed;
        lock (_gate)
        {
            changed = area != _lastArea;
            _lastArea = area;
            _seenOpen |= !area.IsEmpty;
            closed = _seenOpen && area.IsEmpty;
        }

        if (closed)
        {
            StopSampling();
        }

        if (changed)
        {
            OccludedAreaChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
