using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Clicalo.Application.Ports;
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
}
