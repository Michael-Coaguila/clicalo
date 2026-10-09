using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Dimming;
using Clicalo.Presentation.Dock;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// Applies the opacity <see cref="SurfaceDimming"/> decides to every surface of the panel (GEN-009, docs/04 «Opacidad y
/// atenuado»), over 350 ms or at once with reduce motion; a surface that will dim later is evaluated again then, with
/// one <see cref="TimeProvider"/> timer. It reports whether a finger, the pen or the pointer is on any visible surface
/// and when the panel appears. «Release all» is always at 100 % (SEG-002). Dimming is only visual: no surface ignores a
/// touch because it is dimmed (EJE-017).
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The timer is disposed by Dispose, which the surface set calls when the panel closes."
)]
public sealed class SurfaceDimmer : IDisposable
{
    private readonly SurfaceDimming _dimming;
    private readonly ThemeService _theme;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly List<(
        NonActivatingWindow Window,
        DimSurface? Kind,
        IPointerPresence? Presence
    )> _surfaces = [];
    private ITimer? _timer;
    private bool _disposed;

    /// <summary>Creates the dimmer on the UI thread of the surfaces.</summary>
    /// <param name="dimming">What each kind of surface does.</param>
    /// <param name="theme">Reduce motion and contrast themes.</param>
    /// <param name="dispatcher">The dispatcher of the surfaces.</param>
    public SurfaceDimmer(
        SurfaceDimming dimming,
        ThemeService theme,
        System.Windows.Threading.Dispatcher dispatcher
    )
    {
        ArgumentNullException.ThrowIfNull(dimming);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dimming = dimming;
        _theme = theme;
        _dispatcher = dispatcher;
        _dimming.Changed += OnChanged;
        _theme.Changed += OnChanged;
    }

    /// <summary>Follows the panel: its dimming settings, its appearance and the finger on it.</summary>
    /// <param name="panel">The panel window.</param>
    public void Follow(PanelWindow panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        Add(panel, DimSurface.Panel, panel);
        panel.DimSettingsChanged += (_, _) => _dimming.ApplySettings(panel.DimSettings);
        panel.Shown += (_, _) => _dimming.SurfaceShown();
        _dimming.ApplySettings(panel.DimSettings);
        Apply();
    }

    /// <summary>Adds a surface that dims as <paramref name="kind"/> (or never, with <see langword="null"/>).</summary>
    /// <param name="window">The surface.</param>
    /// <param name="kind">Its kind for <see cref="DimPolicy"/>; null keeps it at 100 %.</param>
    /// <param name="presence">Whether a finger is on it, when it says so.</param>
    public void Add(NonActivatingWindow window, DimSurface? kind, IPointerPresence? presence)
    {
        ArgumentNullException.ThrowIfNull(window);
        _surfaces.Add((window, kind, presence));
        if (presence is not null)
        {
            presence.PresenceChanged += (_, _) => UpdatePresence();
        }
    }

    /// <summary>A surface appeared: it is awake and dims a while later (GEN-009).</summary>
    public void Shown()
    {
        if (!_disposed)
        {
            _dimming.SurfaceShown();
        }
    }

    /// <summary>Reports whether a finger, the pen or the pointer is on some surface.</summary>
    public void UpdatePresence()
    {
        if (_disposed)
        {
            return;
        }

        // A hidden surface forgets its finger and its pointer when it hides, so presence alone is enough.
        var inside = _surfaces.Exists(static s => s.Presence is { IsPointerInside: true });
        _dimming.PointerPresence(inside);
    }

    /// <summary>Applies the opacity of every surface now, and schedules the next evaluation.</summary>
    public void Apply()
    {
        if (_disposed)
        {
            return;
        }

        _timer?.Dispose();
        _timer = null;
        var reduceMotion = _theme.ReduceMotion;
        var highContrast = _theme.Effective is ThemeId.HighContrast or ThemeId.SystemHighContrast;
        DateTimeOffset? next = null;
        foreach (var (window, kind, _) in _surfaces)
        {
            if (kind is not { } surface)
            {
                window.FadeTo(1, TimeSpan.Zero);
                continue;
            }

            var decision = _dimming.Decide(surface, reduceMotion, highContrast);
            window.ApplyDim(decision);
            if (decision.NextEvaluationAt is { } at && (next is null || at < next))
            {
                next = at;
            }
        }

        if (next is { } due)
        {
            var clock = _dimming.Clock;
            var now = clock.GetUtcNow();
            _timer = clock.CreateTimer(
                static state => ((SurfaceDimmer)state!).Queue(),
                this,
                due > now ? due - now : TimeSpan.Zero,
                Timeout.InfiniteTimeSpan
            );
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer?.Dispose();
        _dimming.Changed -= OnChanged;
        _theme.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => Apply();

    private void Queue() => _ = _dispatcher.BeginInvoke(Apply);
}
