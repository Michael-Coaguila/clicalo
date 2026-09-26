using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Windows.Win32;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.Input.Pointer;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.UI.Wpf.Pointer;

/// <summary>
/// Translates the <c>WM_POINTER*</c> messages of ONE window into <see cref="PointerFrame"/>s (blueprint §8.3,
/// ADR-0006): <c>GetPointerFrameTouchInfo</c> for fingers (with <c>rcContact</c>), <c>GetPointerPenInfo</c> for pens
/// and <c>GetPointerInfo</c> for the mouse; positions in physical screen pixels; timestamps on the
/// <see cref="TimeProvider"/> timeline; the origin from <c>GetCurrentInputMessageSource</c>. It hooks the window's
/// <c>HwndSource</c> and lives on its UI thread.
/// </summary>
/// <remarks>
/// <para>
/// It never captures the mouse (<c>SetCapture</c> belongs to the foreground window, ADR-0005), never activates the
/// window and marks the pointer messages of a contact as handled, so WPF does not promote them to mouse input on a
/// surface; hover updates (no contact) are left to WPF. The mouse only arrives here when the process routes it
/// through pointer messages (<see cref="PointerSetup.EnableMouseInPointer"/>); a mouse contact is the primary
/// button. The vertical panning of a Workspace window (Control Center, needed because <c>PanningMode</c> depends on
/// the WPF touch stack that is switched off) is not part of the surfaces of M1: it arrives with the Control Center.
/// </para>
/// <para>
/// A frame is dated when Windows recorded it (<c>POINTER_INFO.PerformanceCount</c>) when <see cref="Clock"/> is
/// <see cref="TimeProvider.System"/>, whose timestamps use the same performance counter, and at most
/// <c>Timings.Touch.PointerStampMaxAge</c> back; otherwise, and with any other clock, it is dated when it is read.
/// Frame times never go backwards.
/// </para>
/// <para>
/// The hot path does not allocate: every buffer is reused, including the sample array of the delivered frame, which
/// is only valid during <see cref="IPointerFrameSink.OnFrame"/> (see there). While any contact is down the UI thread
/// runs at <see cref="ThreadPriority.AboveNormal"/> (blueprint §3.2).
/// </para>
/// </remarks>
public sealed class PointerInputSource : IDisposable
{
    private const int InitialCapacity = 10;

    private readonly HwndSourceHook _hook;
    private HwndSource? _source;
    private nint _window;
    private POINTER_TOUCH_INFO[] _touches = new POINTER_TOUCH_INFO[InitialCapacity];
    private PointerSample[] _scratch = new PointerSample[InitialCapacity];
    private PointerSample[]?[] _frames = new PointerSample[]?[InitialCapacity + 1];
    private TrackedPointer[] _tracked = new TrackedPointer[InitialCapacity];
    private int _trackedCount;
    private uint[] _inside = new uint[InitialCapacity];
    private int _insideCount;
    private bool _hasTouchFrame;
    private uint _touchFrameId;
    private nint _touchDevice;
    private DateTimeOffset _lastStamp = DateTimeOffset.MinValue;
    private bool _boosted;

    /// <summary>Creates a source for <paramref name="window"/> that delivers to <paramref name="sink"/>.</summary>
    /// <param name="window">The window whose pointer messages are translated.</param>
    /// <param name="sink">Receives the frames synchronously.</param>
    /// <param name="timeProvider">Stamps the frames.</param>
    public PointerInputSource(Window window, IPointerFrameSink sink, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Window = window;
        Sink = sink;
        Clock = timeProvider;
        _hook = WndProc;
    }

    /// <summary>The window whose pointer messages are translated.</summary>
    public Window Window { get; }

    /// <summary>Receives the frames.</summary>
    public IPointerFrameSink Sink { get; }

    /// <summary>Stamps the frames.</summary>
    public TimeProvider Clock { get; }

    /// <summary>True between <see cref="Attach"/> and <see cref="Detach"/>.</summary>
    public bool IsAttached => _source is not null;

    /// <summary>Contacts currently down on the window, as reported to <see cref="Sink"/>.</summary>
    public int ActiveContacts => _trackedCount;

