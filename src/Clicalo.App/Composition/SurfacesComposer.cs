using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Interaction;
using Clicalo.Application.Localization;
using Clicalo.Application.Profiles;
using Clicalo.Application.Session;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Timing;
using Clicalo.Presentation.Bubble;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Search;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.App.Composition;

/// <summary>
/// Wires the forms of the panel (docs/04 «Vista compacta», «Vista pestaña», «Burbuja minimizada», «Opacidad y
/// atenuado», «Posición») on the UI thread of the Surfaces role: the bubble, the handle and the bar of the Tab view with
/// the windows beside it, the floating «Release all», the dimming of every surface and the positions. It owns no rule:
/// the form is <see cref="PanelForms"/>', the collapse and the guide <see cref="DockRules"/>', the places
/// <see cref="PanelGeometry"/>' and <see cref="DockGeometry"/>', the opacity <see cref="DimPolicy"/>'s through the
/// <see cref="InteractionStore"/>. It implements what the handle and the bar ask for (<see cref="IDockIntents"/>) by
/// sending it to the interaction store, the session, the document or the panel's own intentions.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The surface set disposes itself when the panel window closes; the collapse timer is disposed when it fires or is cancelled and only queues work to the dispatcher."
)]
internal sealed class SurfacesComposer : IDockIntents
{
    private readonly DocumentStore _store;
    private readonly SessionStore _session;
    private readonly InteractionStore _interaction;
    private readonly ProfileViewCoordinator _profiles;
    private readonly PanelComposer _panel;
    private readonly PanelInteractionController _controller;
    private readonly EngineObserverRelay _relay;
    private readonly TimeProvider _time;
    private readonly Dispatcher _ui;
    private SurfaceSet? _surfaces;
    private ITimer? _collapseTimer;
    private bool _refreshQueued;

    /// <summary>Creates the view models of the bubble and the Tab view on the UI thread.</summary>
    /// <param name="store">The document: the view, the Tab settings, the positions.</param>
    /// <param name="session">The session: presence and the profile grid.</param>
    /// <param name="interaction">The interaction state: bubble, bar, side windows, guide, dimming.</param>
    /// <param name="profiles">Which profile is in view.</param>
    /// <param name="panel">The full panel and its intentions.</param>
    /// <param name="controller">Where the shortcuts of the bar go.</param>
    /// <param name="relay">The engine's snapshots and usage.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="time">The clock of the collapse.</param>
    /// <param name="ui">The dispatcher of the UI thread.</param>
    public SurfacesComposer(
        DocumentStore store,
        SessionStore session,
        InteractionStore interaction,
        ProfileViewCoordinator profiles,
        PanelComposer panel,
        PanelInteractionController controller,
        EngineObserverRelay relay,
        ILocalizationContext localization,
        TimeProvider time,
        Dispatcher ui
    )
    {
        _store = store;
        _session = session;
        _interaction = interaction;
        _profiles = profiles;
        _panel = panel;
        _controller = controller;
        _relay = relay;
        _time = time;
        _ui = ui;
        Dock = new DockBarViewModel(controller, localization, this);
        Bubble = new BubbleViewModel(
            localization,
            () => _ = _interaction.Dispatch(new InteractionAction.Restore()),
            ReleaseAll
        );

        _store.Changed += (_, change) => _ = _ui.BeginInvoke(() => OnDocumentChanged(change));
        _session.Changed += OnSessionChanged;
        _interaction.Changed += (_, change) =>
        {
            // The finger and the pointer only change the opacity, which the surfaces follow by themselves.
            var before = change.Previous;
            var after = change.Current;
            if (
                before.Minimized != after.Minimized
                || before.DockOpen != after.DockOpen
                || before.Flyout != after.Flyout
                || before.CoachStep != after.CoachStep
            )
            {
                Invalidate();
            }
        };
        _profiles.Changed += (_, change) =>
        {
            // SEL-004: another profile in view starts the bar on its first page too.
            if (change.Previous.View != change.Current.View)
            {
                Dock.FirstPage();
            }
        };
        _panel.Refreshed += (_, _) => Invalidate();
        _panel.Search.PropertyChanged += OnSearchChanged;
        _relay.SnapshotChanged += (_, change) =>
        {
            Dock.ApplyEngine(change.Snapshot);
            Bubble.ApplyEngine(change.Snapshot);
            Invalidate();
        };
        _relay.UsageCounted += (_, counted) => OnUsage(counted.Shortcut);
        localization.LanguageChanged += (_, _) =>
            _ = _ui.BeginInvoke(() =>
            {
                Dock.Relocalize();
                Bubble.Relocalize();
            });
    }

