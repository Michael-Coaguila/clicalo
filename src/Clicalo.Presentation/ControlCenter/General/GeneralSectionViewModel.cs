using System.Globalization;
using Clicalo.Application.Confirmation;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using ResetFrequentsCommand = Clicalo.Domain.Commands.ResetFrequents;

namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>
/// «General y panel» (docs/05 §3, GEN-001 to GEN-014): projects the settings of the document into
/// <see cref="Screen"/> and turns every tap into a <see cref="SetSetting"/>. Everything saves by itself and applies at
/// once, because whoever follows the settings (theme, panel, engine) reacts to the document; behavior settings enter
/// the undo history and the status bar offers [undo] (DAT-006), presentation and placement settings are reverted with
/// the same control. Lives on the UI thread of the Workspace role.
/// </summary>
public sealed class GeneralSectionViewModel : ObservableObject
{
    private const string PercentSign = "%";
    private const string SavedIcon = "check";
    private const string ResetIcon = "restart_alt";
    private const string CoachIcon = "help";

    /// <summary>The keys on the miniature tile of «Mostrar teclas» (key names are not translated).</summary>
    private const string SampleKeys = "Ctrl + C";

    private static readonly ConfirmationSubject ResetSubject = new(
        nameof(Domain.Commands.ResetFrequents),
        "frequents"
    );

    private readonly GeneralServices _s;
    private GeneralScreen _screen;
    private bool _resetArmed;
    private ITimer? _disarm;

    /// <summary>Creates the section; it follows the document and the language by itself.</summary>
    /// <param name="services">What it works with.</param>
    public GeneralSectionViewModel(GeneralServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _s = services;
        _screen = Project();
        services.Store.Changed += (_, _) => services.Post(Refresh);
        services.Localization.LanguageChanged += (_, _) => services.Post(Refresh);
    }

    /// <summary>A message for the status bar of the Control Center (CCM-003).</summary>
    public event EventHandler<WorkspaceNoticeEventArgs>? Noticed;

    /// <summary>What the section shows.</summary>
    public GeneralScreen Screen
    {
        get => _screen;
        private set => SetProperty(ref _screen, value);
    }

    private UserSettings Settings => _s.Store.Current.Settings;

    /// <summary>Projects the section again (settings, language, armed state).</summary>
    public void Refresh() => Screen = Project();

    /// <summary>A language button (GEN-002): the language changes at once in every window (IDI-001).</summary>
    /// <param name="code">The language code.</param>
    public void SetLanguage(string code)
    {
        if (!string.Equals(code, Settings.Language.Value, StringComparison.Ordinal))
        {
            Write(SettingPaths.Language, new LangCode(code));
        }
    }

    /// <summary>A theme card (GEN-003).</summary>
    /// <param name="theme">The theme.</param>
    public void SetTheme(ThemeChoice theme) => Write(SettingPaths.Theme, theme);

    /// <summary>A size card (GEN-004).</summary>
    /// <param name="size">The size.</param>
    public void SetSize(PanelSize size) => Write(SettingPaths.Size, size);

    /// <summary>− of the text size: 10 % less, never below 100 % (GEN-004).</summary>
    public void TextSmaller() => WriteTextScale(-1);

    /// <summary>+ of the text size: 10 % more, never above 150 % (GEN-004).</summary>
    public void TextBigger() => WriteTextScale(1);

    /// <summary>A view card (GEN-005).</summary>
    /// <param name="density">The view.</param>
    public void SetDensity(PanelDensity density) => Write(SettingPaths.Density, density);

    /// <summary>A card of «Filas visibles» (GEN-006): 0 is Auto.</summary>
    /// <param name="rows">The rows.</param>
    public void SetRows(int rows) => Write(SettingPaths.RowsPreference, rows);

    /// <summary>[followApp] (GEN-007): the same state as the Auto/Fijo button of the panel (PER-006).</summary>
    public void ToggleFollowApp() => Write(SettingPaths.LockProfile, !Settings.LockProfile);

    /// <summary>[showStripT] (GEN-007).</summary>
    public void ToggleAlwaysVisibleRow() =>
        Write(SettingPaths.ShowAlwaysVisibleRow, !Settings.ShowAlwaysVisibleRow);