    /// <summary>
    /// Hooks the window's <c>HwndSource</c> (the handle must exist: call it from
    /// <c>NonActivatingWindow.OnSurfaceInitialized</c> or after <c>SourceInitialized</c>). Does nothing if already
    /// attached. Must run on the window's thread.
    /// </summary>
    /// <exception cref="InvalidOperationException">The window has no handle yet.</exception>
    public void Attach()
    {
        Window.VerifyAccess();
        if (_source is not null)
        {
            return;
        }

        var handle = new WindowInteropHelper(Window).Handle;
        if (handle == 0)
        {
            throw new InvalidOperationException(
                "The window has no handle yet: attach from OnSurfaceInitialized or after SourceInitialized."
            );
        }

        _source =
            HwndSource.FromHwnd(handle)
            ?? throw new InvalidOperationException("The window is not hosted in an HwndSource.");
        _window = handle;
        _source.AddHook(_hook);
    }

    /// <summary>
    /// Removes the hook. Every contact still down is delivered as <see cref="PointerPhase.Cancel"/> first, so no
    /// hold stays active, and a pointer still inside reports <see cref="IPointerFrameSink.OnHover"/>(false). Must run
    /// on the window's thread.
    /// </summary>
    public void Detach()
    {
        Window.VerifyAccess();
        if (_source is null)
        {
            return;
        }

        var source = _source;
        Forget();
        source.RemoveHook(_hook);
    }

    /// <summary>Detaches if attached.</summary>
    public void Dispose()
    {
        if (_source is not null)
        {
            Detach();
        }
    }

    private static uint PointerIdOf(nint wParam) => (uint)(wParam & 0xFFFF);

    private static PhysicalRect Dot(PhysicalPoint position) => new(position.X, position.Y, 1, 1);

    private static PointerInputOrigin CurrentOrigin()
    {
        if (!PInvoke.GetCurrentInputMessageSource(out var source))
        {
            return PointerInputOrigin.Unknown;
        }

        return source.originId switch
        {
            INPUT_MESSAGE_ORIGIN_ID.IMO_HARDWARE => PointerInputOrigin.Hardware,
            INPUT_MESSAGE_ORIGIN_ID.IMO_INJECTED => PointerInputOrigin.Injected,
            INPUT_MESSAGE_ORIGIN_ID.IMO_SYSTEM => PointerInputOrigin.System,
            _ => PointerInputOrigin.Unknown,
        };
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch ((uint)msg)
        {
            case PInvoke.WM_POINTERDOWN:
            case PInvoke.WM_POINTERUPDATE:
            case PInvoke.WM_POINTERUP:
                if (OnPointer(PointerIdOf(wParam)))
                {
                    handled = true;
                }

                break;
            case PInvoke.WM_POINTERCAPTURECHANGED:
                // A contact whose messages were consumed stays consumed: DefWindowProc would turn the lost capture into
                // gestures or promoted mouse input on the surface.
                if (EndLostContact(PointerIdOf(wParam)))
                {
                    handled = true;
                }

                break;
            case PInvoke.WM_POINTERENTER:
                OnEnter(PointerIdOf(wParam));
                break;
            case PInvoke.WM_POINTERLEAVE:
                // Windows sends the leave of a contact only once the contact is over: if it is still tracked, its up
                // was lost (no hold may stay down, REG-03).
                _ = EndLostContact(PointerIdOf(wParam));
                OnLeave(PointerIdOf(wParam));
                break;
            case PInvoke.WM_NCDESTROY:
                // The HwndSource and its hooks go away with the window: only the contacts need ending.
                Forget();
                break;
        }

        return 0;
    }

    /// <summary>Translates one pointer message; true when it belongs to a contact (so WPF must not see it).</summary>
    private bool OnPointer(uint pointerId)
    {
        if (!PInvoke.GetPointerType(pointerId, out var type))
        {
            return false;
        }

        return type switch
        {
            POINTER_INPUT_TYPE.PT_TOUCH => OnTouch(pointerId),
            POINTER_INPUT_TYPE.PT_PEN => OnPen(pointerId),
            POINTER_INPUT_TYPE.PT_MOUSE or POINTER_INPUT_TYPE.PT_TOUCHPAD => OnMouse(pointerId),
            _ => false,
        };
    }