    /// <summary>The handle and the bar of the Tab view.</summary>
    public DockBarViewModel Dock { get; }

    /// <summary>The bubble.</summary>
    public BubbleViewModel Bubble { get; }

    /// <summary>The surfaces, once attached.</summary>
    public SurfaceSet? Surfaces => _surfaces;

    /// <summary>Creates every surface around the panel window and shows the form of the panel.</summary>
    /// <param name="window">The panel window.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="theme">The theme service of the UI thread.</param>
    public void Attach(PanelWindow window, SurfaceRegistry registry, ThemeService theme)
    {
        ArgumentNullException.ThrowIfNull(window);
        var settings = _store.Current.Settings;
        _surfaces = new SurfaceSet(
            window,
            _panel.Panel,
            Dock,
            Bubble,
            new SurfaceDimming(_interaction, SettingsProjection.Dim(settings)),
            registry,
            theme,
            _time,
            SettingsProjection.Touch(settings),
            new SurfaceCallbacks(
                ReleaseAll,
                (contact, summary, reason) => _ = _controller.HoldEnded(contact, summary, reason),
                position => _ = _store.Dispatch(new SetPanelPosition(position)),
                SaveHandle,
                OnTileUsed,
                OnHoldReleased
            )
        );
        Refresh();
    }

    /// <inheritdoc />
    public void OpenBar() => _ = _interaction.Dispatch(new InteractionAction.OpenDock());

    /// <inheritdoc />
    public void MoveHandle(int percent) => SaveHandle(_store.Current.Settings.Dock.Side, percent);

    /// <inheritdoc />
    public void CloseBar()
    {
        CancelCollapse();
        _ = _interaction.Dispatch(new InteractionAction.CloseDock());
        _ = _session.Dispatch(new SessionAction.ClosePicker());
    }

    /// <inheritdoc />
    public void Expand() => ChangeView(PanelDensity.Full);

    /// <inheritdoc />
    public void ShowFrequents()
    {
        _panel.ShowFrequents();
        _ = _interaction.Dispatch(new InteractionAction.CloseFlyout());
    }

    /// <inheritdoc />
    public void ProfileButton()
    {
        if (
            SelectorRules.Tap(
                _profiles.State.View is Clicalo.Domain.ProfileResolution.ViewTarget.Frequents
            ) == SelectorTap.ReturnFromFrequents
        )
        {
            _panel.ReturnFromFrequents();
            return;
        }

        _ = _interaction.Dispatch(new InteractionAction.ToggleFlyout(DockFlyout.Profiles));
        SyncPicker();
    }

    /// <inheritdoc />
    public void ToggleLock() => _panel.Header.ToggleLock();

    /// <inheritdoc />
    public void Search()
    {
        ChangeView(PanelDensity.Full);

        // PAN-001 d: the Full view with an empty search, once the panel is on screen to take the keyboard.
        _ = _ui.BeginInvoke(
            DispatcherPriority.Background,
            () =>
            {
                if (!_panel.Search.IsOpen)
                {
                    _ = _panel.Search.OpenAsync(SearchTrigger.Touch);
                }
            }
        );
    }

    /// <inheritdoc />
    public void Repeat() => _panel.Repeat();

    /// <inheritdoc />
    public void TogglePinned()
    {
        _ = _interaction.Dispatch(new InteractionAction.ToggleFlyout(DockFlyout.Pinned));
        SyncPicker();
    }

    /// <inheritdoc />
    public void ToggleSticky()
    {
        _ = _interaction.Dispatch(new InteractionAction.ToggleFlyout(DockFlyout.Sticky));
        SyncPicker();
    }

    /// <inheritdoc />
    public void TogglePinOpen()
    {
        CancelCollapse();
        _ = _store.Dispatch(
            new SetSetting(SettingPaths.DockPinOpen, !_store.Current.Settings.Dock.PinOpen)
        );
    }