    /// <summary>[showTabsT] (GEN-007).</summary>
    public void ToggleProfileSelectorRow() =>
        Write(SettingPaths.ShowProfileSelectorRow, !Settings.ShowProfileSelectorRow);

    /// <summary>A card of «Columnas» (GEN-008).</summary>
    /// <param name="columns">2, 3 or 4.</param>
    public void SetColumns(int columns) => Write(SettingPaths.Columns, columns);

    /// <summary>«Con teclas» or «Solo nombre» (GEN-008).</summary>
    /// <param name="show">Whether the keys show under each name.</param>
    public void SetShowKeys(bool show) => Write(SettingPaths.ShowKeys, show);

    /// <summary>The opacity slider (GEN-009): snapped to the nearest 5 %.</summary>
    /// <param name="percent">The slider's value, in percent.</param>
    public void SetOpacityPercent(double percent) =>
        Write(SettingPaths.Opacity, SettingsSchema.Opacity.Snap(percent / 100));

    /// <summary>− of the opacity: 10 % less, never below 30 % (GEN-009).</summary>
    public void OpacityLess() =>
        Write(
            SettingPaths.Opacity,
            SettingsSchema.Opacity.Snap(Settings.Opacity - SettingsSchema.OpacityButtonStep)
        );

    /// <summary>+ of the opacity: 10 % more, never above 100 % (GEN-009).</summary>
    public void OpacityMore() =>
        Write(
            SettingPaths.Opacity,
            SettingsSchema.Opacity.Snap(Settings.Opacity + SettingsSchema.OpacityButtonStep)
        );

    /// <summary>[autoDim] (GEN-009).</summary>
    public void ToggleAutoDim() => Write(SettingPaths.AutoDim, !Settings.AutoDim);

    /// <summary>The slider of [dimLevel] (GEN-009): 10 to 80 % in steps of 5.</summary>
    /// <param name="percent">The slider's value, in percent.</param>
    public void SetDimPercent(double percent) =>
        Write(SettingPaths.DimTo, SettingsSchema.DimTo.Snap(percent / 100));

    /// <summary>A side card of «Modo pestaña» (GEN-010): it does not change the view.</summary>
    /// <param name="side">The edge.</param>
    public void SetDockSide(DockSide side) => Write(SettingPaths.DockSide, side);

    /// <summary>↑ or ← of [handlePos] (GEN-010): 10 % back, never below 8 %.</summary>
    public void HandleBack() => MoveHandle(-1);

    /// <summary>↓ or → of [handlePos] (GEN-010): 10 % forward, never above 92 %.</summary>
    public void HandleForward() => MoveHandle(1);

    /// <summary>[handleLock] (PES-003): dragging no longer moves the handle; tapping still opens the bar.</summary>
    public void ToggleHandleLock() =>
        Write(SettingPaths.DockHandleLocked, !Settings.Dock.HandleLocked);

    /// <summary>A button of [dockCount] (GEN-010).</summary>
    /// <param name="perPage">4, 5, 6 or 8.</param>
    public void SetPerPage(int perPage) => Write(SettingPaths.DockPerPage, perPage);

    /// <summary>[autoHide] (GEN-010): the inverse of «pinOpen».</summary>
    public void ToggleAutoHide() => Write(SettingPaths.DockPinOpen, !Settings.Dock.PinOpen);

    /// <summary>[gutter] (GEN-010).</summary>
    public void ToggleKeepScrollbar() => Write(SettingPaths.DockGutter, !Settings.Dock.Gutter);

    /// <summary>[fbSound] (GEN-011).</summary>
    public void ToggleSound() => Write(SettingPaths.FeedbackSound, !Settings.Feedback.Sound);

    /// <summary>[fbFlash] (GEN-011).</summary>
    public void ToggleFlash() => Write(SettingPaths.FeedbackFlash, !Settings.Feedback.Flash);

    /// <summary>A button of [safeMax] (GEN-012): <see langword="null"/> is «Nunca».</summary>
    /// <param name="limit">The limit.</param>
    public void SetMaxHold(TimeSpan? limit) =>
        Write(SettingPaths.MaxHold, limit is { } value ? (object)value : SettingsSchema.NoValue);

    /// <summary>[safeSwitch] (GEN-012).</summary>
    public void ToggleReleaseOnAppSwitch() =>
        Write(SettingPaths.ReleaseOnAppSwitch, !Settings.KeySafety.ReleaseOnAppSwitch);