    private bool OnTouch(uint pointerId)
    {
        var count = ReadTouchFrame(pointerId);
        if (count == 0)
        {
            return false;
        }

        var belongs = false;
        for (var i = 0; i < count; i++)
        {
            ref readonly var info = ref _touches[i].pointerInfo;
            if (info.pointerId == pointerId)
            {
                belongs = IsTracked(pointerId) || IsContact(info.pointerFlags);
            }
        }

        ref readonly var first = ref _touches[0].pointerInfo;
        if (
            _hasTouchFrame
            && first.frameId == _touchFrameId
            && (nint)first.sourceDevice == _touchDevice
        )
        {
            // Another message of a frame already delivered whole.
            return belongs;
        }

        _hasTouchFrame = true;
        _touchFrameId = first.frameId;
        _touchDevice = (nint)first.sourceDevice;
        var origin = CurrentOrigin();
        var stamp = Stamp(first.PerformanceCount);
        var samples = 0;
        for (var i = 0; i < count; i++)
        {
            ref readonly var touch = ref _touches[i];
            ref readonly var info = ref touch.pointerInfo;
            if ((nint)info.hwndTarget != _window && info.pointerId != pointerId)
            {
                continue;
            }

            var position = new PhysicalPoint(info.ptPixelLocation.X, info.ptPixelLocation.Y);
            var contact =
                (touch.touchMask & PInvoke.TOUCH_MASK_CONTACTAREA) != 0
                    ? PhysicalRect.FromEdges(
                        touch.rcContact.left,
                        touch.rcContact.top,
                        touch.rcContact.right,
                        touch.rcContact.bottom
                    )
                    : Dot(position);
            var flags = info.pointerFlags;
            Collect(
                ref samples,
                info.pointerId,
                PointerKind.Finger,
                IsDown(flags, POINTER_FLAGS.POINTER_FLAG_INCONTACT),
                HasAny(flags, POINTER_FLAGS.POINTER_FLAG_CANCELED),
                position,
                contact,
                stamp,
                origin
            );
        }

        Deliver(first.frameId, stamp, samples);
        return belongs;
    }

    private bool OnPen(uint pointerId)
    {
        if (!PInvoke.GetPointerPenInfo(pointerId, out var pen))
        {
            return false;
        }

        ref readonly var info = ref pen.pointerInfo;
        return OnSingle(info, PointerKind.Pen, POINTER_FLAGS.POINTER_FLAG_INCONTACT);
    }

    private bool OnMouse(uint pointerId)
    {
        if (!PInvoke.GetPointerInfo(pointerId, out var info))
        {
            return false;
        }

        // A mouse contact is the primary button: the right button stays a WPF mouse message.
        return OnSingle(info, PointerKind.Mouse, POINTER_FLAGS.POINTER_FLAG_FIRSTBUTTON);
    }

    private bool OnSingle(in POINTER_INFO info, PointerKind kind, POINTER_FLAGS contactFlag)
    {
        var tracked = IsTracked(info.pointerId);
        var down = IsDown(info.pointerFlags, contactFlag);
        if (!tracked && !down)
        {
            return false;
        }

        var position = new PhysicalPoint(info.ptPixelLocation.X, info.ptPixelLocation.Y);
        var stamp = Stamp(info.PerformanceCount);
        var samples = 0;
        Collect(
            ref samples,
            info.pointerId,
            kind,
            down,
            HasAny(info.pointerFlags, POINTER_FLAGS.POINTER_FLAG_CANCELED),
            position,
            Dot(position),
            stamp,
            CurrentOrigin()
        );
        Deliver(info.frameId, stamp, samples);
        return true;
    }

    private static bool HasAny(POINTER_FLAGS flags, POINTER_FLAGS any) =>
        (flags & any) != POINTER_FLAGS.POINTER_FLAG_NONE;

    private static bool IsContact(POINTER_FLAGS flags) =>
        HasAny(
            flags,
            POINTER_FLAGS.POINTER_FLAG_INCONTACT
                | POINTER_FLAGS.POINTER_FLAG_DOWN
                | POINTER_FLAGS.POINTER_FLAG_UP
                | POINTER_FLAGS.POINTER_FLAG_CANCELED
        );

