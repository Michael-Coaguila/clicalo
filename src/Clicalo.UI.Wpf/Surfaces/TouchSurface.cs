using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;
using GestureTarget = Clicalo.Domain.Touch.TouchTarget;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The common part of the small surfaces of the panel (blueprint §8.1): the bubble, the handle and the bar of the Tab
/// view, the windows beside it and the floating «Release all». Each is a <see cref="NonActivatingWindow"/> (it never
/// takes the foreground, REG-01) whose finger, pen and mouse go through the product's pointer layer
/// (<see cref="PointerInputSource"/> feeding a <see cref="GestureHost"/>, ADR-0006): every target answers on at least
/// 44 × 44 (REG-02), a Mantener holds while its contact lasts (EJE-004), and a zone can drag the surface
/// (<see cref="DragTracker"/>, PAN-004): a contact that dragged never counts as a tap, and neither does one that
/// scrolled a zone of the surface (TAC-004, <see cref="TrackContact"/>). With <see cref="Modes"/>, test mode and the
/// menu of a shortcut take its gestures first (PES-010, PES-014, CUA-014). Hiding a surface ends its holds (REG-03).
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "A WPF window's lifetime ends with Close: OnClosed disposes the pointer layer and the gestures."
)]
public abstract class TouchSurface : NonActivatingWindow, IPointerFrameSink, IPointerPresence
{
    private readonly TimeProvider _time;
    private readonly ThemeService _theme;
    private readonly Action<uint, ContactSummary, HoldEndReason> _holdEnded;
    private readonly ContactTracker _contacts = new();
    private readonly Dictionary<int, SurfaceTarget> _targets = [];
    private readonly Dictionary<object, int> _ids = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<uint> _dragged = [];
    private readonly DragTracker _drag;
    private TouchSettings _touch;
    private FrameworkElement? _dragZone;
    private GestureHost? _gestures;
    private PointerInputSource? _pointer;
    private int _nextId;
    private bool _hovered;
    private bool _touching;
    private bool _closed;

