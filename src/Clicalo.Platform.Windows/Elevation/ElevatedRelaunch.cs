using System.Globalization;
using Clicalo.Application.Ports;

namespace Clicalo.Platform.Windows.Elevation;

/// <summary>
/// «Reabrir como administrador» (LOG-007, EJE-013, user decision D7, ADR-0027): on demand, with the UAC prompt every
/// time and no system component. Only the installed executable, verified by <see cref="InstalledExecutable"/>, is ever
/// started elevated, with <c>--handover=&lt;pid&gt;</c> so it waits for this instance to end and then loads the document
/// this instance flushed on its way out (blueprint §3.3, «Relanzamiento elevado»). Nothing travels between the two.
/// </summary>
public sealed class ElevatedRelaunch : IElevatedRelaunch
{
    /// <summary>The option the elevated instance receives, followed by <c>=</c> and the process id it waits for.</summary>
    public const string HandoverOption = "--handover";

    private readonly string? _installed;
    private readonly string? _running;
    private readonly int _processId;
    private readonly IElevationLauncher _launcher;
    private readonly Func<string, FileAttributes?> _attributes;

    /// <summary>The relaunch of this process.</summary>
    /// <param name="installedExecutable">The installed <c>Clicalo.exe</c>, or null for a copy that is not installed.</param>
    public ElevatedRelaunch(string? installedExecutable)
        : this(
            installedExecutable,
            Environment.ProcessPath,
            Environment.ProcessId,
            Environment.IsPrivilegedProcess,
            new ShellRunasLauncher(),
            InstalledExecutable.OnDisk
        ) { }

    /// <summary>The relaunch over a fake launcher and disk (tests).</summary>
    internal ElevatedRelaunch(
        string? installedExecutable,
        string? runningExecutable,
        int processId,
        bool isElevated,
        IElevationLauncher launcher,
        Func<string, FileAttributes?> attributes
    )
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(attributes);
        _installed = installedExecutable;
        _running = runningExecutable;
        _processId = processId;
        IsElevated = isElevated;
        _launcher = launcher;
        _attributes = attributes;
    }

    /// <inheritdoc />
    public bool IsElevated { get; }

    /// <summary>The arguments of the elevated instance.</summary>
    /// <param name="processId">The process it waits for.</param>
    public static string HandoverArguments(int processId) =>
        HandoverOption + "=" + processId.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public Task<ElevationOutcome> RelaunchAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<ElevationOutcome>(cancellationToken);
        }

        if (IsElevated || !InstalledExecutable.IsVerified(_installed, _running, _attributes))
        {
            return Task.FromResult(ElevationOutcome.NotInstalled);
        }

        var executable = Path.GetFullPath(_installed!);
        var arguments = HandoverArguments(_processId);
        var answer = new TaskCompletionSource<ElevationOutcome>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        // The UAC prompt blocks the caller until the person answers: a short-lived STA thread of its own, never the UI.
        var thread = new Thread(() =>
        {
            try
            {
                answer.SetResult(_launcher.Launch(executable, arguments));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _ = ex;
                answer.SetResult(ElevationOutcome.Failed);
            }
        })
        {
            Name = "Clicalo.Elevation",
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return answer.Task;
    }
}
