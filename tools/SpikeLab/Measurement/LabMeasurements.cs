using System.Globalization;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Measurement;

/// <summary>
/// The automatic measurements of the laboratory: running counters (read by <see cref="RepetitionRecorder"/>), the
/// current foreground and the last values the guide strip shows. Counters are thread-safe (the SysEvents thread, the
/// probe reader and the UI thread write them); <see cref="Changed"/> is raised on the UI thread, coalesced.
/// </summary>
internal sealed class LabMeasurements
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private readonly Func<long> _violations;
    private readonly Action<Action> _post;
    private long _foregroundChanges;
    private long _ownForegroundChanges;
    private long _surfaceActivations;
    private long _probeF24;
    private long _probeChars;
    private long _probeMenus;
    private long _probeModifierKeys;
    private long _rightsChords;
    private long _refused;
    private int _changePosted;
    private ForegroundChange? _foreground;

    /// <summary>Creates the measurements.</summary>
    /// <param name="violations">Reads <c>ActivationGuard.Violations</c>.</param>
    /// <param name="log">The timeline.</param>
    /// <param name="post">Runs an action on the UI thread.</param>
    public LabMeasurements(Func<long> violations, LabEventLog log, Action<Action> post)
    {
        _violations = violations;
        Log = log;
        _post = post;
    }

    /// <summary>Raised on the UI thread after the measurements changed (several changes may raise it once).</summary>
    public event EventHandler? Changed;

    /// <summary>The timeline.</summary>
    public LabEventLog Log { get; }

    /// <summary>The current foreground, as last seen.</summary>
    public ForegroundChange? Foreground => Volatile.Read(ref _foreground);

    /// <summary>«Última orden»: the last tile command and its pattern.</summary>
    public string? LastCommand { get; private set; }

    /// <summary>«Concesión»: the last lease and its result.</summary>
    public string? LastLease { get; private set; }

    /// <summary>«Devolución»: how the last lease gave the foreground back.</summary>
    public string? LastRestore { get; private set; }

    /// <summary>The latency of the last tap or command, in milliseconds.</summary>
    public double? LastLatencyMs { get; private set; }

    /// <summary>The last notice for the maintainer (a refused injection, a denied lease, a tap that did not count…).</summary>
    public string? LastNotice { get; private set; }

    /// <summary>The running counters.</summary>
    public MeasurementCounters Read() =>
        new(
            Interlocked.Read(ref _foregroundChanges),
            Interlocked.Read(ref _ownForegroundChanges),
            Interlocked.Read(ref _surfaceActivations),
            _violations(),
            Interlocked.Read(ref _probeF24),
            Interlocked.Read(ref _probeChars),
            Interlocked.Read(ref _probeMenus),
            Interlocked.Read(ref _probeModifierKeys),
            Interlocked.Read(ref _rightsChords),
            Interlocked.Read(ref _refused)
        );

    /// <summary>A new foreground window (any thread).</summary>
    public void OnForeground(ForegroundChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Volatile.Write(ref _foreground, change);
        Interlocked.Increment(ref _foregroundChanges);
        if (change.Surface is not null)
        {
            Interlocked.Increment(ref _ownForegroundChanges);
        }

        Log.Add("foreground", "Primer plano: " + change.Describe() + ".");
        PostChanged();
    }

    /// <summary>A lab surface received an activation message (UI thread, inside its window procedure).</summary>
    public void OnSurfaceActivation(string surface, string message)
    {
        Interlocked.Increment(ref _surfaceActivations);
        Log.Add("activation", surface + " recibió " + message + ".");
        PostChanged();
    }

    /// <summary>A REG-01 violation was counted by <c>ActivationGuard</c>.</summary>
    public void OnViolation(string detail)
    {
        Log.Add("violation", detail);
        Notice("reg01.violations subió: " + detail);
    }

    /// <summary>An event of InputProbe (any thread).</summary>
    public void OnProbe(ProbeSignal signal)
    {
        switch (signal)
        {
            case ProbeSignal.F24:
                Interlocked.Increment(ref _probeF24);
                Log.Add("probe", "La sonda recibió VK_F24.");
                break;
            case ProbeSignal.Character:
                Interlocked.Increment(ref _probeChars);
                Log.Add("probe", "La sonda recibió un carácter.");
                break;
            case ProbeSignal.Menu:
                Interlocked.Increment(ref _probeMenus);
                Log.Add("probe", "La sonda recibió WM_SYSCOMMAND(SC_KEYMENU).");
                break;
            case ProbeSignal.ModifierKey:
                Interlocked.Increment(ref _probeModifierKeys);
                break;
            default:
                return;
        }

        PostChanged();
    }

    /// <summary>The laboratory injected the internal rights chord (any thread).</summary>
    public void OnRightsChord()
    {
        Interlocked.Increment(ref _rightsChords);
        Log.Add(
            "injection",
            "SpikeLab inyectó el atajo interno de derechos (paso 2 de la escalera)."
        );
        PostChanged();
    }

    /// <summary>The laboratory refused to inject a batch (any thread).</summary>
    public void OnRefused(string reason)
    {
        Interlocked.Increment(ref _refused);
        Log.Add("refused", reason);
        Notice(reason);
    }

    /// <summary>A tile command was executed (UI thread).</summary>
    public void OnCommand(string description, double latencyMs)
    {
        LastCommand = description;
        LastLatencyMs = latencyMs;
        Log.Add("command", description + string.Create(Spanish, $" ({latencyMs:0.0} ms)."));
        PostChanged();
    }

    /// <summary>A lease was requested or ended (UI thread).</summary>
    public void OnLease(string lease, string? restore)
    {
        LastLease = lease;
        if (restore is not null)
        {
            LastRestore = restore;
        }

        Log.Add("lease", lease + (restore is null ? "." : "; devolución: " + restore + "."));
        PostChanged();
    }

    /// <summary>Shows <paramref name="text"/> in the notice line of the guide strip (any thread).</summary>
    public void Notice(string text)
    {
        LastNotice = text;
        PostChanged();
    }

    /// <summary>Asks for a refresh of the guide strip (any thread).</summary>
    public void PostChanged()
    {
        if (Interlocked.Exchange(ref _changePosted, 1) == 1)
        {
            return;
        }

        _post(() =>
        {
            Volatile.Write(ref _changePosted, 0);
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }
}
