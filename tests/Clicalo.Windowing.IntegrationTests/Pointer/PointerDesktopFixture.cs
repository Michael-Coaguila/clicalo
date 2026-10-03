using System.Windows;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// The desktop of the pointer tests: one <see cref="PointerTestSurface"/> shown passively, topmost, in the middle of
/// the primary work area, with its <see cref="PointerInputSource"/>, a <see cref="GestureRecognizer"/> on the default
/// preset (Mild tremor, TAC-001) fed by a <see cref="GestureHost"/>, and a recorder. Started only when desktop tests
/// are enabled. The synthetic pointers may only touch this process's windows.
/// </summary>
public sealed class PointerDesktopFixture : IAsyncLifetime
{
    /// <summary>How long a test waits for the frames or gestures it expects.</summary>
    public static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private OwnerAnchor? _anchor;
    private PointerTestSurface? _surface;
    private GestureHost? _host;

    /// <summary>The filter values of the surface: the default preset.</summary>
    public static TouchSettings Settings { get; } =
        new(
            TouchPresets.Default.Debounce,
            TouchPresets.Default.HitSlopPx,
            TouchPresets.Default.CancelMovePx,
            TouchPresets.Default.MinContact
        );

    public PointerTestArbiter Arbiter { get; } = new();

    public PointerRecorder Recorder { get; } = new(TimeProvider.System);

    public PointerTestSurface Surface => _surface ?? throw NotStarted();

    public GestureHost Host => _host ?? throw NotStarted();

    public async ValueTask InitializeAsync()
    {
        if (!DesktopTestEnvironment.IsEnabled)
        {
            return;
        }

        var firstFrame = WpfThread.Invoke(() =>
        {
            _anchor = new OwnerAnchor();
            var guard = new ActivationGuard(Arbiter, TimeProvider.System);
            var registry = new SurfaceRegistry(_anchor, guard);
            var surface = new PointerTestSurface(registry, Recorder, TimeProvider.System);
            var work = SystemParameters.WorkArea;
            surface.Left = work.Left + ((work.Width - surface.Width) / 2);
            surface.Top = work.Top + ((work.Height - surface.Height) / 2);
            var composed = FirstFrame.Watch(surface);
            surface.ShowPassive();
            _surface = surface;
            return composed;
        });
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        WpfThread.Invoke(() =>
        {
            var recognizer = new GestureRecognizer(Settings, Surface.DpiScale);
            recognizer.SetTargets(Surface.Targets());
            _host = new GestureHost(
                recognizer,
                WpfThread.Dispatcher,
                TimeProvider.System,
                Recorder.OnGesture
            );
            Recorder.Host = _host;
        });

        // A gesture sent before the surface is composed falls through to the window below (FirstFrame).
        await firstFrame.WaitAsync(EventTimeout, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Starts a test: waits until the previous gestures are over and the filter memory of every tile has expired,
    /// then forgets what was recorded.
    /// </summary>
    public async Task PrepareAsync()
    {
        await WaitUntilAsync(
            () => WpfThread.Invoke(() => Surface.Source.ActiveContacts == 0),
            "the previous test left a contact down"
        );
        await Task.Delay(
            Settings.Debounce + Timings.Touch.PostSwipeLock,
            TestContext.Current.CancellationToken
        );
        Recorder.Clear();
    }

    /// <summary>A pointer of <paramref name="kind"/> that may only touch this process's windows.</summary>
    public static SyntheticPointer CreatePointer(SyntheticPointerKind kind) =>
        new(kind, [Environment.ProcessId]);

    /// <summary>The recognizer target of tile <paramref name="id"/> (1 to 4).</summary>
    public TouchTarget Tile(int id) =>
        WpfThread.Invoke(() => Host.Recognizer.Targets.Single(t => t.Id.Value == id));

    /// <summary>The center of tile <paramref name="id"/>, in physical pixels.</summary>
    public PhysicalPoint CenterOf(int id) => Tile(id).Bounds.Center;

    /// <summary>Waits until <paramref name="condition"/> holds, failing with <paramref name="because"/> on timeout.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(EventTimeout);
        while (!condition())
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), deadline.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                condition().ShouldBeTrue(because);
                return;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_surface is not null)
        {
            WpfThread.Invoke(() =>
            {
                _host?.Dispose();
                _surface.Close();
                _anchor?.Dispose();
            });
        }

        return ValueTask.CompletedTask;
    }

    private static InvalidOperationException NotStarted() => new(DesktopTestEnvironment.SkipReason);
}