    /// <summary>Creates the surface on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="id">Its identity in the registry.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer frames and the gesture deadlines.</param>
    /// <param name="theme">The theme service of the UI thread.</param>
    /// <param name="touch">The touch filter (TAC-002).</param>
    /// <param name="holdEnded">Where the end of a hold goes, by contact (INV-9).</param>
    /// <param name="look">Its shape and shadow.</param>
    protected TouchSurface(
        SurfaceId id,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch,
        Action<uint, ContactSummary, HoldEndReason> holdEnded,
        SurfaceLook look
    )
        : base(id, registry)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(holdEnded);
        _time = time;
        _theme = theme;
        _touch = touch;
        _holdEnded = holdEnded;
        _drag = new DragTracker(touch, 1);
        Look = look;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        theme.Attach(this);
        LayoutUpdated += (_, _) => RefreshTargets();
        LocationChanged += (_, _) => RefreshTargets();
    }

    /// <summary>A finger, the pen or the pointer came onto the surface or left it (<see cref="IsPointerInside"/>): dimming (GEN-009).</summary>
    public event EventHandler? PresenceChanged;

    /// <summary>An accepted tap ran a shortcut of the surface (PES-010, PES-012).</summary>
    public event EventHandler<DockTileEventArgs>? TileTapped;

    /// <summary>A Mantener of the surface was released (PES-012).</summary>
    public event EventHandler? HoldReleased;

    /// <summary>Whether a finger, the pen or the pointer is on the surface.</summary>
    public bool IsPointerInside => _hovered || _touching;

    /// <summary>Whether a finger or the pen rests on the surface.</summary>
    public bool IsTouching => _touching;

    /// <summary>The theme service of the UI thread.</summary>
    protected ThemeService Theme => _theme;

    /// <summary>
    /// Test mode and the menu of the shortcuts of this surface (PES-010, PES-014): asked before a shortcut runs.
    /// <see langword="null"/>, the default, for a surface without them.
    /// </summary>
    protected DockTileModes? Modes { get; set; }

    /// <summary>
    /// The width the surface keeps, in logical pixels, while its height follows its content: text wraps inside it
    /// (PES-010, PES-011, PES-014, PES-015). <see cref="double.NaN"/>, the default: both follow the content.
    /// </summary>
    protected double FixedWidth { get; set; } = double.NaN;

    /// <summary>The distance past which a contact drags or scrolls instead of tapping, in physical pixels (PAN-004).</summary>
    protected double DragThresholdPx => _drag.ThresholdPx;

    /// <summary>The surface on screen, in physical pixels; empty before it has a handle.</summary>
    public PhysicalRect ScreenBounds => PhysicalBounds(this, inflate: false);

    /// <summary>Physical pixels per logical pixel of the surface's monitor.</summary>
    public double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    /// <summary>Takes new touch filter values (TAC-002).</summary>
    /// <param name="touch">The values.</param>
    public void ApplyTouch(TouchSettings touch)
    {
        _touch = touch;
        _gestures?.Recognizer.Configure(touch, DpiScale);
        _drag.Configure(touch, DpiScale);
    }

    /// <summary>Shows the surface passively at <paramref name="bounds"/> (REG-01).</summary>
    /// <param name="bounds">Where, in physical pixels.</param>
    public void PresentAt(PhysicalRect bounds)
    {
        if (_closed || bounds.IsEmpty)
        {
            return;
        }

        if (ScreenBounds != bounds)
        {
            MovePassive(bounds);
        }

        ShowPassive();
        RefreshTargets();
    }

    /// <summary>
    /// Hides the surface. It receives no pointer-up once hidden, so its holds end now (REG-03) and its contacts are
    /// forgotten.
    /// </summary>
    public void Conceal()
    {
        if (_closed)
        {
            return;
        }

        _gestures?.Reset();
        _contacts.Clear();
        _drag.Reset();
        _dragZone = null;
        _dragged.Clear();
        SetTouching(false);
        SetHovered(false);
        HidePassive();
    }

    /// <summary>The size the surface asks for at its content, in physical pixels at <paramref name="scale"/>.</summary>
    /// <param name="scale">Physical pixels per logical pixel of the monitor it goes to.</param>
    public (int Width, int Height) MeasurePhysical(double scale)
    {
        if (Content is not UIElement content)
        {
            return (1, 1);
        }

        var border = BorderThickness;
        var fixedWidth = FixedWidth is > 0 and < double.PositiveInfinity;
        content.Measure(
            new Size(
                fixedWidth
                    ? Math.Max(0, FixedWidth - border.Left - border.Right)
                    : double.PositiveInfinity,
                double.PositiveInfinity
            )
        );
        var width = fixedWidth
            ? FixedWidth
            : content.DesiredSize.Width + border.Left + border.Right;
        var height = content.DesiredSize.Height + border.Top + border.Bottom;
        return (
            Math.Max(1, (int)Math.Ceiling(width * scale)),
            Math.Max(1, (int)Math.Ceiling(height * scale))
        );
    }

    /// <inheritdoc />
    public void OnFrame(in PointerFrame frame)
    {
        foreach (var sample in frame.Samples)
        {
            _contacts.Observe(sample);
            TrackDrag(sample);
            if (TrackContact(sample))
            {
                // TAC-004: a contact that scrolled or slid something activates nothing.
                _ = _dragged.Add(sample.PointerId);
            }
        }

        SetTouching(_contacts.Count > 0);
        try
        {
            _gestures?.OnFrame(frame);
        }
        finally
        {
            foreach (var sample in frame.Samples)
            {
                _contacts.Forget(sample);
                if (sample.Phase is PointerPhase.Up or PointerPhase.Cancel)
                {
                    _dragged.Remove(sample.PointerId);
                }
            }

            SetTouching(_contacts.Count > 0);
        }
    }

    /// <inheritdoc />
    public void OnHover(bool inside) => SetHovered(inside);

    /// <summary>Recomputes the touch targets from what is on screen.</summary>
    public void RefreshTargets()
    {
        if (_closed || _gestures is not { } gestures || PresentationSource.FromVisual(this) is null)
        {
            return;
        }

        _targets.Clear();
        var targets = ImmutableArray.CreateBuilder<GestureTarget>();
        foreach (var target in CollectTargets())
        {
            var bounds = PhysicalBounds(target.Element, inflate: true);
            if (bounds.IsEmpty)
            {
                continue;
            }

            var id = IdOf((object?)target.Tile ?? target.Element);
            if (_targets.ContainsKey(id))
            {
                continue;
            }

            targets.Add(new GestureTarget(new TouchTargetId(id), bounds, target.Kind));
            _targets[id] = target;
        }

        gestures.Recognizer.SetTargets(targets.ToImmutable());
    }

    /// <summary>Every target on screen, in order.</summary>
    protected abstract IEnumerable<SurfaceTarget> CollectTargets();

    /// <summary>
    /// Follows a contact for a zone of the surface that scrolls or slides under the finger (TAC-004); nothing by
    /// default.
    /// </summary>
    /// <param name="sample">The pointer sample, in physical screen pixels.</param>
    /// <returns>Whether the contact scrolled or slid: it is not a tap.</returns>
    protected virtual bool TrackContact(in PointerSample sample) => false;

    /// <summary>The zones whose drag moves the surface; none by default.</summary>
    protected virtual IEnumerable<FrameworkElement> DragZones => [];

    /// <summary>Whether a contact that went down at <paramref name="position"/> in <paramref name="zone"/> may drag it.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="position">Where the contact went down, in physical pixels.</param>
    protected virtual bool DragStartsAt(FrameworkElement zone, PhysicalPoint position) => true;

    /// <summary>A drag of <paramref name="zone"/> passed the threshold (PAN-004).</summary>
    /// <param name="zone">The zone.</param>
    protected virtual void OnDragStarted(FrameworkElement zone) { }

    /// <summary>The drag moved: <paramref name="offset"/> from where the contact went down, in physical pixels.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="offset">The offset.</param>
    protected virtual void OnDragged(FrameworkElement zone, PhysicalOffset offset) { }

    /// <summary>The drag ended: the contact lifted or was cancelled.</summary>
    /// <param name="zone">The zone.</param>
    protected virtual void OnDragEnded(FrameworkElement zone) { }

    /// <inheritdoc />
    protected override void OnSurfaceInitialized()
    {
        base.OnSurfaceInitialized();
        var scale = DpiScale;
        _drag.Configure(_touch, scale);
        _gestures = new GestureHost(
            new GestureRecognizer(_touch, scale),
            Dispatcher,
            _time,
            OnGesture
        );
        _pointer = new PointerInputSource(this, this, _time);
        _pointer.Attach();
        RefreshTargets();
    }

    /// <inheritdoc />
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _gestures?.Recognizer.Configure(_touch, newDpi.DpiScaleX);
        _drag.Configure(_touch, newDpi.DpiScaleX);
        RefreshTargets();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _pointer?.Detach();
        _pointer?.Dispose();

        // On the UI thread: a hold that is still active ends with HoldEndReason.Reset and the engine releases it.
        _gestures?.Dispose();
        _theme.Detach(this);
        base.OnClosed(e);
    }

    /// <summary>
    /// The bounds of <paramref name="element"/> on screen, in physical pixels; with <paramref name="inflate"/>, grown
    /// around its center to at least 44 × 44 logical pixels (REG-02): where targets overlap, the nearest center wins.
    /// </summary>
    protected static PhysicalRect PhysicalBounds(FrameworkElement element, bool inflate) =>
        TouchBounds.Of(element, inflate);

    private int IdOf(object key)
    {
        if (!_ids.TryGetValue(key, out var id))
        {
            id = _nextId++;
            _ids[key] = id;
        }

        return id;
    }

    private void TrackDrag(in PointerSample sample)
    {
        switch (sample.Phase)
        {
            case PointerPhase.Down when !_drag.IsTracking:
                foreach (var zone in DragZones)
                {
                    if (
                        PhysicalBounds(zone, inflate: true).Contains(sample.Position)
                        && DragStartsAt(zone, sample.Position)
                    )
                    {
                        _dragZone = zone;
                        _ = _drag.Down(sample.PointerId, sample.Position);
                        break;
                    }
                }

                break;

            case PointerPhase.Move when _dragZone is { } zone:
                var wasDragging = _drag.IsDragging;
                if (_drag.Move(sample.PointerId, sample.Position) is { } offset)
                {
                    if (!wasDragging)
                    {
                        OnDragStarted(zone);
                    }

                    OnDragged(zone, offset);
                }

                break;

            case PointerPhase.Up
            or PointerPhase.Cancel when _dragZone is { } ended:
                if (
                    sample.Phase == PointerPhase.Up
                    && _drag.Move(sample.PointerId, sample.Position) is { } last
                )
                {
                    OnDragged(ended, last);
                }

                var end = _drag.Up(sample.PointerId);
                if (end == DragEnd.Dragged)
                {
                    // PAN-004: a gesture that became a drag is not a tap.
                    _ = _dragged.Add(sample.PointerId);
                    _dragZone = null;
                    OnDragEnded(ended);
                }
                else if (end == DragEnd.Tapped)
                {
                    _dragZone = null;
                }

                break;
        }
    }

    private void SetTouching(bool touching)
    {
        if (touching == _touching)
        {
            return;
        }

        var before = IsPointerInside;
        _touching = touching;
        if (before != IsPointerInside)
        {
            PresenceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetHovered(bool hovered)
    {
        if (hovered == _hovered)
        {
            return;
        }

        var before = IsPointerInside;
        _hovered = hovered;
        if (before != IsPointerInside)
        {
            PresenceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnGesture(GestureEvent gesture)
    {
        switch (gesture.Kind)
        {
            case GestureKind.Tap
                when !_dragged.Contains(gesture.PointerId) && TargetOf(gesture) is { } target:
                if (target.Tile is { } tile)
                {
                    // PES-014, CUA-014: test mode and an open menu take the tap before the engine.
                    if (Modes?.Tapped(tile) == true)
                    {
                        break;
                    }

                    tile.Tapped(
                        gesture.PointerId,
                        _contacts.DeviceOf(gesture.PointerId),
                        _contacts.Summarize(gesture.PointerId, gesture.Timestamp, DpiScale),
                        gesture.Timestamp
                    );
                    TileTapped?.Invoke(this, new DockTileEventArgs(tile));
                }
                else if (Modes?.TappedElsewhere() != true)
                {
                    // CUA-014: with the menu of a shortcut open, a tap on another button only closes it.
                    target.Tap?.Invoke();
                }

                break;

            case GestureKind.HoldStart when TargetOf(gesture)?.Tile is { } held:
                if (Modes?.HoldStarted(held) == true)
                {
                    break;
                }

                held.HoldStarted(
                    gesture.PointerId,
                    _contacts.DeviceOf(gesture.PointerId),
                    gesture.Timestamp
                );
                break;

            case GestureKind.LongPress
                when Modes is { } modes
                    && !_dragged.Contains(gesture.PointerId)
                    && TargetOf(gesture)?.Tile is { } pressed:
                // CUA-014, PES-010: 600 ms without moving opens the menu of the shortcut instead of running it.
                _ = modes.LongPressed(pressed, _contacts.DeviceOf(gesture.PointerId));
                break;

            case GestureKind.Ignored
                when Modes is { } modes && TargetOf(gesture)?.Tile is { } ignored:
                // TAC-008: test mode marks an ignored touch with its reason.
                modes.Ignored(ignored, gesture.Ignored);
                break;

            case GestureKind.HoldEnd:
                // The contact that started the hold owns it (INV-9): its end reaches the engine by contact.
                _holdEnded(
                    gesture.PointerId,
                    _contacts.Summarize(gesture.PointerId, gesture.Timestamp, DpiScale),
                    gesture.HoldEnd
                );
                HoldReleased?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private SurfaceTarget? TargetOf(GestureEvent gesture) =>
        gesture.Target is { } id ? _targets.GetValueOrDefault(id.Value) : null;
}