    /// <summary>[reduceM] (GEN-013, TEM-006).</summary>
    public void ToggleReduceMotion() => Write(SettingPaths.ReduceMotion, !Settings.ReduceMotion);

    /// <summary>
    /// [resetFreq] (GEN-013, FRE-004, REG-04): the first tap arms it for 3.5 s ([delConfirm]); the second one empties
    /// usage, pins and hidden after a backup, with [undo].
    /// </summary>
    public void ResetFrequents()
    {
        switch (_s.Confirm.Tap(ResetSubject))
        {
            case TwoStepResult.Armed armed:
                _resetArmed = true;
                _disarm?.Dispose();
                var wait = armed.Until - _s.Time.GetUtcNow();
                _disarm = _s.Time.CreateTimer(
                    _ =>
                        _s.Post(() =>
                        {
                            _resetArmed = false;
                            Refresh();
                        }),
                    null,
                    wait > TimeSpan.Zero ? wait : TimeSpan.Zero,
                    Timeout.InfiniteTimeSpan
                );
                Refresh();
                break;
            case TwoStepResult.Confirmed confirmed:
                _disarm?.Dispose();
                _resetArmed = false;
                if (_s.Store.Dispatch(new ResetFrequentsCommand(), confirmed.Token).IsSuccess)
                {
                    Notify(new WorkspaceNotice(L.ResetFreqT, ResetIcon, _s.Store.CanUndo, false));
                }

                Refresh();
                break;
        }
    }

    /// <summary>[seeWelcome] (GEN-014): closes the Control Center and opens the welcome at step 0.</summary>
    public void SeeWelcome() => _s.OpenWelcome();

    /// <summary>«Ver la guía de la pestaña» (GEN-014, PES-015): the guide shows again the next time the bar opens.</summary>
    public void SeeTabGuide()
    {
        if (_s.Store.Dispatch(new SetSetting(SettingPaths.DockCoachDone, false)).IsSuccess)
        {
            Refresh();
            Notify(new WorkspaceNotice(L.SeeCoachD, CoachIcon, false, false));
        }
    }

    /// <summary>[uNokb] (GEN-014, BIE-005).</summary>
    public void ToggleNoKeyboard() => Write(SettingPaths.NoKeyboardUser, !Settings.NoKeyboardUser);

    private static int Percent(double fraction) =>
        (int)Math.Round(fraction * 100, MidpointRounding.AwayFromZero);

    private static string PercentText(int percent) =>
        percent.ToString(CultureInfo.InvariantCulture) + PercentSign;

    private static string HandlePath(DockSide side) =>
        side switch
        {
            DockSide.Left => SettingPaths.DockHandleLeft,
            DockSide.Top => SettingPaths.DockHandleTop,
            DockSide.Bottom => SettingPaths.DockHandleBottom,
            _ => SettingPaths.DockHandleRight,
        };

    private static int HandlePosition(DockSettings dock) =>
        dock.Side switch
        {
            DockSide.Left => dock.HandlePositions.Left,
            DockSide.Top => dock.HandlePositions.Top,
            DockSide.Bottom => dock.HandlePositions.Bottom,
            _ => dock.HandlePositions.Right,
        };

    private void WriteTextScale(int direction)
    {
        var range = SettingsSchema.TextScalePercent;
        var next = (int)range.Clamp(Settings.TextScalePercent + (direction * range.Step));
        Write(SettingPaths.TextScale, next);
    }

    private void MoveHandle(int direction)
    {
        var dock = Settings.Dock;
        var range = SettingsSchema.DockHandlePosition;
        var next = (int)range.Clamp(HandlePosition(dock) + (direction * range.Step));
        Write(HandlePath(dock.Side), next);
    }

    private void Write(string path, object value)
    {
        if (!_s.Store.Dispatch(new SetSetting(path, value)).IsSuccess)
        {
            return;
        }

        Refresh();
        if (SettingsSchema.Find(path) is { Undoable: true })
        {
            Notify(new WorkspaceNotice(L.Saved, SavedIcon, _s.Store.CanUndo, false));
        }
    }

    private void Notify(WorkspaceNotice notice) =>
        Noticed?.Invoke(this, new WorkspaceNoticeEventArgs(notice));