    /// <inheritdoc />
    public void CoachNext()
    {
        if (DockRules.NextCoachStep(_interaction.Current.CoachStep) is null)
        {
            CoachSkip();
            return;
        }

        _ = _interaction.Dispatch(new InteractionAction.CoachNext());
    }

    /// <inheritdoc />
    public void CoachSkip()
    {
        _ = _interaction.Dispatch(new InteractionAction.CoachReset());
        _ = _store.Dispatch(new SetSetting(SettingPaths.DockCoachDone, true));
    }

    /// <inheritdoc />
    public void ReleaseAll() => _ = _controller.ReleaseAll();

    private void ChangeView(PanelDensity density)
    {
        CancelCollapse();
        _ = _interaction.Dispatch(new InteractionAction.ViewChanged());
        _ = _session.Dispatch(new SessionAction.ClosePicker());
        _ = _store.Dispatch(new SetSetting(SettingPaths.Density, density));
    }

    private void SaveHandle(DockSide side, int percent)
    {
        var path = side switch
        {
            DockSide.Left => SettingPaths.DockHandleLeft,
            DockSide.Top => SettingPaths.DockHandleTop,
            DockSide.Bottom => SettingPaths.DockHandleBottom,
            _ => SettingPaths.DockHandleRight,
        };
        _ = _store.Dispatch(new SetSetting(path, DockGeometry.ClampPercent(percent)));
    }

    /// <summary>
    /// The profile grid beside the bar is the session's profile grid (SEL-002, PES-011): its window opens and closes
    /// with it, and the other side windows close it.
    /// </summary>
    private void SyncPicker()
    {
        var wanted = _interaction.Current.Flyout == DockFlyout.Profiles;
        if (_session.Current.PickerOpen != wanted)
        {
            _ = _session.Dispatch(
                wanted ? new SessionAction.TogglePicker() : new SessionAction.ClosePicker()
            );
        }
    }

    private void OnSessionChanged(object? sender, SessionChangedEventArgs change)
    {
        // PAN-001 e: the tray shows the panel out of the bubble.
        if (
            change.Previous.Presence == PanelPresence.Hidden
            && change.Current.Presence == PanelPresence.Visible
        )
        {
            _ = _interaction.Dispatch(new InteractionAction.Restore());
        }

        // SEL-004: choosing a profile closes the grid, and its window beside the bar with it.
        if (!change.Current.PickerOpen && _interaction.Current.Flyout == DockFlyout.Profiles)
        {
            _ = _interaction.Dispatch(new InteractionAction.CloseFlyout());
        }

        Invalidate();
    }

    private void OnDocumentChanged(DocumentChangedEventArgs change)
    {
        var before = change.Before.Settings;
        var after = change.After.Settings;
        if (ReferenceEquals(before, after))
        {
            return;
        }

        if (before.Density != after.Density)
        {
            // PAN-001 c: another view leaves the bubble, folds the bar and starts on page 1.
            _ = _interaction.Dispatch(new InteractionAction.ViewChanged());
            Dock.FirstPage();
        }

        if (!before.Touch.Equals(after.Touch))
        {
            _surfaces?.ApplyTouch(SettingsProjection.Touch(after));
        }

        Invalidate();
    }

    private void OnSearchChanged(object? sender, PropertyChangedEventArgs change)
    {
        if (
            string.Equals(
                change.PropertyName,
                nameof(SearchViewModel.IsOpen),
                StringComparison.Ordinal
            )
        )
        {
            Invalidate();
        }
    }

    /// <summary>A shortcut ran (FRE-002): the open bar collapses after it, as PES-012 says.</summary>
    private void OnUsage(Clicalo.Domain.Primitives.ShortcutId shortcut)
    {
        if (CurrentForm() != PanelForm.DockOpen)
        {
            return;
        }

        var behavior = _store.Current.Library.TryGetShortcut(shortcut, out var found)
            ? PanelProjector.BehaviorOf(found.Action)
            : TileBehavior.Hold;
        var use = behavior switch
        {
            TileBehavior.Toggle => DockUse.ToggleChanged,
            TileBehavior.Hold => (DockUse?)null,
            _ => DockUse.Ran,
        };
        if (use is { } ran)
        {
            ScheduleCollapse(ran);
        }
    }