    /// <summary>True while the contact is down: its contact flag is set and it is not ending.</summary>
    private static bool IsDown(POINTER_FLAGS flags, POINTER_FLAGS contactFlag) =>
        HasAny(flags, contactFlag)
        && !HasAny(flags, POINTER_FLAGS.POINTER_FLAG_UP | POINTER_FLAGS.POINTER_FLAG_CANCELED);

    /// <summary>
    /// Adds the sample of one pointer to the frame being built, deciding its phase from what was reported before:
    /// a contact that was not down and is now goes <see cref="PointerPhase.Down"/>; one that was down moves, lifts or
    /// is cancelled. Hover (never down) produces nothing.
    /// </summary>
    private void Collect(
        ref int samples,
        uint pointerId,
        PointerKind kind,
        bool down,
        bool canceled,
        PhysicalPoint position,
        PhysicalRect contact,
        DateTimeOffset stamp,
        PointerInputOrigin origin
    )
    {
        var index = IndexOfTracked(pointerId);
        PointerPhase phase;
        if (index >= 0)
        {
            phase =
                canceled ? PointerPhase.Cancel
                : down ? PointerPhase.Move
                : PointerPhase.Up;
            if (phase == PointerPhase.Move)
            {
                ref var tracked = ref _tracked[index];
                tracked.Position = position;
                tracked.Contact = contact;
                tracked.Origin = origin;
            }
            else
            {
                RemoveTracked(index);
            }
        }
        else if (down)
        {
            phase = PointerPhase.Down;
            AddTracked(
                new TrackedPointer
                {
                    PointerId = pointerId,
                    Kind = kind,
                    Position = position,
                    Contact = contact,
                    Origin = origin,
                }
            );
        }
        else
        {
            return;
        }

        if (samples == _scratch.Length)
        {
            Array.Resize(ref _scratch, _scratch.Length * 2);
        }

        _scratch[samples++] = new PointerSample(
            pointerId,
            kind,
            phase,
            position,
            contact,
            stamp,
            origin
        );
    }

    /// <summary>
    /// Hands the collected samples to the sink as one frame, ordered by pointer identifier, in a reused array, with
    /// the thread priority raised while contacts are down.
    /// </summary>
    private void Deliver(uint frameId, DateTimeOffset stamp, int samples)
    {
        if (samples == 0)
        {
            return;
        }

        for (var i = 1; i < samples; i++)
        {
            var sample = _scratch[i];
            var j = i - 1;
            while (j >= 0 && _scratch[j].PointerId > sample.PointerId)
            {
                _scratch[j + 1] = _scratch[j];
                j--;
            }

            _scratch[j + 1] = sample;
        }

        if (samples >= _frames.Length)
        {
            Array.Resize(ref _frames, samples * 2);
        }

        var buffer = _frames[samples] ??= new PointerSample[samples];
        Array.Copy(_scratch, buffer, samples);
        if (_trackedCount > 0 && !_boosted)
        {
            ContactPriorityBoost.Acquire();
            _boosted = true;
        }

        try
        {
            Sink.OnFrame(
                new PointerFrame(
                    frameId,
                    stamp,
                    ImmutableCollectionsMarshal.AsImmutableArray(buffer)
                )
            );
        }
        finally
        {
            if (_trackedCount == 0 && _boosted)
            {
                ContactPriorityBoost.Release();
                _boosted = false;
            }
        }
    }

    /// <summary>
    /// Delivers a <see cref="PointerPhase.Cancel"/> for a contact that is still tracked but will send no more messages
    /// (capture lost, or a leave without its up); true when the contact was tracked.
    /// </summary>
    private bool EndLostContact(uint pointerId)
    {
        var index = IndexOfTracked(pointerId);
        if (index < 0)
        {
            return false;
        }

        var tracked = _tracked[index];
        var samples = 0;
        var stamp = Stamp(0);
        Collect(
            ref samples,
            pointerId,
            tracked.Kind,
            down: false,
            canceled: true,
            tracked.Position,
            tracked.Contact,
            stamp,
            tracked.Origin
        );
        Deliver(0, stamp, samples);
        return true;
    }