    private string T(Message message) => _s.Localization.Current.Format(message);

    private SwitchItem Switch(string icon, Message title, Message? description, bool on) =>
        new(icon, T(title), description is { } d ? T(d) : string.Empty, on);

    private GeneralScreen Project()
    {
        var settings = Settings;
        return new GeneralScreen(
            T(L.PanelTitle),
            T(L.PanelSub),
            Look(settings),
            Layout(settings),
            Transparency(settings),
            Dock(settings),
            new FeedbackModel(
                T(L.SecFeedback),
                Switch("volume_up", L.FbSound, L.FbSoundD, settings.Feedback.Sound),
                Switch("flare", L.FbFlash, L.FbFlashD, settings.Feedback.Flash)
            ),
            Safety(settings),
            new AccessModel(
                T(L.SecA11yData),
                Switch("animation", L.ReduceM, L.ReduceMD, settings.ReduceMotion),
                T(_resetArmed ? L.DelConfirm : L.ResetFreq),
                T(L.ResetFreq),
                T(L.ResetFreqD),
                _resetArmed
            ),
            new StartModel(
                T(L.SecStart),
                T(L.SeeWelcome),
                T(L.SeeWelcomeD),
                T(L.SeeCoach),
                T(L.SeeCoachD),
                Switch("keyboard_off", L.UNokb, L.UNokbD, settings.NoKeyboardUser)
            )
        );
    }

    private LookModel Look(UserSettings settings)
    {
        var localizer = _s.Localization.Current;
        var scale = SettingsSchema.TextScalePercent;
        return new LookModel(
            T(L.LangBoth),
            [
                .. _s.Localization.Languages.Select(locale => new LanguageOption(
                    locale.Code,
                    locale.ShortName,
                    locale.NativeName,
                    string.Equals(locale.Code, settings.Language.Value, StringComparison.Ordinal)
                )),
            ],
            T(L.Theme),
            [
                Option(ThemeChoice.Auto, T(L.ThemeAuto), settings.Theme),
                Option(ThemeChoice.Dark, T(L.Dark), settings.Theme),
                Option(ThemeChoice.Light, T(L.Light), settings.Theme),
                Option(ThemeChoice.HighContrast, T(L.Hc), settings.Theme),
            ],
            T(L.Size),
            [
                Option(PanelSize.Small, T(L.SizeS), settings.Size),
                Option(PanelSize.Medium, T(L.SizeM), settings.Size),
                Option(PanelSize.Large, T(L.SizeL), settings.Size),
            ],
            T(L.TextSize),
            PercentText(settings.TextScalePercent),
            T(L.TextSmaller),
            T(L.TextBigger),
            settings.TextScalePercent > scale.Min,
            settings.TextScalePercent < scale.Max,
            T(L.View),
            [
                Option(PanelDensity.Full, T(L.DFull), settings.Density),
                Option(PanelDensity.Compact, T(L.DCompact), settings.Density),
                Option(PanelDensity.Dock, T(L.DDock), settings.Density),
            ],
            localizer.Format(
                settings.Density switch
                {
                    PanelDensity.Compact => L.DCompactD,
                    PanelDensity.Dock => L.DDockD,
                    _ => L.DFullD,
                }
            )
        );
    }

    private LayoutModel Layout(UserSettings settings)
    {
        var rowsTitle = T(L.RowsVis);
        var autoRows = settings.Size == PanelSize.Small ? 2 : 3;
        return new LayoutModel(
            T(L.SecLayout),
            rowsTitle,
            T(L.RowsVisD),
            [
                .. Enumerable
                    .Range(0, 4)
                    .Select(rows =>
                    {
                        var label =
                            rows == 0 ? T(L.RAuto2) : rows.ToString(CultureInfo.InvariantCulture);
                        return new SettingOption<int>(
                            rows,
                            label,
                            T(L.SettingOption(setting: rowsTitle, name: label)),
                            settings.RowsPreference == rows,
                            rows == 0 ? autoRows : rows
                        );
                    }),
            ],
            Switch("autorenew", L.FollowApp, L.FollowAppD, !settings.LockProfile),
            Switch("push_pin", L.ShowStripT, L.ShowStripD, settings.ShowAlwaysVisibleRow),
            Switch("tab", L.ShowTabsT, L.ShowTabsD, settings.ShowProfileSelectorRow),
            settings.ShowProfileSelectorRow ? string.Empty : T(L.ShowTabsNote),
            T(L.Columns),
            [
                .. Enumerable
                    .Range((int)SettingsSchema.Columns.Min, 3)
                    .Select(columns =>
                    {
                        var label = T(L.ColumnsN(columns));
                        return new SettingOption<int>(
                            columns,
                            label,
                            label,
                            settings.Columns == columns,
                            columns
                        );
                    }),
            ],
            T(L.ShowKeys),
            [
                Option(true, T(L.KeysWith), settings.ShowKeys),
                Option(false, T(L.KeysNameOnly), settings.ShowKeys),
            ],
            T(L.Copy),
            SampleKeys
        );
    }

