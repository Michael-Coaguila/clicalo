using System.Globalization;
using System.IO;
using System.Windows.Automation;
using System.Windows.Threading;
using Clicalo.Application.Foreground;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Input;
using Clicalo.Tools.SpikeLab.Measurement;
using Clicalo.Tools.SpikeLab.Reporting;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Surfaces;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.Tools.SpikeLab.Views;
using Clicalo.UI.Wpf.Automation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Tools.SpikeLab.Session;

/// <summary>
/// One run of a spike script: the lab surfaces, the script engine, the repetition recorder, the lease flows and the
/// report. It executes every tile action, counts the repetitions that match the current step, keeps the guide strip
/// up to date and writes the report after every change. UI thread only.
/// </summary>
internal sealed class LabSession : ILabInputSink, IAsyncDisposable
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private static readonly TimeSpan RestoreBudget = Timings.Windowing.ViolationRestoreBudget;
    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(10);

    private readonly LabHost _host;
    private readonly ScriptEngine _engine;
    private readonly RepetitionRecorder _recorder;
    private readonly ReportWriter _writer;
    private readonly LeaseFlows _flows;
    private readonly LiveAnnouncer _announcer;
    private readonly List<GuideView> _guides = [];
    private readonly MachineInfo _machine = MachineInfo.Current();
    private readonly DispatcherTimer _reportTimer;
    private MeasurementCounters _stepBaseline;
    private ForegroundChange? _target;
    private ForegroundChange? _lastExternal;
    private bool _targetFrozen;
    private bool _forcing;
    private int _notices;
    private bool _voiceNumbers;

    /// <summary>Starts <paramref name="script"/>: creates the surfaces and shows the guide strip.</summary>
    public LabSession(LabHost host, SpikeScript script, string reportDirectory)
    {
        _host = host;
        _engine = new ScriptEngine(script, host.Time, RestoreBudget);
        _recorder = new RepetitionRecorder(
            host.Time,
            host.Measurements.Read,
            host.Post,
            OnRepetitionClosed
        );
        _writer = new ReportWriter(reportDirectory, script.Id, _engine.StartedAt);
        Surfaces = new LabSurfaces(
            host.Registry,
            new LabSurfaceContext(host.Time, this, host.Board, host.Directory, host.Log)
        );
        _announcer = new LiveAnnouncer(Surfaces.Guide.View.Notice);
        _guides.Add(Surfaces.Guide.View);
        _flows = new LeaseFlows(
            host,
            Surfaces,
            () => script.Id == SpikeId.S4,
            () => _recorder.Rebase(),
            OnLeaseCycle,
            Announce,
            ReleaseAll,
            () => ExitRequested?.Invoke()
        );

        _lastExternal = host.Watcher.Current is { IsOwnProcess: false } current ? current : null;
        ResetStep();
        host.ForegroundChanged = OnForeground;
        host.GlobalHotkeyPressed = () => _ = _flows.OpenSearchAsync(LeaseOrigin.GlobalHotkey);
        host.TrayMenuRequested = anchor => _ = _flows.ShowTrayMenuAsync(anchor);
        host.Latch.Changed += (_, _) => ShowLatches();
        host.Measurements.Changed += (_, _) => RefreshGuides();
        host.Board.Changed += (_, _) => RefreshGuides();
        _engine.Changed += (_, _) =>
        {
            RefreshGuides();
            WriteReport();
        };

        // The strip is placed from its size with the first step in it, so it appears where it stays.
        RefreshGuides();
        Surfaces.Place();
        if (!Surfaces.Guide.TryShow())
        {
            host.Measurements.Notice(
                "La tira-guía no se pudo mostrar como ventana no activable (ver piezas): no se puede medir."
            );
        }
        _reportTimer = new DispatcherTimer { Interval = ReportInterval };
        _reportTimer.Tick += (_, _) => WriteReport();
        _reportTimer.Start();
        host.Log.Add("session", "Empieza " + script.Title + ".");
        RefreshGuides();
        WriteReport();
    }

    /// <summary>Raised when the maintainer chose «Salir» in the tray menu.</summary>
    public Action? ExitRequested { get; set; }

    /// <summary>The spike being run.</summary>
    public SpikeScript Script => _engine.Script;

    /// <summary>The lab surfaces.</summary>
    public LabSurfaces Surfaces { get; }

    /// <summary>The JSON report.</summary>
    public string ReportPath => _writer.JsonPath;

    /// <summary>The Markdown summary.</summary>
    public string SummaryPath => _writer.MarkdownPath;

    /// <summary>«Enviar teclas»: the chord tiles send real keys to the app in front (off by default: S1.md).</summary>
    public bool SendsKeys { get; set; }

    /// <summary>
    /// «Números de voz» on the panel tiles (ACC-009): the accessible names start with «{n} ». Set from the control
    /// window or from the step action of S3 row 6.
    /// </summary>
    public bool VoiceNumbers
    {
        get => _voiceNumbers;
        set
        {
            var changed = _voiceNumbers != value;
            _voiceNumbers = value;
            var number = 1;
            foreach (var tile in LabTiles.Panel)
            {
                if (Surfaces.Panel.Find(tile.Id) is { } control)
                {
                    control.VoiceNumber = value ? number : null;
                }

                number++;
            }

            if (changed)
            {
                VoiceNumbersChanged?.Invoke(value);
                RefreshGuides();
            }

            WriteReport();
        }
    }

    /// <summary>Raised on the UI thread when «Números de voz» changes, so the control window shows it too.</summary>
    public Action<bool>? VoiceNumbersChanged { get; set; }

    /// <summary>«Mostrar superficies».</summary>
    public void ShowSurfaces()
    {
        if (!Surfaces.ShowUnderTest())
        {
            _host.Measurements.Notice(
                "Las superficies no se pudieron mostrar como ventanas no activables (ver piezas)."
            );
        }

        if (!Surfaces.Guide.IsShown)
        {
            _ = Surfaces.Guide.TryShow();
        }
    }

    /// <summary>«Ocultar superficies».</summary>
    public void HideSurfaces() => Surfaces.HideUnderTest();

    /// <summary>«Empezar ciclo» and «Repetir»: the current step starts again from zero.</summary>
    public void Repeat()
    {
        _recorder.Flush();
        _engine.Repeat();
        ResetStep();
        RefreshGuides();
    }

    /// <summary>«Funcionó».</summary>
    public void Worked()
    {
        if (_engine.CurrentStep is not { } step)
        {
            return;
        }

        if (step.NeedsConfirmation)
        {
            _recorder.Flush();
            _engine.MarkWorked(RepetitionEvidence.Empty);
        }
        else
        {
            _engine.MarkWorked(ManualEvidence());
        }
    }

    /// <summary>«Falló».</summary>
    public void Failed()
    {
        if (_engine.CurrentStep is null)
        {
            return;
        }

        _engine.MarkFailed(ManualEvidence());
    }

    /// <summary>«Siguiente».</summary>
    public void Next()
    {
        _recorder.Flush();
        _engine.Next();
        ResetStep();
        RefreshGuides();
    }

    /// <summary>«Paso anterior» in the control window.</summary>
    public void Previous()
    {
        var index = _engine.Snapshot().CurrentIndex;
        if (index == 0)
        {
            return;
        }

        _recorder.Flush();
        _engine.GoTo(index - 1);
        ResetStep();
        RefreshGuides();
    }

    /// <summary>«Soltar todo»: clears the latches and releases any left modifier that is down.</summary>
    public void ReleaseAll()
    {
        _host.Latch.Clear();
        var released = _host.Injector.ReleaseHeldModifiers();
        var text =
            released.Count == 0
                ? "Soltar todo: no había ninguna tecla pulsada ni enclavada."
                : "Soltar todo: se soltaron " + string.Join(", ", released) + ".";
        _host.Log.Add("release", text);
        _host.Measurements.Notice(text);
    }

    /// <summary>«Aviso cortés» or «Aviso urgente» (S3 row 9).</summary>
    public void AnnounceTest(AnnouncementUrgency urgency)
    {
        _notices++;
        Announce(
            string.Create(
                Spanish,
                $"{(urgency == AnnouncementUrgency.Assertive ? "Aviso urgente" : "Aviso cortés")} de prueba número {_notices}."
            ),
            urgency
        );
    }

    /// <summary>
    /// «Forzar activación del panel» (S1 row 31): calls <c>SetForegroundWindow</c> on the panel without a lease and
    /// measures whether <c>ActivationGuard</c> counts it and the previous app comes back within the budget.
    /// </summary>
    public async Task ForceActivationAsync()
    {
        var panel = Surfaces.Panel;
        if (!panel.IsShown || panel.Handle == 0)
        {
            _host.Measurements.Notice("Muestra primero las superficies.");
            return;
        }

        // One forced activation at a time: a second tap while the first is measured would chain the foreground
        // handlers below and count two violations inside one repetition.
        if (_forcing)
        {
            _host.Measurements.Notice(
                "Espera: la activación forzada anterior aún se está midiendo."
            );
            return;
        }

        _forcing = true;
        try
        {
            await ForceActivationCoreAsync(panel);
        }
        finally
        {
            _forcing = false;
        }
    }

    /// <summary>«Abrir sonda».</summary>
    public async Task OpenProbeAsync()
    {
        try
        {
            await _host.Probe.OpenAsync(CancellationToken.None);
        }
        catch (Exception ex)
            when (ex is InputProbeException or TimeoutException or FileNotFoundException)
        {
            _host.Measurements.Notice("No se pudo abrir la sonda: " + ex.Message);
        }
    }

    /// <summary>«Centro de control» in the control window (origin Touch: the tap on the control window gives the right).</summary>
    public Task OpenControlCenterAsync() => _flows.OpenControlCenterAsync(LeaseOrigin.Touch);

    /// <summary>Writes the report now and waits for it.</summary>
    public Task SaveReportAsync()
    {
        var snapshot = _engine.Snapshot();
        var context = Context();
        return _writer.WriteAsync(
            ReportJson.Write(snapshot, context),
            MarkdownSummary.Render(snapshot, context),
            CancellationToken.None
        );
    }

    /// <inheritdoc />
    public void OnTile(TileInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tile = input.Tile;
        if (ArrangeGuide(tile.Action))
        {
            // Folding or moving the strip is not an order under test: «Última orden» keeps the last real one.
            return;
        }

        var outcome = Execute(input);
        var latency = _host.Time.GetUtcNow() - input.StartedAt;
        _host.Measurements.OnCommand(
            tile.Describe()
                + " por "
                + How(input)
                + (outcome is null ? string.Empty : " · " + outcome),
            latency.TotalMilliseconds
        );
        if (
            input.Control is { } control
            && tile.Action is not (LabAction.CycleShift or LabAction.ToggleControl)
        )
        {
            Flash(control);
        }

        if (!tile.IsTestTarget)
        {
            return;
        }

        Trigger(
            new RepetitionEvidence
            {
                Trigger = new TriggerInfo(
                    input.IsCommand ? StepTrigger.UiaCommand : StepTrigger.SurfaceTap
                )
                {
                    Surface = input.Surface,
                    Group = input.Group,
                    Tile = tile.Id,
                    Pattern = input.IsCommand ? input.Pattern : null,
                    Pointer = input.Pointer,
                    Channel = input.Channel,
                    VoiceNumber = input.Control?.VoiceNumber,
                },
                LatencyMs = latency.TotalMilliseconds,
            }
        );
    }

    /// <inheritdoc />
    public void OnHandleDrag(string surface, SurfaceGroup group, PointerKind? pointer) =>
        Trigger(
            new RepetitionEvidence
            {
                Trigger = new TriggerInfo(StepTrigger.HandleDrag)
                {
                    Surface = surface,
                    Group = group,
                    Pointer = pointer,
                    Channel = "pointer",
                },
            }
        );

    /// <inheritdoc />
    public void OnIgnoredTouch(string surface, IgnoreReason reason)
    {
        _host.Log.Add("ignored", surface + ": toque ignorado (" + reason + ").");
        _host.Measurements.Notice("Toque ignorado por el filtro (" + reason + "): no cuenta.");
    }

    /// <inheritdoc />
    public void OnActivationMessage(string surface, string message) =>
        _host.Measurements.OnSurfaceActivation(surface, message);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _reportTimer.Stop();
        await _flows.CloseAllAsync();
        _recorder.Flush();
        _host.Log.Add("session", "Termina " + Script.Title + ".");
        await SaveReportAsync();
        _recorder.Dispose();
        _writer.Dispose();
        Surfaces.Dispose();
    }

    private async Task ForceActivationCoreAsync(PanelSurface panel)
    {
        var expected = _lastExternal;
        var started = _host.Time.GetTimestamp();
        var back = new TaskCompletionSource<TimeSpan>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var previous = _host.ForegroundChanged;
        _host.ForegroundChanged = change =>
        {
            previous?.Invoke(change);
            if (expected is not null && change.Window == expected.Window)
            {
                back.TrySetResult(_host.Time.GetElapsedTime(started));
            }
        };

        _host.Log.Add(
            "forced",
            "SpikeLab llama a SetForegroundWindow sobre el panel, sin concesión."
        );
        _ = PInvoke.SetForegroundWindow((HWND)panel.Handle);
        double? restoredWithin = null;
        try
        {
            var limit = Task.Delay(RestoreBudget * 3, _host.Time);
            if (await Task.WhenAny(back.Task, limit) == back.Task)
            {
                restoredWithin = (await back.Task).TotalMilliseconds;
            }
        }
        finally
        {
            _host.ForegroundChanged = previous;
        }

        var style = (WINDOW_EX_STYLE)
            PInvoke.GetWindowLong((HWND)panel.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        Trigger(
            new RepetitionEvidence
            {
                Trigger = new TriggerInfo(StepTrigger.ForcedActivation)
                {
                    Surface = panel.SurfaceName,
                    Group = SurfaceGroup.Panel,
                },
                RestoredWithinMs = restoredWithin,
                NoActivateStyleKept = style.HasFlag(WINDOW_EX_STYLE.WS_EX_NOACTIVATE),
            }
        );
    }

    private string? Execute(TileInput input)
    {
        var tile = input.Tile;
        switch (tile.Action)
        {
            case LabAction.SendChord when tile.Chord is { } chord:
                return SendChord(chord);
            case LabAction.CycleShift:
                _host.Latch.CycleShift();
                return null;
            case LabAction.ToggleControl:
                _host.Latch.ToggleControl();
                return null;
            case LabAction.ReleaseAll:
                ReleaseAll();
                return null;
            case LabAction.OpenSearch:
                _ = _flows.OpenSearchAsync(
                    input.IsCommand ? LeaseOrigin.UiaInvoke : LeaseOrigin.Touch
                );
                return null;
            case LabAction.OpenControlCenter:
                _ = _flows.OpenControlCenterAsync(
                    input.IsCommand ? LeaseOrigin.UiaInvoke : LeaseOrigin.Touch
                );
                return null;
            case LabAction.ToggleProfile:
                Expand(Surfaces.Profiles, LabTiles.Profile, input);
                return null;
            case LabAction.ToggleSideWindow:
                Expand(Surfaces.DockSide, "dock-handle", input);
                return null;
            case LabAction.ChooseProfile:
                Surfaces.Profiles.TryHide();
                SetExpanded(LabTiles.Profile, expanded: false);
                return null;
            case LabAction.SearchResult:
                _ = _flows.CloseSearchAsync(result: true);
                return null;
            case LabAction.SearchClose:
                _ = _flows.CloseSearchAsync(result: false);
                return null;
            case LabAction.SearchDictate:
                _ = _flows.DictateAsync();
                return null;
            case LabAction.SearchField:
                _ = _flows.FocusSearchFieldAsync();
                return null;
            case LabAction.GuideWorked:
                Worked();
                return null;
            case LabAction.GuideFailed:
                Failed();
                return null;
            case LabAction.GuideRepeat:
                Repeat();
                return null;
            case LabAction.GuideNext:
                Next();
                return null;
            case LabAction.GuideStepAction:
                RunStepAction();
                return null;
            default:
                return null;
        }
    }

    private bool ArrangeGuide(LabAction action)
    {
        switch (action)
        {
            case LabAction.GuideFold:
                Surfaces.Guide.ToggleFolded();
                return true;
            case LabAction.GuideInstruction:
                Surfaces.Guide.ToggleFullInstruction();
                return true;
            case LabAction.GuideMove:
                Surfaces.Guide.MoveToOtherHalf();
                return true;
            default:
                return false;
        }
    }

    private string SendChord(LabChord chord)
    {
        var full = chord.With(_host.Latch.Consume());
        if (!SendsKeys)
        {
            return "sin enviar (" + full.Describe() + "; «Enviar teclas» está desactivado)";
        }

        var outcome = _host.Injector.Send(full);
        if (!outcome.Sent)
        {
            _host.Measurements.OnRefused(outcome.Reason ?? "Envío rechazado.");
            return "no enviado";
        }

        return "enviado " + full.Describe();
    }

    private void RunStepAction()
    {
        switch (_engine.CurrentStep?.Action)
        {
            case StepAction.ForceActivation:
                _ = ForceActivationAsync();
                break;
            case StepAction.PoliteNotice:
                AnnounceTest(AnnouncementUrgency.Polite);
                break;
            case StepAction.AssertiveNotice:
                AnnounceTest(AnnouncementUrgency.Assertive);
                break;
            case StepAction.ToggleVoiceNumbers:
                VoiceNumbers = !VoiceNumbers;
                break;
        }
    }

    private void Expand(LabSurface window, string tileId, TileInput input)
    {
        var expand =
            input.IsCommand && input.Pattern == CommandPattern.ExpandCollapse
                ? input.Expand
                : !window.IsShown;
        if (expand)
        {
            window.TryShow();
        }
        else
        {
            window.TryHide();
        }

        SetExpanded(tileId, window.IsShown);
    }

    private void SetExpanded(string tileId, bool expanded)
    {
        foreach (var surface in (LabSurface[])[Surfaces.Panel, Surfaces.Dock])
        {
            if (surface.Find(tileId) is { } control)
            {
                control.IsExpanded = expanded;
                TileFactory.ShowHighlighted(control, expanded);
            }
        }
    }

    private void ShowLatches()
    {
        if (Surfaces.Panel.Find(LabTiles.Shift) is { } shift)
        {
            var state = _host.Latch.Shift;
            shift.ToggleState = state switch
            {
                LatchState.Once => ToggleState.On,
                LatchState.Locked => ToggleState.Indeterminate,
                _ => ToggleState.Off,
            };
            shift.Tag = state == LatchState.Locked ? "⇪" : "⇧";
            TileFactory.ShowHighlighted(shift, state != LatchState.Off);
        }

        if (Surfaces.Panel.Find(LabTiles.Control) is { } control)
        {
            control.ToggleState = _host.Latch.Control ? ToggleState.On : ToggleState.Off;
            TileFactory.ShowHighlighted(control, _host.Latch.Control);
        }
    }

    private static void Flash(ShortcutTile control)
    {
        if (control.IsExpanded || control.ToggleState != ToggleState.Off)
        {
            return;
        }

        TileFactory.ShowHighlighted(control, highlighted: true);
        var timer = new DispatcherTimer { Interval = Timings.Notices.ExecutionFlash };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!control.IsExpanded && control.ToggleState == ToggleState.Off)
            {
                TileFactory.ShowHighlighted(control, highlighted: false);
            }
        };
        timer.Start();
    }

    private void Announce(string text, AnnouncementUrgency urgency)
    {
        _host.Log.Add("notice", text);
        try
        {
            _announcer.Announce(text, urgency);
            _host.Board.Ready("LiveAnnouncer", "Región live y RaiseNotificationEvent (ACC-001).");
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            _host.Board.Fail("LiveAnnouncer", ex);
            _host.Measurements.Notice(text);
        }
    }

    private void Trigger(RepetitionEvidence candidate)
    {
        if (_engine.CurrentStep is not { } step)
        {
            return;
        }

        if (StepTriggerMatcher.Mismatch(step, candidate) is { } reason)
        {
            if (candidate.Trigger?.Kind == step.Trigger)
            {
                _host.Measurements.Notice(reason);
            }

            return;
        }

        // The app in front when the first repetition starts is the target app of the step.
        _target ??= _lastExternal;
        _targetFrozen = true;
        _recorder.Trigger(candidate, InputProbeSession.SettleTime);
    }

    private void OnLeaseCycle(RepetitionEvidence evidence) => Trigger(evidence);

    private void OnRepetitionClosed(RepetitionEvidence evidence)
    {
        var foreground = _host.Watcher.Current;
        _engine.RecordAutomatic(
            evidence with
            {
                ForegroundProcess = foreground.Describe(),
                TargetProcess = _target?.Describe(),
                TargetInFront = _target is null ? null : foreground.Window == _target.Window,
            }
        );
    }

    private RepetitionEvidence ManualEvidence()
    {
        var foreground = _host.Watcher.Current;
        _target ??= _lastExternal;
        _targetFrozen = true;
        return new RepetitionEvidence
        {
            Delta = _recorder.TakeManual(),
            ForegroundProcess = foreground.Describe(),
            TargetProcess = _target?.Describe(),
            TargetInFront = _target is null ? null : foreground.Window == _target.Window,
        };
    }

    private void OnForeground(ForegroundChange change)
    {
        if (change.IsOwnProcess || change.Window == 0)
        {
            return;
        }

        _lastExternal = change;

        // Until the first repetition starts, the maintainer is still bringing the target app to the front.
        if (!_targetFrozen)
        {
            _target = change;
        }
    }

    private void ResetStep()
    {
        _recorder.Rebase();
        _stepBaseline = _host.Measurements.Read();
        _target = _lastExternal;
        _targetFrozen = false;
    }

    private void RefreshGuides()
    {
        var model = GuideModelBuilder.Build(_engine.Snapshot(), Status(), RestoreBudget);
        foreach (var guide in _guides)
        {
            guide.Update(model);
        }
    }

    private LabStatus Status() =>
        new()
        {
            Foreground = (_host.Measurements.Foreground ?? _host.Watcher.Current).Describe(),
            SinceStep = _host.Measurements.Read() - _stepBaseline,
            TotalViolations = _host.Guard.Violations,
            LastCommand = _host.Measurements.LastCommand,
            LastLatencyMs = _host.Measurements.LastLatencyMs,
            LastLease = _host.Measurements.LastLease,
            LastRestore = _host.Measurements.LastRestore,
            ProbeOpen = _host.Probe.IsOpen,
            Notice = _host.Measurements.LastNotice,
            PiecesNotReady = _host.Board.Components.Count(component =>
                component.State != LabComponentState.Ready
            ),
            SendsKeys = SendsKeys,
            VoiceNumbers = VoiceNumbers,
        };

    private ReportContext Context() =>
        new(
            // Monitors come and go (a second one for S1 row 30): the report shows the ones there now.
            _machine with
            {
                Monitors = HardwareProbe.Monitors(),
            },
            _host.Time.GetUtcNow(),
            _host.Board.Components,
            _host.Log.Snapshot()
        )
        {
            DroppedEvents = _host.Log.Dropped,
            SendsKeys = SendsKeys,
            VoiceNumbers = VoiceNumbers,
        };

    private void WriteReport() =>
        _ = SaveReportAsync()
            .ContinueWith(
                task =>
                    _host.Measurements.Notice(
                        "No se pudo guardar el informe: "
                            + task.Exception?.GetBaseException().Message
                    ),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default
            );

    private static string How(TileInput input) =>
        input.IsCommand
            ? input.Pattern.ToString()
            : input.Pointer switch
            {
                PointerKind.Finger => "toque (dedo)",
                PointerKind.Pen => "toque (lápiz)",
                PointerKind.Mouse => "toque (mouse)",
                _ => "toque",
            };
}
