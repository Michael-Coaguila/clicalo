using System.Collections.Immutable;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.UI.Wpf.Windowing;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// Base of the laboratory surfaces: a real <see cref="NonActivatingWindow"/> (CLC0001 applies) whose tiles are real
/// <see cref="ShortcutTile"/>s. Touch goes through the real pointer layer (<see cref="PointerInputSource"/> and
/// <see cref="GestureRecognizer"/>, blueprint §8.3); while that layer is not integrated, the surface falls back to the
/// mouse input Windows promotes from touch and the control window shows the layer as pending. UI Automation commands
/// come from the tiles' peers. The surface also counts its own activation messages (the laboratory's instrument,
/// independent of <c>ActivationGuard</c>).
/// </summary>
internal abstract class LabSurface : NonActivatingWindow, IPointerFrameSink
{
    private const string PointerComponent = "PointerInputSource";
    private const string GestureComponent = "GestureRecognizer";
    private const string WindowingComponent = "NonActivatingWindow";

    private readonly List<(FrameworkElement Element, LabTile Tile)> _targets = [];
    private readonly List<GestureEvent> _gestures = [];
    private readonly Dictionary<uint, PointerKind> _contacts = [];
    private GestureRecognizer? _recognizer;
    private PointerInputSource? _pointer;
    private ITimer? _deadline;
    private DragState? _drag;
    private nint _handle;

    /// <summary>Creates the surface <paramref name="id"/> in <paramref name="group"/>.</summary>
    protected LabSurface(
        SurfaceId id,
        SurfaceRegistry registry,
        LabSurfaceContext context,
        SurfaceGroup group
    )
        : base(id, registry)
    {
        Context = context;
        Group = group;
        SurfaceName = id.ToString();
        Title = "Clícalo SpikeLab · " + SurfaceName;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        BorderThickness = new Thickness(1);
        SetResourceReference(BackgroundProperty, SystemColors.WindowBrushKey);
        SetResourceReference(BorderBrushProperty, SystemColors.ActiveBorderBrushKey);
        LayoutUpdated += (_, _) => RefreshTargets();
    }

    /// <summary>«Panel#0», «Dock#0»…</summary>
    public string SurfaceName { get; }

    /// <summary>The group of the surface under test.</summary>
    public SurfaceGroup Group { get; }

    /// <summary>True after a successful <see cref="TryShow"/> and until <see cref="TryHide"/>.</summary>
    public bool IsShown { get; private set; }

    /// <summary>True when touch goes through the real pointer layer; false on the promoted-mouse fallback.</summary>
    public bool UsesPointerLayer => _recognizer is not null && _pointer is not null;

    /// <summary>The window handle once created (without going through the contract), zero before.</summary>
    public nint Handle => _handle;

    /// <summary>The tiles of the surface.</summary>
    public IEnumerable<(ShortcutTile Control, LabTile Tile)> Tiles =>
        _targets
            .Where(target => target.Element is ShortcutTile)
            .Select(target => ((ShortcutTile)target.Element, target.Tile));

    /// <summary>What the surface shares with the others.</summary>
    protected LabSurfaceContext Context { get; }

    /// <summary>The element that drags the surface (the panel handle); null when it cannot be dragged.</summary>
    protected virtual FrameworkElement? DragHandle => null;

    /// <summary>The tile control of <paramref name="id"/>, if this surface has it.</summary>
    public ShortcutTile? Find(string id) =>
        Tiles
            .Where(tile => string.Equals(tile.Tile.Id, id, StringComparison.Ordinal))
            .Select(tile => tile.Control)
            .FirstOrDefault();

    /// <summary>Shows the surface without activating it; records the windowing as pending if it is not integrated.</summary>
    public bool TryShow()
    {
        try
        {
            ShowPassive();
            IsShown = true;
            Context.Board.Ready(
                WindowingComponent,
                "Superficies no activables (ShowPassive, HidePassive, MovePassive)."
            );
            RefreshTargets();
            return true;
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            Context.Board.Fail(WindowingComponent, ex);
            return false;
        }
    }