    private TransparencyModel Transparency(UserSettings settings)
    {
        var dimTitle = T(L.DimLevel);
        var opacity = Percent(settings.Opacity);
        var dim = Percent(settings.DimTo);
        return new TransparencyModel(
            T(L.SecTransp),
            T(L.Opacity),
            opacity,
            PercentText(opacity),
            T(L.OpLess),
            T(L.OpMore),
            T(L.Behind),
            Switch("blur_on", L.AutoDim, L.AutoDimD, settings.AutoDim),
            dimTitle,
            dim,
            PercentText(dim),
            T(L.LessOf(name: dimTitle)),
            T(L.MoreOf(name: dimTitle))
        );
    }

    private DockModel Dock(UserSettings settings)
    {
        var dock = settings.Dock;
        var vertical = dock.Side is DockSide.Left or DockSide.Right;
        var position = HandlePosition(dock);
        var range = SettingsSchema.DockHandlePosition;
        var perPageTitle = T(L.DockCount);
        return new DockModel(
            T(L.SecBar),
            T(L.BarExplain),
            [
                Option(DockSide.Left, T(L.SLeft), dock.Side),
                Option(DockSide.Top, T(L.STop), dock.Side),
                Option(DockSide.Bottom, T(L.SBottom), dock.Side),
                Option(DockSide.Right, T(L.SRight), dock.Side),
            ],
            dock.Gutter,
            T(L.HandlePos),
            T(L.HandlePosD),
            vertical,
            T(vertical ? L.HandleUp : L.HandleLeft),
            T(vertical ? L.HandleDown : L.HandleRight),
            position > range.Min,
            position < range.Max,
            Switch("lock", L.HandleLock, L.HandleLockD, dock.HandleLocked),
            perPageTitle,
            [
                .. SettingsSchema.DockPerPageChoices.Select(count =>
                {
                    var label = count.ToString(CultureInfo.InvariantCulture);
                    return new SettingOption<int>(
                        count,
                        label,
                        T(L.SettingOption(setting: perPageTitle, name: label)),
                        dock.PerPage == count
                    );
                }),
            ],
            Switch("visibility_off", L.AutoHide, L.AutoHideD, !dock.PinOpen),
            Switch("swap_vert", L.Gutter, L.GutterD, dock.Gutter)
        );
    }

    private SafetyModel Safety(UserSettings settings)
    {
        var current = settings.KeySafety.MaxHold;
        var options = new List<SettingOption<TimeSpan?>>();
        foreach (var choice in SettingsSchema.MaxHoldChoices)
        {
            var label = T(
                choice.TotalSeconds % 60 != 0
                    ? L.HoldSeconds((long)choice.TotalSeconds)
                    : L.HoldMinutes((long)choice.TotalMinutes)
            );
            options.Add(new SettingOption<TimeSpan?>(choice, label, label, current == choice));
        }

        var never = T(L.Never);
        options.Add(new SettingOption<TimeSpan?>(null, never, never, current is null));
        return new SafetyModel(
            T(L.SecSafety),
            T(L.SafeMax),
            [.. options],
            T(L.SafeMaxD),
            Switch("swap_horiz", L.SafeSwitch, L.SafeSwitchD, settings.KeySafety.ReleaseOnAppSwitch)
        );
    }

    private static SettingOption<T> Option<T>(T value, string label, T current) =>
        new(value, label, label, EqualityComparer<T>.Default.Equals(value, current));
}
