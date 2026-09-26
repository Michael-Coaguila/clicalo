using System.Collections.Immutable;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Platform.Windows.Tray;
using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Surfaces;
using Clicalo.Tools.SpikeLab.Views;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Tools.SpikeLab.Session;

/// <summary>
/// The foreground flows of S4 with the real <see cref="IForegroundOrchestrator"/>: the search under a
/// <c>TextInput</c> lease (opened by touch, UI Automation or the global shortcut), the lab Control Center under a
/// <c>ControlCenter</c> lease, and the tray menu under a <c>TrayMenu</c> lease. Every cycle measures the grant, the
/// ladder step, the times and the verified restoration, and ends with one repetition candidate. In M1 no action is
/// sent after the search: the flow only gives the foreground back and verifies it (S4.md).
/// </summary>
internal sealed class LeaseFlows
{
    private const int TrayControlCenter = 1;
    private const int TrayReleaseAll = 2;
    private const int TrayExit = 3;
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>
    /// The items of the tray menu (S4 rows 10 and 11). Their names differ from every other name of SpikeLab, so a voice
    /// command never has to choose between the menu and a tile.
    /// </summary>
    public static ImmutableArray<TrayMenuItem> TrayMenuItems { get; } =
    [
        new TrayMenuItem(TrayControlCenter, "Abrir el Centro de control"),
        new TrayMenuItem(TrayReleaseAll, "Soltar todas las teclas"),
        new TrayMenuItem(TrayExit, "Salir de SpikeLab"),
    ];

    private readonly LabHost _host;
    private readonly LabSurfaces _surfaces;
    private readonly Func<bool> _enabled;
    private readonly Action _cycleStarting;
    private readonly Action<RepetitionEvidence> _cycleDone;
    private readonly Action<string, AnnouncementUrgency> _announce;
    private readonly Action _releaseAll;
    private readonly Action _exit;
    private ForegroundLease? _searchLease;
    private LeaseEvidence? _searchEvidence;
    private LeaseOrigin _searchOrigin;
    private LabControlCenter? _controlCenter;
    private ForegroundLease? _controlCenterLease;
    private LeaseEvidence? _controlCenterEvidence;
    private bool _busy;

    /// <summary>Creates the flows.</summary>
    /// <param name="host">The real pieces.</param>
    /// <param name="surfaces">The lab surfaces.</param>
    /// <param name="enabled">True while leases may be requested (S4); false in S1 and S3, where they would break the cycles.</param>
    /// <param name="cycleStarting">Called before a lease is requested, to start its repetition window.</param>
    /// <param name="cycleDone">Receives the evidence of every lease cycle.</param>
    /// <param name="announce">Speaks a notice (a denial) through the live region.</param>
    /// <param name="releaseAll">«Soltar todo» from the tray menu.</param>
    /// <param name="exit">«Salir» from the tray menu.</param>
    public LeaseFlows(
        LabHost host,
        LabSurfaces surfaces,
        Func<bool> enabled,
        Action cycleStarting,
        Action<RepetitionEvidence> cycleDone,
        Action<string, AnnouncementUrgency> announce,
        Action releaseAll,
        Action exit
    )
    {
        _host = host;
        _surfaces = surfaces;
        _enabled = enabled;
        _cycleStarting = cycleStarting;
        _cycleDone = cycleDone;
        _announce = announce;
        _releaseAll = releaseAll;
        _exit = exit;
        if (host.TouchKeyboard is { } keyboard)
        {
            keyboard.OccludedAreaChanged += (_, _) => host.Post(KeepPanelAboveKeyboard);
        }
    }

    /// <summary>True while the search lease is active.</summary>
    public bool SearchOpen => _searchLease is not null;