    private void OnTileUsed(DockTileViewModel tile, bool inSideWindow)
    {
        // PES-010: «Pinned» closes after one of its shortcuts is used, unless it holds or latches.
        if (
            inSideWindow
            && DockRules.SideWindowClosesAfterUse(
                tile.Behavior is TileBehavior.Hold or TileBehavior.Toggle
            )
        )
        {
            _ = _interaction.Dispatch(new InteractionAction.CloseFlyout());
        }
    }

    private void OnHoldReleased()
    {
        if (CurrentForm() == PanelForm.DockOpen)
        {
            ScheduleCollapse(DockUse.HoldReleased);
        }
    }

    private void ScheduleCollapse(DockUse use)
    {
        if (!DockRules.CollapsesAfter(use, _store.Current.Settings.Dock.PinOpen))
        {
            return;
        }

        CancelCollapse();
        _collapseTimer = _time.CreateTimer(
            static state => ((SurfacesComposer)state!).QueueCollapse(),
            this,
            Timings.Dock.CollapseDelay,
            Timeout.InfiniteTimeSpan
        );
    }

    private void QueueCollapse() =>
        _ = _ui.BeginInvoke(() =>
        {
            _collapseTimer?.Dispose();
            _collapseTimer = null;
            if (CurrentForm() == PanelForm.DockOpen && !_store.Current.Settings.Dock.PinOpen)
            {
                CloseBar();
            }
        });

    private void CancelCollapse()
    {
        _collapseTimer?.Dispose();
        _collapseTimer = null;
    }

    private PanelForm CurrentForm()
    {
        var interaction = _interaction.Current;
        return PanelForms.Of(
            _session.Current.Presence == PanelPresence.Visible,
            _store.Current.Settings.Density,
            interaction.Minimized,
            interaction.DockOpen
        );
    }

    private void Invalidate()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        _ = _ui.BeginInvoke(DispatcherPriority.Normal, Refresh);
    }

    /// <summary>Projects the form, the bar and what keeps the surfaces awake, and shows them.</summary>
    private void Refresh()
    {
        _refreshQueued = false;
        var settings = _store.Current.Settings;
        var panel = _panel.Panel;
        var held = !panel.Engine.Held.IsEmpty;

        // docs/04: panic, the search and the profile grid keep every surface awake.
        _ = _interaction.Dispatch(new InteractionAction.SetOpen(DimExceptions.Panic, held));
        _ = _interaction.Dispatch(
            new InteractionAction.SetOpen(DimExceptions.Search, _panel.Search.IsOpen)
        );
        _ = _interaction.Dispatch(
            new InteractionAction.SetOpen(DimExceptions.ProfileGrid, _session.Current.PickerOpen)
        );

        // docs/04: Quick settings, the tile menu and edit mode keep the surfaces awake too.
        _ = _interaction.Dispatch(
            new InteractionAction.SetOpen(DimExceptions.QuickSettings, _panel.QuickSettings.IsOpen)
        );
        _ = _interaction.Dispatch(
            new InteractionAction.SetOpen(DimExceptions.ContextMenu, _panel.Menu.IsOpen)
        );
        _ = _interaction.Dispatch(
            new InteractionAction.SetOpen(DimExceptions.EditMode, _panel.EditMode.IsOn)
        );

        var interaction = _interaction.Current;
        var form = CurrentForm();
        var model = _panel.LastModel;
        Dock.ApplyTiles(model.Tiles, settings.ShowAlwaysVisibleRow ? model.StripTiles : []);
        var selector = panel.Selector;
        Dock.Apply(
            new DockBarState(
                settings.Dock,
                SettingsProjection.Size(settings),
                settings.ShowAlwaysVisibleRow,
                settings.StickyModifiersRow,
                settings.VoiceNumbers,
                selector.IsFrequentsActive,
                selector.ProfileName,
                selector.ProfileIcon,
                selector.IsActiveApp,
                _panel.Header.IsFixed,
                _panel.CanRepeat,
                interaction.Flyout,
                interaction.CoachStep,
                form == PanelForm.DockOpen,
                held
            )
        );
        _surfaces?.Apply(
            new SurfaceLayout(
                form,
                settings.Dock,
                settings.PanelPositions,
                SettingsProjection.Size(settings),
                held,
                form == PanelForm.DockOpen ? interaction.Flyout : DockFlyout.None,
                form == PanelForm.DockOpen && Dock.ShowsCoach
            )
        );
    }
}