    /// <summary>Ends every contact with a cancel and the hover, and forgets the window.</summary>
    private void Forget()
    {
        if (_trackedCount > 0)
        {
            var stamp = Stamp(0);
            var samples = 0;
            while (_trackedCount > 0)
            {
                var tracked = _tracked[0];
                Collect(
                    ref samples,
                    tracked.PointerId,
                    tracked.Kind,
                    down: false,
                    canceled: true,
                    tracked.Position,
                    tracked.Contact,
                    stamp,
                    tracked.Origin
                );
            }

            Deliver(0, stamp, samples);
        }

        if (_insideCount > 0)
        {
            _insideCount = 0;
            Sink.OnHover(false);
        }

        _hasTouchFrame = false;
        _source = null;
        _window = 0;
    }

    private void OnEnter(uint pointerId)
    {
        for (var i = 0; i < _insideCount; i++)
        {
            if (_inside[i] == pointerId)
            {
                return;
            }
        }

        if (_insideCount == _inside.Length)
        {
            Array.Resize(ref _inside, _inside.Length * 2);
        }

        _inside[_insideCount++] = pointerId;
        if (_insideCount == 1)
        {
            Sink.OnHover(true);
        }
    }

    private void OnLeave(uint pointerId)
    {
        for (var i = 0; i < _insideCount; i++)
        {
            if (_inside[i] != pointerId)
            {
                continue;
            }

            _inside[i] = _inside[--_insideCount];
            if (_insideCount == 0)
            {
                Sink.OnHover(false);
            }

            return;
        }
    }

    /// <summary>
    /// Reads the whole touch frame of the current message into <see cref="_touches"/>, growing it once if the frame
    /// has more contacts than it holds; 0 when the frame cannot be read.
    /// </summary>
    private unsafe int ReadTouchFrame(uint pointerId)
    {
        var count = (uint)_touches.Length;
        fixed (POINTER_TOUCH_INFO* buffer = _touches)
        {
            if (PInvoke.GetPointerFrameTouchInfo(pointerId, &count, buffer))
            {
                return (int)count;
            }
        }

        uint needed = 0;
        if (
            !PInvoke.GetPointerFrameTouchInfo(pointerId, &needed, null)
            || needed <= (uint)_touches.Length
        )
        {
            return 0;
        }

        _touches = new POINTER_TOUCH_INFO[needed];
        count = needed;
        fixed (POINTER_TOUCH_INFO* buffer = _touches)
        {
            return PInvoke.GetPointerFrameTouchInfo(pointerId, &count, buffer) ? (int)count : 0;
        }
    }

    /// <summary>
    /// The time of a frame on the <see cref="Clock"/> timeline: when Windows recorded it if the counter can be
    /// trusted, otherwise now; never earlier than the previous frame.
    /// </summary>
    private DateTimeOffset Stamp(ulong performanceCount)
    {
        var now = Clock.GetUtcNow();
        var stamp = now;
        if (
            performanceCount is > 0 and <= long.MaxValue
            && ReferenceEquals(Clock, TimeProvider.System)
        )
        {
            var age = Clock.GetElapsedTime((long)performanceCount);
            if (age >= TimeSpan.Zero && age <= Timings.Touch.PointerStampMaxAge)
            {
                stamp = now - age;
            }
        }

        if (stamp < _lastStamp)
        {
            stamp = _lastStamp;
        }

        _lastStamp = stamp;
        return stamp;
    }

    private bool IsTracked(uint pointerId) => IndexOfTracked(pointerId) >= 0;

    private int IndexOfTracked(uint pointerId)
    {
        for (var i = 0; i < _trackedCount; i++)
        {
            if (_tracked[i].PointerId == pointerId)
            {
                return i;
            }
        }

        return -1;
    }

    private void AddTracked(in TrackedPointer pointer)
    {
        if (_trackedCount == _tracked.Length)
        {
            Array.Resize(ref _tracked, _tracked.Length * 2);
        }

        _tracked[_trackedCount++] = pointer;
    }

    private void RemoveTracked(int index)
    {
        _trackedCount--;
        if (index < _trackedCount)
        {
            Array.Copy(_tracked, index + 1, _tracked, index, _trackedCount - index);
        }

        _tracked[_trackedCount] = default;
    }
}