    /// <summary>Opens the search under a <c>TextInput</c> lease with <paramref name="origin"/>.</summary>
    public async Task OpenSearchAsync(LeaseOrigin origin)
    {
        if (!Allowed("La búsqueda con concesión se prueba en S4."))
        {
            return;
        }

        if (_searchLease is not null)
        {
            _surfaces.Search.FocusField();
            return;
        }

        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            _cycleStarting();
            var search = _surfaces.Search;
            search.Clear();
            if (!search.TryShow())
            {
                Done(
                    Unavailable(
                        LeaseKind.TextInput,
                        origin,
                        "La superficie de búsqueda no se pudo mostrar."
                    ),
                    1,
                    0
                );
                return;
            }

            var (lease, evidence) = await AcquireAsync(
                LeaseKind.TextInput,
                origin,
                () => search.SurfaceWindow
            );
            if (lease is null)
            {
                search.TryHide();
                Denied(evidence, "No pude abrir la búsqueda");
                Done(evidence, 1, 0);
                return;
            }

            if (!search.IsShown)
            {
                // «Cerrar búsqueda» arrived while the lease was being requested: give the foreground back at once.
                Done(await RestoreAsync(lease, evidence), 1, 0, origin);
                return;
            }

            _searchLease = lease;
            _searchEvidence = evidence;
            _searchOrigin = origin;
            search.FocusField();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// Ends the search: gives the foreground back (verified), hides the search and records the cycle. With
    /// <paramref name="result"/> the maintainer chose «Resultado Negrita»; M1 sends no action either way. While the
    /// field is still empty, «Resultado Negrita» keeps the search open and says why: Wispr Flow and Typeless paste a
    /// moment after the dictation ends, and a paste that arrives after the foreground went back would land in the app
    /// under test. «Cerrar búsqueda» always closes.
    /// </summary>
    public async Task CloseSearchAsync(bool result)
    {
        if (_searchLease is not { } lease || _searchEvidence is not { } evidence)
        {
            _surfaces.Search.TryHide();
            return;
        }

        if (result && _surfaces.Search.FieldLength == 0)
        {
            _host.Measurements.Notice(
                "El campo de búsqueda sigue vacío: espera a que aparezca el texto y vuelve a tocar «Resultado "
                    + "Negrita». Si no llega, toca «Cerrar búsqueda»: el ciclo cuenta como fallido."
            );
            return;
        }

        _searchLease = null;
        _searchEvidence = null;
        var search = _surfaces.Search;
        var length = search.FieldLength;
        var restored = await RestoreAsync(lease, evidence);
        search.TryHide();
        search.Clear();
        await HideKeyboardAsync();
        _host.Log.Add(
            "search",
            (result ? "Resultado elegido" : "Búsqueda cerrada")
                + string.Create(
                    Spanish,
                    $" con {length} caracteres en el campo (el texto no se guarda)."
                )
        );
        Done(restored, 1, length > 0 ? 1 : 0, _searchOrigin);
    }

    /// <summary>«Dictar»: focuses the search field and starts Windows dictation (Win+H) through the guarded effects.</summary>
    public async Task DictateAsync()
    {
        if (_searchLease is null)
        {
            _host.Measurements.Notice(
                "Abre primero la búsqueda: el dictado va al campo con el foco."
            );
            return;
        }

        _surfaces.Search.FocusField();
        await StartDictationAsync();
    }

    /// <summary>The search field was tapped: focus it and show the touch keyboard.</summary>
    public async Task FocusSearchFieldAsync()
    {
        if (_searchLease is null)
        {
            return;
        }

        _surfaces.Search.FocusField();
        if (_host.TouchKeyboard is { } keyboard)
        {
            try
            {
                await keyboard.ShowKeyboardAsync(
                    _surfaces.Search.SurfaceWindow,
                    CancellationToken.None
                );
            }
            catch (Exception ex) when (ComponentBoard.IsContained(ex))
            {
                _host.Board.Fail("TouchKeyboard", ex);
            }
        }
    }

    /// <summary>Opens the lab Control Center under a <c>ControlCenter</c> lease with <paramref name="origin"/>.</summary>
    public async Task OpenControlCenterAsync(LeaseOrigin origin)
    {
        if (
            !Allowed("El Centro de control con concesión se prueba en S4.")
            || _controlCenter is not null
            || _busy
        )
        {
            return;
        }

        _busy = true;
        try
        {
            _cycleStarting();
            var window = new LabControlCenter(CloseControlCenterAsync, Dictate, ShowKeyboardFor);
            window.Show();
            var token = new WindowToken(new WindowInteropHelper(window).Handle);
            PointerSetup.DisableTouchFeedback(token);
            var (lease, evidence) = await AcquireAsync(
                LeaseKind.ControlCenter,
                origin,
                () => token
            );
            if (lease is null)
            {
                window.CloseNow();
                Denied(evidence, "No pude abrir el Centro de control");
                Done(evidence, window.FieldCount, 0);
                return;
            }

            if (window.IsClosed)
            {
                // Closed (for example with Alt+F4 or by voice) while the lease was being requested: give the
                // foreground back at once instead of keeping a lease on a window that no longer exists.
                Done(await RestoreAsync(lease, evidence), window.FieldCount, 0);
                return;
            }

            _controlCenter = window;
            _controlCenterLease = lease;
            _controlCenterEvidence = evidence;
            window.FocusFirstField();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Opens the tray menu under a <c>TrayMenu</c> lease at <paramref name="anchor"/>.</summary>
    public async Task ShowTrayMenuAsync(PhysicalPoint anchor)
    {
        if (_host.TrayMenu is not { } menu || _busy)
        {
            return;
        }

        _busy = true;
        int? choice = null;
        try
        {
            _cycleStarting();
            var (lease, evidence) = await AcquireAsync(
                LeaseKind.TrayMenu,
                LeaseOrigin.Tray,
                () => menu.Window
            );
            if (lease is null)
            {
                Denied(evidence, "No pude abrir el menú de la bandeja");
                Done(evidence, 0, 0);
                return;
            }

            try
            {
                choice = await menu.ShowMenuAsync(TrayMenuItems, anchor);
            }
            catch (Exception ex) when (ComponentBoard.IsContained(ex))
            {
                _host.Board.Fail("TrayMenuHost", ex);
            }

            Done(await RestoreAsync(lease, evidence), 0, 0);
        }
        finally
        {
            _busy = false;
        }

        switch (choice)
        {
            case TrayControlCenter:
                await OpenControlCenterAsync(LeaseOrigin.Tray);
                break;
            case TrayReleaseAll:
                _releaseAll();
                break;
            case TrayExit:
                _exit();
                break;
        }
    }

    /// <summary>Closes the Control Center and the search without recording cycles (shutting down).</summary>
    public async Task CloseAllAsync()
    {
        if (_searchLease is not null)
        {
            await CloseSearchAsync(result: false);
        }

        if (_controlCenter is not null)
        {
            await CloseControlCenterAsync(_controlCenter);
        }
    }

    private async Task CloseControlCenterAsync(LabControlCenter window)
    {
        if (!ReferenceEquals(window, _controlCenter) || _controlCenterLease is not { } lease)
        {
            window.CloseNow();
            return;
        }

        var evidence = _controlCenterEvidence!;
        var withText = window.FieldsWithText;
        _controlCenter = null;
        _controlCenterLease = null;
        _controlCenterEvidence = null;

        // Back to the app first, then close: Windows never activates another window of SpikeLab in between.
        var restored = await RestoreAsync(lease, evidence);
        await HideKeyboardAsync();
        window.CloseNow();
        Done(restored, window.FieldCount, withText);
    }

    private async Task<(ForegroundLease? Lease, LeaseEvidence Evidence)> AcquireAsync(
        LeaseKind kind,
        LeaseOrigin origin,
        Func<WindowToken> target
    )
    {
        var before = _host.Watcher.Current;
        var evidence = new LeaseEvidence(kind, origin)
        {
            PreviousProcess = before.Describe(),
            PreviousWasProbe = before.IsProbe,
        };
        if (_host.Orchestrator is not { } orchestrator)
        {
            return (
                null,
                evidence with
                {
                    Unavailable = "ForegroundOrchestrator no arrancó (ver piezas).",
                }
            );
        }

        var chords = _host.Measurements.Read().RightsChords;
        var started = _host.Time.GetTimestamp();
        try
        {
            var result = await orchestrator.AcquireAsync(
                new LeaseRequest(kind, target(), origin, IdleTimeout: null),
                CancellationToken.None
            );
            evidence = evidence with
            {
                AcquireMs = _host.Time.GetElapsedTime(started).TotalMilliseconds,
                LadderStep = _host.Measurements.Read().RightsChords > chords ? 2 : 1,
            };
            switch (result)
            {
                case LeaseResult.Granted granted:
                    evidence = evidence with { Granted = true };
                    _host.Measurements.OnLease(Describe(evidence), restore: null);
                    return (granted.Lease, evidence);
                case LeaseResult.Denied denied:
                    evidence = evidence with { Denial = denied.Reason };
                    _host.Measurements.OnLease(Describe(evidence), restore: null);
                    return (null, evidence);
                default:
                    return (
                        null,
                        evidence with
                        {
                            Unavailable = "Respuesta desconocida del orquestador.",
                        }
                    );
            }
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            _host.Board.Fail("ForegroundOrchestrator", ex);
            return (null, evidence with { Unavailable = ex.GetType().Name + ": " + ex.Message });
        }
    }

    private async Task<LeaseEvidence> RestoreAsync(ForegroundLease lease, LeaseEvidence evidence)
    {
        var started = _host.Time.GetTimestamp();
        RestoreOutcome? outcome = null;
        try
        {
            outcome = await lease.RestoreAsync(CancellationToken.None);
            await lease.DisposeAsync();
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            _host.Board.Fail("ForegroundLease", ex);
        }

        var returned =
            outcome is RestoreOutcome.Restored or RestoreOutcome.RestoredAfterRetry
            && _host.Watcher.Current.Window == lease.PreviousForeground.Handle;
        var restored = evidence with
        {
            Restore = outcome,
            RestoreMs = _host.Time.GetElapsedTime(started).TotalMilliseconds,
            ForegroundReturned = returned,
        };
        _host.Measurements.OnLease(Describe(restored), Describe(outcome));
        return restored;
    }

    private void Done(LeaseEvidence lease, int fields, int withText, LeaseOrigin? origin = null) =>
        _cycleDone(
            new RepetitionEvidence
            {
                Trigger = new TriggerInfo(StepTrigger.LeaseCycle)
                {
                    Channel = Channel(origin ?? lease.Origin),
                },
                Lease = lease,
                FieldCount = fields,
                FieldsWithText = withText,
            }
        );

    private void Denied(LeaseEvidence evidence, string what)
    {
        var why = evidence.Unavailable ?? evidence.Denial?.ToString() ?? "sin motivo";
        _announce(what + ": " + why + ".", AnnouncementUrgency.Assertive);
    }

    private bool Allowed(string otherwise)
    {
        if (_enabled())
        {
            return true;
        }

        _host.Measurements.Notice(otherwise);
        return false;
    }

    private async Task StartDictationAsync()
    {
        try
        {
            if (_host.TouchKeyboard is { } keyboard)
            {
                await keyboard.StartDictationAsync(CancellationToken.None);
            }
            else
            {
                await _host.KeyEffects.SendDictationChordAsync(CancellationToken.None);
            }
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            _host.Board.Fail("TouchKeyboard", ex);
        }
    }

    private async Task HideKeyboardAsync()
    {
        if (_host.TouchKeyboard is not { } keyboard)
        {
            return;
        }

        try
        {
            await keyboard.HideKeyboardAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            _host.Board.Fail("TouchKeyboard", ex);
        }
    }

    private void Dictate(TextBox field)
    {
        Keyboard.Focus(field);
        _ = StartDictationAsync();
    }

    private void ShowKeyboardFor(TextBox field)
    {
        if (_host.TouchKeyboard is not { } keyboard || _controlCenter is not { } window)
        {
            return;
        }

        _ = ShowAsync();

        async Task ShowAsync()
        {
            try
            {
                await keyboard.ShowKeyboardAsync(
                    new WindowToken(new WindowInteropHelper(window).Handle),
                    CancellationToken.None
                );
            }
            catch (Exception ex) when (ComponentBoard.IsContained(ex))
            {
                _host.Board.Fail("TouchKeyboard", ex);
            }
        }
    }

    private void KeepPanelAboveKeyboard()
    {
        if (_host.TouchKeyboard is not { } keyboard || !_surfaces.Panel.IsShown)
        {
            return;
        }

        PhysicalRect occluded;
        try
        {
            occluded = keyboard.OccludedArea;
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            _host.Board.Fail("TouchKeyboard", ex);
            return;
        }

        var panel = _surfaces.Panel.Bounds();
        if (
            occluded.IsEmpty
            || panel.IsEmpty
            || panel.Bottom <= occluded.Top
            || panel.Top >= occluded.Bottom
        )
        {
            return;
        }

        if (
            _surfaces.Panel.TryMove(
                panel with
                {
                    Top = Math.Max(0, occluded.Top - panel.Height - 8),
                }
            )
        )
        {
            _host.Log.Add(
                "keyboard",
                "El teclado táctil tapaba el panel: el panel subió (EC-BUS-01)."
            );
            _host.Measurements.Notice("El teclado táctil tapaba el panel: el panel subió.");
        }
    }

    private static LeaseEvidence Unavailable(LeaseKind kind, LeaseOrigin origin, string why) =>
        new(kind, origin) { Unavailable = why };

    private static string Channel(LeaseOrigin origin) =>
        origin switch
        {
            LeaseOrigin.Touch => "pointer",
            LeaseOrigin.UiaInvoke => "uia",
            LeaseOrigin.GlobalHotkey => "hotkey",
            LeaseOrigin.Tray => "tray",
            _ => "lab",
        };

    private static string Describe(LeaseEvidence evidence) =>
        evidence.Kind
        + (
            evidence.Granted
                ? string.Create(
                    Spanish,
                    $" concedida (paso {evidence.LadderStep}, {evidence.AcquireMs:0} ms)"
                )
            : evidence.Unavailable is { } why ? " no disponible: " + why
            : " denegada (" + evidence.Denial + ")"
        )
        + " · origen "
        + evidence.Origin;

    private static string Describe(RestoreOutcome? outcome) =>
        outcome switch
        {
            RestoreOutcome.Restored => "Restaurado",
            RestoreOutcome.RestoredAfterRetry => "Restaurado al reintentar",
            RestoreOutcome.Flashed => "Parpadeo en la barra de tareas",
            RestoreOutcome.Failed => "Falló",
            _ => "no se pudo pedir",
        };
}