    /// <summary>Hides the surface without activating anything.</summary>
    public void TryHide()
    {
        if (!IsShown)
        {
            return;
        }

        try
        {
            HidePassive();
            IsShown = false;
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            Context.Board.Fail(WindowingComponent, ex);
        }
    }

    /// <summary>Moves the surface to <paramref name="bounds"/> (physical pixels) without activating it.</summary>
    public bool TryMove(PhysicalRect bounds)
    {
        try
        {
            MovePassive(bounds);
            return true;
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            Context.Board.Fail(WindowingComponent, ex);
            return false;
        }
    }

    /// <summary>The window bounds in physical screen pixels (<c>GetWindowRect</c>).</summary>
    public PhysicalRect Bounds()
    {
        if (_handle == 0 || !PInvoke.GetWindowRect((HWND)_handle, out var rect))
        {
            return PhysicalRect.Empty;
        }

        return PhysicalRect.FromEdges(rect.left, rect.top, rect.right, rect.bottom);
    }

    /// <inheritdoc />
    public void OnFrame(in PointerFrame frame)
    {
        foreach (var sample in frame.Samples)
        {
            TrackContact(sample);
        }

        if (_recognizer is not { } recognizer)
        {
            return;
        }

        _gestures.Clear();
        recognizer.Feed(frame, _gestures);
        Dispatch();
        foreach (var sample in frame.Samples)
        {
            if (sample.Phase is PointerPhase.Up or PointerPhase.Cancel)
            {
                _contacts.Remove(sample.PointerId);
            }
        }

        ScheduleDeadline();
    }

    /// <inheritdoc />
    public void OnHover(bool inside) { }

    /// <summary>Adds a tile and wires its UI Automation events and the promoted-mouse fallback.</summary>
    protected ShortcutTile AddTile(LabTile tile, double width, double height)
    {
        var control = TileFactory.Create(tile, width, height);
        TileFactory.WireAutomation(
            control,
            (pattern, expand) =>
                Context.Sink.OnTile(
                    new TileInput(tile, control, SurfaceName, Group, Context.Time.GetUtcNow())
                    {
                        IsCommand = true,
                        Pattern = pattern,
                        Expand = expand,
                        Channel = "uia",
                    }
                )
        );
        AddTarget(control, tile);
        return control;
    }

    /// <summary>Makes <paramref name="element"/> a touch target that runs <paramref name="tile"/>.</summary>
    protected void AddTarget(FrameworkElement element, LabTile tile)
    {
        ArgumentNullException.ThrowIfNull(element);
        _targets.Add((element, tile));

        // handledEventsToo: a text box handles its own mouse events, and its tap must still reach the session.
        element.AddHandler(
            MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(
                (_, args) =>
                {
                    if (UsesPointerLayer)
                    {
                        return;
                    }

                    args.Handled = true;
                    Context.Sink.OnTile(
                        new TileInput(
                            tile,
                            element as ShortcutTile,
                            SurfaceName,
                            Group,
                            Context.Time.GetUtcNow()
                        )
                        {
                            Channel = "mouse-promoted",
                        }
                    );
                }
            ),
            handledEventsToo: true
        );
    }

    /// <summary>Called once the handle has the non-activation contract: hooks the messages and the pointer layer.</summary>
    protected override void OnSurfaceInitialized()
    {
        base.OnSurfaceInitialized();
        _handle = new WindowInteropHelper(this).Handle;
        Context.Directory.Register(_handle, SurfaceName);
        HwndSource.FromHwnd(_handle)?.AddHook(CountActivation);
        AttachPointerLayer();
        if (DragHandle is { } handle)
        {
            handle.MouseLeftButtonDown += (_, args) => StartMouseDrag(args);
            handle.MouseMove += (_, _) => ContinueMouseDrag();
            handle.MouseLeftButtonUp += (_, _) => EndDrag(pointerId: null);
        }
    }

