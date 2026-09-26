using System.IO;
using System.Text;
using System.Windows.Interop;
using System.Windows.Threading;
using Clicalo.Domain.Touch;
using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Reporting;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Surfaces;

namespace Clicalo.Tools.SpikeLab.Session;

/// <summary>
/// The laboratory as a whole: the host of the real pieces and, once the maintainer chose a spike, its session.
/// </summary>
internal sealed class LabApp : IAsyncDisposable
{
    private bool _disposed;

    /// <summary>Creates the laboratory on the UI thread of <paramref name="dispatcher"/>.</summary>
    public LabApp(LabOptions options, Dispatcher dispatcher)
    {
        Options = options;
        Host = new LabHost(dispatcher);
    }

    /// <summary>The command line.</summary>
    public LabOptions Options { get; }

    /// <summary>The real pieces.</summary>
    public LabHost Host { get; }

    /// <summary>The spike run, once chosen.</summary>
    public LabSession? Session { get; private set; }

    /// <summary>Where the reports go.</summary>
    public string ReportDirectory => Options.ReportDirectory ?? ReportWriter.DefaultDirectory;

    /// <summary>Called on the UI thread when a session starts.</summary>
    public Action? SessionStarted { get; set; }

    /// <summary>Called when the maintainer asks to leave (tray menu).</summary>
    public Action? ExitRequested { get; set; }

    /// <summary>
    /// <c>--check</c>: composes every piece without showing anything (no tray icon, no shortcut, surfaces created
    /// without being shown), writes <c>check-&lt;fecha&gt;.md</c> to the reports folder and returns the exit code.
    /// </summary>
    public static async Task<int> CheckAsync(LabOptions options, Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(options);
        var app = new LabApp(options, dispatcher);
        await using (app)
        {
            await app.Host.StartAsync(quiet: true);
            var panel = new PanelSurface(
                app.Host.Registry,
                new LabSurfaceContext(
                    app.Host.Time,
                    new SilentSink(),
                    app.Host.Board,
                    app.Host.Directory,
                    app.Host.Log
                )
            );
            app.Host.Board.Try(
                "NonActivatingWindow",
                "Superficies no activables (creadas sin mostrarse).",
                () => new WindowInteropHelper(panel).EnsureHandle()
            );
            try
            {
                panel.Close();
            }
            catch (InvalidOperationException)
            {
                // The handle never finished its creation (windowing pending): nothing to close.
            }

            var text = new StringBuilder("# SpikeLab · comprobación de piezas\n\n");
            text.Append(
                app.Host.Board.AllReady
                    ? "Todas las piezas están listas.\n\n"
                    : "Hay piezas que no están listas: lo que dependa de ellas no se podrá medir.\n\n"
            );
            foreach (var component in app.Host.Board.Components)
            {
                text.Append("- ")
                    .Append(ReportJson.Name(component.State))
                    .Append(": ")
                    .Append(component.Name)
                    .Append(". ")
                    .Append(component.Detail)
                    .Append('\n');
            }

            Directory.CreateDirectory(app.ReportDirectory);
            var path = Path.Combine(
                app.ReportDirectory,
                "check-"
                    + app.Host.Time.GetLocalNow()
                        .ToString(
                            "yyyy-MM-dd-HHmmss",
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                    + ".md"
            );
            await File.WriteAllTextAsync(path, text.ToString(), new UTF8Encoding(false));
            return app.Host.Board.AllReady ? 0 : LabOptions.PiecesNotReadyExitCode;
        }
    }

    /// <summary>Composes the pieces and, when the command line names a spike, starts it.</summary>
    public async Task StartAsync()
    {
        await Host.StartAsync(quiet: false);
        if (Options.Spike is { } spike)
        {
            StartSpike(spike);
        }
    }

    /// <summary>Starts the run of <paramref name="spike"/> (once per process).</summary>
    public void StartSpike(SpikeId spike)
    {
        if (Session is not null || _disposed)
        {
            return;
        }

        Session = new LabSession(Host, SpikeScripts.For(spike), ReportDirectory)
        {
            ExitRequested = () => ExitRequested?.Invoke(),
        };
        SessionStarted?.Invoke();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Session is { } session)
        {
            await session.DisposeAsync();
        }

        await Host.DisposeAsync();
    }

    private sealed class SilentSink : ILabInputSink
    {
        public void OnTile(TileInput input) { }

        public void OnHandleDrag(string surface, SurfaceGroup group, PointerKind? pointer) { }

        public void OnIgnoredTouch(string surface, IgnoreReason reason) { }

        public void OnActivationMessage(string surface, string message) { }
    }
}
