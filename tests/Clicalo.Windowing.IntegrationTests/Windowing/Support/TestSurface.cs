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

    /// <summary>Sets <see cref="NonActivatingWindow.ShadowMargin"/> (logical units on every side).</summary>
    public void UseShadowMargin(double margin) => ShadowMargin = new Thickness(margin);

    /// <inheritdoc />
    protected override void OnSurfaceInitialized() =>
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(Record);

    private nint Record(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        RecordSequence(hwnd, (uint)msg, wParam);
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