    /// <inheritdoc />
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Context.Log.Add(
            "dpi",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{SurfaceName} cambió de DPI: {oldDpi.PixelsPerInchX:0} → {newDpi.PixelsPerInchX:0}."
            )
        );
        try
        {
            _recognizer?.Configure(LabSurfaceContext.DefaultTouchSettings, newDpi.DpiScaleX);
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            Context.Board.Fail(GestureComponent, ex);
        }

        RefreshTargets();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _deadline?.Dispose();
        try
        {
            _pointer?.Detach();
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            Context.Board.Fail(PointerComponent, ex);
        }

        _pointer?.Dispose();
        Context.Directory.Unregister(_handle);
        base.OnClosed(e);
    }

    private void AttachPointerLayer()
    {
        GestureRecognizer? recognizer = null;
        var recognizerReady = Context.Board.Try(
            GestureComponent,
            "Gestos del dominio (toque, filtro de TAC-002).",
            () =>
            {
                recognizer = new GestureRecognizer(
                    LabSurfaceContext.DefaultTouchSettings,
                    VisualTreeHelper.GetDpi(this).DpiScaleX
                );
                recognizer.SetTargets([]);
            }
        );
        var pointer = new PointerInputSource(this, this, Context.Time);
        var pointerReady = Context.Board.Try(
            PointerComponent,
            "WM_POINTER propio en cada superficie (ADR-0006).",
            pointer.Attach
        );
        if (recognizerReady && pointerReady)
        {
            _recognizer = recognizer;
            _pointer = pointer;
            return;
        }

        // Without both halves the pointer messages would be consumed without gestures: go back to promoted mouse.
        if (pointerReady)
        {
            try
            {
                pointer.Detach();
            }
            catch (Exception ex) when (ComponentBoard.IsContained(ex))
            {
                Context.Board.Fail(PointerComponent, ex);
            }
        }

        pointer.Dispose();
    }

    private void RefreshTargets()
    {
        if (_recognizer is not { } recognizer || PresentationSource.FromVisual(this) is null)
        {
            return;
        }

        var targets = ImmutableArray.CreateBuilder<TouchTarget>(_targets.Count);
        for (var i = 0; i < _targets.Count; i++)
        {
            var bounds = PhysicalBounds(_targets[i].Element);
            if (!bounds.IsEmpty)
            {
                targets.Add(new TouchTarget(new TouchTargetId(i), bounds, TouchTargetKind.Tap));
            }
        }

        try
        {
            recognizer.SetTargets(targets.ToImmutable());
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            Context.Board.Fail(GestureComponent, ex);
        }
    }

    private static PhysicalRect PhysicalBounds(FrameworkElement element)
    {
        if (!element.IsVisible || PresentationSource.FromVisual(element) is null)
        {
            return PhysicalRect.Empty;
        }

        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(
            new Point(element.ActualWidth, element.ActualHeight)
        );
        return PhysicalRect.FromEdges(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(bottomRight.X),
            (int)Math.Round(bottomRight.Y)
        );
    }

    private void TrackContact(PointerSample sample)
    {
        switch (sample.Phase)
        {
            case PointerPhase.Down:
                _contacts[sample.PointerId] = sample.Kind;
                if (DragHandle is { } handle && PhysicalBounds(handle).Contains(sample.Position))
                {
                    _drag = new DragState(sample.PointerId, sample.Position, Bounds(), sample.Kind);
                }

                break;
            case PointerPhase.Move when _drag is { } drag && drag.PointerId == sample.PointerId:
                MoveBy(drag, sample.Position);
                break;
            case PointerPhase.Up or PointerPhase.Cancel when _drag?.PointerId == sample.PointerId:
                EndDrag(sample.PointerId);
                break;
        }
    }

    private void Dispatch()
    {
        foreach (var gesture in _gestures)
        {
            switch (gesture.Kind)
            {
                case GestureKind.Tap when gesture.Target is { } id && id.Value < _targets.Count:
                    var (element, tile) = _targets[id.Value];
                    var input = new TileInput(
                        tile,
                        element as ShortcutTile,
                        SurfaceName,
                        Group,
                        gesture.Timestamp
                    )
                    {
                        Pointer = _contacts.TryGetValue(gesture.PointerId, out var kind)
                            ? kind
                            : null,
                        Channel = "pointer",
                    };

                    // Leave the window procedure of WM_POINTERUP before running the action.
                    _ = Dispatcher.BeginInvoke(() => Context.Sink.OnTile(input));
                    break;
                case GestureKind.Ignored:
                    var reason = gesture.Ignored;
                    _ = Dispatcher.BeginInvoke(() =>
                        Context.Sink.OnIgnoredTouch(SurfaceName, reason)
                    );
                    break;
            }
        }
    }

    private void ScheduleDeadline()
    {
        _deadline?.Dispose();
        _deadline = null;
        if (_recognizer?.NextDeadline is not { } deadline)
        {
            return;
        }

        var due = deadline - Context.Time.GetUtcNow();
        _deadline = Context.Time.CreateTimer(
            _ => _ = Dispatcher.BeginInvoke(Tick),
            state: null,
            due < TimeSpan.Zero ? TimeSpan.Zero : due,
            Timeout.InfiniteTimeSpan
        );
    }

    private void Tick()
    {
        if (_recognizer is not { } recognizer)
        {
            return;
        }

        _gestures.Clear();
        recognizer.OnTick(Context.Time.GetUtcNow(), _gestures);
        Dispatch();
        ScheduleDeadline();
    }

    private nint CountActivation(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        var name = (uint)message switch
        {
            PInvoke.WM_ACTIVATE when (wParam & 0xFFFF) != PInvoke.WA_INACTIVE => "WM_ACTIVATE",
            PInvoke.WM_NCACTIVATE when wParam != 0 => "WM_NCACTIVATE(TRUE)",
            PInvoke.WM_ACTIVATEAPP when wParam != 0 => "WM_ACTIVATEAPP(TRUE)",
            _ => null,
        };
        if (name is not null)
        {
            Context.Sink.OnActivationMessage(SurfaceName, name);
        }

        return 0;
    }

    private void StartMouseDrag(MouseButtonEventArgs args)
    {
        if (UsesPointerLayer || !PInvoke.GetCursorPos(out var cursor))
        {
            return;
        }

        args.Handled = true;
        _drag = new DragState(0, new PhysicalPoint(cursor.X, cursor.Y), Bounds(), Pointer: null);
    }

    private void ContinueMouseDrag()
    {
        if (
            UsesPointerLayer
            || _drag is not { } drag
            || Mouse.LeftButton != MouseButtonState.Pressed
            || !PInvoke.GetCursorPos(out var cursor)
        )
        {
            return;
        }

        MoveBy(drag, new PhysicalPoint(cursor.X, cursor.Y));
    }

    private void MoveBy(DragState drag, PhysicalPoint position)
    {
        if (drag.StartBounds.IsEmpty)
        {
            return;
        }

        TryMove(
            drag.StartBounds with
            {
                Left = drag.StartBounds.Left + position.X - drag.Start.X,
                Top = drag.StartBounds.Top + position.Y - drag.Start.Y,
            }
        );
    }

    private void EndDrag(uint? pointerId)
    {
        if (_drag is not { } drag || (pointerId is { } id && id != drag.PointerId))
        {
            return;
        }

        _drag = null;
        Context.Sink.OnHandleDrag(SurfaceName, Group, drag.Pointer);
    }

    private sealed record DragState(
        uint PointerId,
        PhysicalPoint Start,
        PhysicalRect StartBounds,
        PointerKind? Pointer
    );
}
