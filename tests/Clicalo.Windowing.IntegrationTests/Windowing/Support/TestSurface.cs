using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// A real surface for the tests: a <see cref="NonActivatingWindow"/> with a plain, non-focusable content that records
/// what reaches its window procedure (activation, focus, pointer and mouse messages) before the common hook sees it.
/// Counters are read from the test thread.
/// </summary>
public sealed class TestSurface : NonActivatingWindow
{
    private readonly ConcurrentQueue<string> _activations = new();
    private readonly ConcurrentQueue<string> _sequence = new();
    private readonly ConcurrentQueue<(long Timestamp, string Line)> _pointerLog = new();
    private int _pointerUpdates;
    private int _pointerDowns;
    private int _pointerUps;
    private int _mouseDowns;
    private int _mouseUps;
    private uint? _styleAtFirstShow;
    private nint? _ownerAtFirstShow;

    /// <summary>Creates a hidden surface of <paramref name="width"/> × <paramref name="height"/> logical units.</summary>
    public TestSurface(SurfaceId id, SurfaceRegistry registry, double width, double height)
        : base(id, registry)
    {
        Width = width;
        Height = height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x57, 0x9A)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            Focusable = false,
        };
    }

    /// <summary>The window as a handle (zero before it exists).</summary>
    public nint Handle => SurfaceWindow.Handle;

    /// <summary>Activation and focus messages received: <c>WM_ACTIVATE</c> (not inactive), <c>WM_NCACTIVATE(TRUE)</c>,
    /// <c>WM_ACTIVATEAPP(TRUE)</c> and <c>WM_SETFOCUS</c>, by name.</summary>
    public IReadOnlyList<string> Activations => [.. _activations];

    /// <summary>
    /// Every activation message received, in order and in both directions (<c>WM_ACTIVATEAPP</c>, <c>WM_NCACTIVATE</c>,
    /// <c>WM_ACTIVATE</c>), each with whether the surface owned the foreground at that moment: the diagnostic of the
    /// negative test of <c>ActivationGuard</c>.
    /// </summary>
    public IReadOnlyList<string> ActivationSequence => [.. _sequence];

    public int PointerDowns => Volatile.Read(ref _pointerDowns);

    public int PointerUps => Volatile.Read(ref _pointerUps);

    public int MouseDowns => Volatile.Read(ref _mouseDowns);

    public int MouseUps => Volatile.Read(ref _mouseUps);

    /// <summary>The extended style when the first <c>WM_SHOWWINDOW(TRUE)</c> arrived.</summary>
    public uint? StyleAtFirstShow => _styleAtFirstShow;

    /// <summary>The owner when the first <c>WM_SHOWWINDOW(TRUE)</c> arrived.</summary>
    public nint? OwnerAtFirstShow => _ownerAtFirstShow;

    /// <summary>
    /// The pointer messages received since <paramref name="since"/> (<see cref="System.Diagnostics.Stopwatch"/> ticks),
    /// each with its milliseconds after it, the pointer id and the screen point; <c>WM_POINTERUPDATE</c> only as a count.
    /// </summary>
    public IReadOnlyList<string> PointerLogSince(long since) =>
        [
            .. _pointerLog
                .Where(entry => entry.Timestamp >= since)
                .Select(entry =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"+{System.Diagnostics.Stopwatch.GetElapsedTime(since, entry.Timestamp).TotalMilliseconds:0.0} ms {Id} {entry.Line}"
                    )
                ),
            string.Create(
                CultureInfo.InvariantCulture,
                $"{Id}: {Volatile.Read(ref _pointerUpdates)} WM_POINTERUPDATE in total"
            ),
        ];

    /// <summary>Sets <see cref="NonActivatingWindow.ShadowMargin"/> (logical units on every side).</summary>
    public void UseShadowMargin(double margin) => ShadowMargin = new Thickness(margin);

    /// <inheritdoc />
    protected override void OnSurfaceInitialized() =>
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(Record);

    private nint Record(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        RecordSequence(hwnd, (uint)msg, wParam);
        RecordPointer((uint)msg, wParam, lParam);
        switch ((uint)msg)
        {
            case NativeSurface.WmActivate when (wParam & 0xFFFF) != 0:
                _activations.Enqueue("WM_ACTIVATE");
                break;
            case NativeSurface.WmNcActivate when wParam != 0:
                _activations.Enqueue("WM_NCACTIVATE");
                break;
            case NativeSurface.WmActivateApp when wParam != 0:
                _activations.Enqueue("WM_ACTIVATEAPP");
                break;
            case NativeSurface.WmSetFocus:
                _activations.Enqueue("WM_SETFOCUS");
                break;
            case NativeSurface.WmPointerDown:
                Interlocked.Increment(ref _pointerDowns);
                break;
            case NativeSurface.WmPointerUp:
                Interlocked.Increment(ref _pointerUps);
                break;
            case NativeSurface.WmLeftButtonDown:
                Interlocked.Increment(ref _mouseDowns);
                break;
            case NativeSurface.WmLeftButtonUp:
                Interlocked.Increment(ref _mouseUps);
                break;
            case NativeSurface.WmShowWindow when wParam != 0 && _styleAtFirstShow is null:
                _styleAtFirstShow = NativeSurface.ExStyle(hwnd);
                _ownerAtFirstShow = NativeSurface.GetWindow(hwnd, NativeSurface.Owner);
                break;
        }

        return 0;
    }

    private void RecordPointer(uint msg, nint wParam, nint lParam)
    {
        var name = msg switch
        {
            0x0238 => "WM_POINTERDEVICECHANGE",
            0x0239 => "WM_POINTERDEVICEINRANGE",
            0x023A => "WM_POINTERDEVICEOUTOFRANGE",
            0x0241 => "WM_NCPOINTERUPDATE",
            0x0242 => "WM_NCPOINTERDOWN",
            0x0243 => "WM_NCPOINTERUP",
            NativeSurface.WmPointerDown => "WM_POINTERDOWN",
            NativeSurface.WmPointerUp => "WM_POINTERUP",
            0x0249 => "WM_POINTERENTER",
            0x024A => "WM_POINTERLEAVE",
            NativeSurface.WmPointerActivate => "WM_POINTERACTIVATE",
            0x024C => "WM_POINTERCAPTURECHANGED",
            0x024D => "WM_TOUCHHITTESTING",
            NativeSurface.WmMouseActivate => "WM_MOUSEACTIVATE",
            NativeSurface.WmLeftButtonDown => "WM_LBUTTONDOWN",
            NativeSurface.WmLeftButtonUp => "WM_LBUTTONUP",
            0x0204 => "WM_RBUTTONDOWN",
            0x0205 => "WM_RBUTTONUP",
            0x007B => "WM_CONTEXTMENU",
            _ => null,
        };
        if (msg == 0x0245)
        {
            Interlocked.Increment(ref _pointerUpdates);
        }

        if (name is null)
        {
            return;
        }

        var x = (short)((long)lParam & 0xFFFF);
        var y = (short)(((long)lParam >> 16) & 0xFFFF);
        _pointerLog.Enqueue(
            (
                System.Diagnostics.Stopwatch.GetTimestamp(),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name} id={(long)wParam & 0xFFFF} flags=0x{((long)wParam >> 16) & 0xFFFF:X} at ({x}, {y})"
                )
            )
        );
    }

    private void RecordSequence(nint hwnd, uint msg, nint wParam)
    {
        var name = msg switch
        {
            NativeSurface.WmActivateApp => "WM_ACTIVATEAPP",
            NativeSurface.WmNcActivate => "WM_NCACTIVATE",
            NativeSurface.WmActivate => "WM_ACTIVATE",
            _ => null,
        };
        if (name is null)
        {
            return;
        }

        var active = msg == NativeSurface.WmActivate ? (wParam & 0xFFFF) != 0 : wParam != 0;
        var foreground = ForegroundWindows.Current;
        _sequence.Enqueue(
            name
                + (active ? "(TRUE)" : "(FALSE)")
                + (
                    foreground == hwnd
                        ? " in front"
                        : " not in front (" + WhoIsInFront(foreground) + ")"
                )
        );
    }

    /// <summary>Who owns the foreground when it is not this surface: the anchor, a surface, this process or another.</summary>
    private string WhoIsInFront(nint foreground)
    {
        if (foreground == 0)
        {
            return "nobody";
        }

        if (foreground == Registry.Anchor.Window.Handle)
        {
            return "the owner anchor";
        }

        if (Registry.TryGetSurface(new WindowToken(foreground), out var surface))
        {
            return "surface " + surface;
        }

        return ForegroundWindows.IsOfThisProcess(foreground)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"window 0x{foreground:X} of this process"
            )
            : "another process";
    }
}
