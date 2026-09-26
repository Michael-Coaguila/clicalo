using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// The windowing of one UI thread as the application composes it (anchor, guard, registry and integrity check), on
/// the shared WPF test thread, with <see cref="TestSurface"/>s created on demand. Disposing it closes every surface
/// and destroys the anchor.
/// </summary>
public sealed class SurfaceLab : IDisposable
{
    private readonly List<TestSurface> _surfaces = [];

    private SurfaceLab(RecordingArbiter arbiter, TimeProvider timeProvider)
    {
        Arbiter = arbiter;
        Anchor = new OwnerAnchor();
        Guard = new ActivationGuard(arbiter, timeProvider);
        Registry = new SurfaceRegistry(Anchor, Guard);
        Integrity = new SurfaceIntegrityCheck(Registry, timeProvider);
    }

    public RecordingArbiter Arbiter { get; }

    public OwnerAnchor Anchor { get; }

    public ActivationGuard Guard { get; }

    public SurfaceRegistry Registry { get; }

    public SurfaceIntegrityCheck Integrity { get; }

    /// <summary>Creates the lab on the WPF test thread.</summary>
    public static SurfaceLab Create(
        RecordingArbiter? arbiter = null,
        TimeProvider? timeProvider = null
    ) =>
        WpfThread.Invoke(() =>
            new SurfaceLab(arbiter ?? new RecordingArbiter(), timeProvider ?? TimeProvider.System)
        );

    /// <summary>A new hidden surface; its handle exists only after <see cref="WithHandle"/>, a show or a move.</summary>
    public TestSurface CreateSurface(SurfaceKind kind, int instance, double width, double height) =>
        WpfThread.Invoke(() =>
        {
            var surface = new TestSurface(new SurfaceId(kind, instance), Registry, width, height);
            _surfaces.Add(surface);
            return surface;
        });

    /// <summary>Creates the handle of <paramref name="surface"/> without showing it and returns the surface.</summary>
    public static TestSurface WithHandle(TestSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        WpfThread.Invoke(() =>
            new System.Windows.Interop.WindowInteropHelper(surface).EnsureHandle()
        );
        return surface;
    }

    /// <summary>Closes <paramref name="surface"/> (its handle is destroyed and it leaves the registry).</summary>
    public void Close(TestSurface surface) =>
        WpfThread.Invoke(() =>
        {
            surface.Close();
            _surfaces.Remove(surface);
        });

    public void Dispose() =>
        WpfThread.Invoke(() =>
        {
            Integrity.Dispose();
            foreach (var surface in _surfaces)
            {
                surface.Close();
            }

            _surfaces.Clear();
            Anchor.Dispose();
        });
}
