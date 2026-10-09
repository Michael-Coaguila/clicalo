using System.Globalization;
using Clicalo.Application.Localization;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Panel.EditMode;
using Clicalo.Presentation.Panel.TestMode;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel.QuickSettings;

/// <summary>
/// The Quick settings sheet (AJR-001 to AJR-005, docs/04 §5), in this order: the «Centro de control» card ([openCC] and
/// [ccSub]); Vista (Completa, Compacta, Pestaña); Opacidad (− · slider from 30 to 100 % · +); Tamaño (S, M, L); Lado
/// de la pestaña, only in the Tab view; Tema in 2 × 2 (Auto, Oscuro, Claro, Alto contraste); and the switch rows Modo
/// prueba (30 s), [autoDim], [stickyMods] and [voiceNums]. Every change is a <see cref="SetSetting"/> on the document,
/// saved by itself and applied at once by whoever follows the settings (theme, opacity, layout, dimming); presentation
/// settings stay out of the undo history (DAT-006).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Vista, Tamaño, Lado, switching test mode on and opening the control center close the sheet; Tema, Opacidad,
/// Atenuar, Teclas fijas and Números do not (AJR-001). Opening the search, entering edit mode and minimizing close it
/// too: the composition calls <see cref="Close"/>.</item>
/// <item>The − and + of the opacity move 10 % and round to a multiple of 5 inside 30–100 (AJR-002): the grid and the
/// bounds are <see cref="SettingsSchema.Opacity"/>'s.</item>
/// <item>It shows what <see cref="Apply"/> last gave it; the composition calls it after every change of the
/// settings, on the UI thread of the Surfaces role.</item>
/// </list>
/// </remarks>
public sealed class QuickSettingsViewModel : ObservableObject
{
    /// <summary>AJR-002: the − and + buttons move the opacity by 10 %.</summary>
    private const double OpacityButtonStep = 0.10;

    private const string PercentSign = "%";
    private const string TestModeIcon = "science";
    private const string AutoDimIcon = "blur_on";
    private const string StickyIcon = "keyboard";
    private const string VoiceIcon = "pin";

    private readonly DocumentStore _store;
    private readonly ILocalizationContext _localization;
    private readonly TestModeViewModel _testMode;
    private readonly IControlCenterIntents _controlCenter;
    private readonly Func<ProfileId?> _profileInView;
    private UserSettings _settings;
    private bool _isOpen;
    private bool _showsSides;
    private int _opacityPercent;
    private string _opacityText = string.Empty;
    private string _name = string.Empty;
    private string _controlCenterTitle = string.Empty;
    private string _controlCenterSubtitle = string.Empty;
    private string _viewHeading = string.Empty;
    private string _opacityHeading = string.Empty;
    private string _opacityLessName = string.Empty;
    private string _opacityMoreName = string.Empty;
    private string _sizeHeading = string.Empty;
    private string _sideHeading = string.Empty;
    private string _themeHeading = string.Empty;

    /// <summary>Creates the sheet, closed.</summary>
    /// <param name="store">The document whose settings it shows and writes.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="testMode">Test mode, behind the first switch row.</param>
    /// <param name="controlCenter">Where the «Centro de control» card goes.</param>
    /// <param name="profileInView">
    /// The profile in view, or <see langword="null"/> while Frequents is (the card then opens General, AJR-001).
    /// </param>
    public QuickSettingsViewModel(
        DocumentStore store,
        ILocalizationContext localization,
        TestModeViewModel testMode,
        IControlCenterIntents controlCenter,
        Func<ProfileId?> profileInView
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(testMode);
        ArgumentNullException.ThrowIfNull(controlCenter);
        ArgumentNullException.ThrowIfNull(profileInView);
        _store = store;
        _localization = localization;
        _testMode = testMode;
        _controlCenter = controlCenter;
        _profileInView = profileInView;
        _settings = store.Current.Settings;

        Views =
        [
            Option(PanelDensity.Full, "view_agenda", SettingPaths.Density, closes: true),
            Option(PanelDensity.Compact, "view_compact", SettingPaths.Density, closes: true),
            Option(PanelDensity.Dock, "view_sidebar", SettingPaths.Density, closes: true),
        ];
        Sizes =
        [
            Option(PanelSize.Small, string.Empty, SettingPaths.Size, closes: true),
            Option(PanelSize.Medium, string.Empty, SettingPaths.Size, closes: true),
            Option(PanelSize.Large, string.Empty, SettingPaths.Size, closes: true),
        ];
        Sides =
        [
            Option(DockSide.Left, "align_horizontal_left", SettingPaths.DockSide, closes: true),
            Option(DockSide.Top, "align_vertical_top", SettingPaths.DockSide, closes: true),
            Option(DockSide.Bottom, "align_vertical_bottom", SettingPaths.DockSide, closes: true),
            Option(DockSide.Right, "align_horizontal_right", SettingPaths.DockSide, closes: true),
        ];
        Themes =
        [
            Option(ThemeChoice.Auto, string.Empty, SettingPaths.Theme, closes: false),
            Option(ThemeChoice.Dark, string.Empty, SettingPaths.Theme, closes: false),
            Option(ThemeChoice.Light, string.Empty, SettingPaths.Theme, closes: false),
            Option(ThemeChoice.HighContrast, string.Empty, SettingPaths.Theme, closes: false),
        ];
        TestModeSwitch = new QuickSwitchViewModel(TestModeIcon, ToggleTestMode);
        AutoDimSwitch = new QuickSwitchViewModel(
            AutoDimIcon,
            () => Write(SettingPaths.AutoDim, !_settings.AutoDim, closes: false)
        );
        StickySwitch = new QuickSwitchViewModel(
            StickyIcon,
            () =>
                Write(SettingPaths.StickyModifiersRow, !_settings.StickyModifiersRow, closes: false)
        );
        VoiceNumbersSwitch = new QuickSwitchViewModel(
            VoiceIcon,
            () => Write(SettingPaths.VoiceNumbers, !_settings.VoiceNumbers, closes: false)
        );
        Switches = [TestModeSwitch, AutoDimSwitch, StickySwitch, VoiceNumbersSwitch];

        _testMode.PropertyChanged += (_, change) =>
        {
            if (
                string.Equals(
                    change.PropertyName,
                    nameof(TestModeViewModel.IsOn),
                    StringComparison.Ordinal
                )
            )
            {
                TestModeSwitch.IsOn = _testMode.IsOn;
            }
        };
        Relocalize();
        Apply(_settings);
    }

    /// <summary>Whether the sheet is open (the panel does not dim while it is, docs/04).</summary>
    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    /// <summary>The sheet's accessible name, [quick].</summary>
    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    /// <summary>[openCC], the title of the card.</summary>
    public string ControlCenterTitle
    {
        get => _controlCenterTitle;
        private set => SetProperty(ref _controlCenterTitle, value);
    }

    /// <summary>[ccSub], the line under it.</summary>
    public string ControlCenterSubtitle
    {
        get => _controlCenterSubtitle;
        private set => SetProperty(ref _controlCenterSubtitle, value);
    }

    /// <summary>[view].</summary>
    public string ViewHeading
    {
        get => _viewHeading;
        private set => SetProperty(ref _viewHeading, value);
    }

    /// <summary>Completa, Compacta and Pestaña.</summary>
    public IReadOnlyList<QuickOptionViewModel> Views { get; }

    /// <summary>[opacity].</summary>
    public string OpacityHeading
    {
        get => _opacityHeading;
        private set => SetProperty(ref _opacityHeading, value);
    }

    /// <summary>The opacity in percent, 30 to 100 in steps of 5 (the slider's value).</summary>
    public int OpacityPercent
    {
        get => _opacityPercent;
        private set => SetProperty(ref _opacityPercent, value);
    }

    /// <summary>The value shown next to [opacity]: «92%».</summary>
    public string OpacityText
    {
        get => _opacityText;
        private set => SetProperty(ref _opacityText, value);
    }

    /// <summary>The lowest opacity of the slider, in percent.</summary>
    public static int OpacityMinimumPercent => Percent(SettingsSchema.Opacity.Min);

    /// <summary>The highest opacity of the slider, in percent.</summary>
    public static int OpacityMaximumPercent => Percent(SettingsSchema.Opacity.Max);

    /// <summary>The step of the slider, in percent.</summary>
    public static int OpacityStepPercent => Percent(SettingsSchema.Opacity.Step);

    /// <summary>The accessible name of − ([opLess]).</summary>
    public string OpacityLessName
    {
        get => _opacityLessName;
        private set => SetProperty(ref _opacityLessName, value);
    }

    /// <summary>The accessible name of + ([opMore]).</summary>
    public string OpacityMoreName
    {
        get => _opacityMoreName;
        private set => SetProperty(ref _opacityMoreName, value);
    }

    /// <summary>[size].</summary>
    public string SizeHeading
    {
        get => _sizeHeading;
        private set => SetProperty(ref _sizeHeading, value);
    }

    /// <summary>S, M and L.</summary>
    public IReadOnlyList<QuickOptionViewModel> Sizes { get; }

    /// <summary>Whether the «Lado de la pestaña» row shows: only in the Tab view (AJR-001 item 5).</summary>
    public bool ShowsSides
    {
        get => _showsSides;
        private set => SetProperty(ref _showsSides, value);
    }

    /// <summary>[barSide].</summary>
    public string SideHeading
    {
        get => _sideHeading;
        private set => SetProperty(ref _sideHeading, value);
    }

    /// <summary>Left, top, bottom and right, icons with an accessible name.</summary>
    public IReadOnlyList<QuickOptionViewModel> Sides { get; }

    /// <summary>[theme].</summary>
    public string ThemeHeading
    {
        get => _themeHeading;
        private set => SetProperty(ref _themeHeading, value);
    }

    /// <summary>Auto, Oscuro, Claro and Alto contraste, in a 2 × 2 grid.</summary>
    public IReadOnlyList<QuickOptionViewModel> Themes { get; }

    /// <summary>Modo prueba (30 s).</summary>
    public QuickSwitchViewModel TestModeSwitch { get; }

    /// <summary>[autoDim] «Atenuar cuando no lo uso».</summary>
    public QuickSwitchViewModel AutoDimSwitch { get; }

    /// <summary>[stickyMods] «Teclas fijas».</summary>
    public QuickSwitchViewModel StickySwitch { get; }

    /// <summary>[voiceNums] «Números para control por voz».</summary>
    public QuickSwitchViewModel VoiceNumbersSwitch { get; }

    /// <summary>The four switch rows, in order.</summary>
    public IReadOnlyList<QuickSwitchViewModel> Switches { get; }

    /// <summary>⚙ of the header: opens or closes the sheet.</summary>
    public void Toggle() => IsOpen = !IsOpen;

    /// <summary>Opens the sheet.</summary>
    public void Open() => IsOpen = true;

    /// <summary>Closes the sheet.</summary>
    public void Close() => IsOpen = false;

    /// <summary>The «Centro de control» card: closes the sheet and opens the control center (AJR-001 item 1).</summary>
    public void OpenControlCenter()
    {
        Close();
        _controlCenter.OpenControlCenter(_profileInView() ?? ProfileId.General);
    }

    /// <summary>− of the opacity: 10 % less, rounded to a multiple of 5, never below 30 % (AJR-002).</summary>
    public void DecreaseOpacity() => WriteOpacity(_settings.Opacity - OpacityButtonStep);

    /// <summary>+ of the opacity: 10 % more, rounded to a multiple of 5, never above 100 % (AJR-002).</summary>
    public void IncreaseOpacity() => WriteOpacity(_settings.Opacity + OpacityButtonStep);

    /// <summary>The slider moved (or UI Automation set its value): the opacity snaps to the nearest multiple of 5.</summary>
    /// <param name="percent">The slider's value, in percent.</param>
    public void SetOpacityPercent(double percent) => WriteOpacity(percent / 100);

    /// <summary>Shows <paramref name="settings"/>: the chosen options, the opacity and the switches.</summary>
    /// <param name="settings">The current settings.</param>
    public void Apply(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        Select(Views, settings.Density);
        Select(Sizes, settings.Size);
        Select(Sides, settings.Dock.Side);
        Select(Themes, settings.Theme);
        ShowsSides = settings.Density == PanelDensity.Dock;
        OpacityPercent = Percent(settings.Opacity);
        OpacityText = OpacityPercent.ToString(CultureInfo.InvariantCulture) + PercentSign;
        TestModeSwitch.IsOn = _testMode.IsOn;
        AutoDimSwitch.IsOn = settings.AutoDim;
        StickySwitch.IsOn = settings.StickyModifiersRow;
        VoiceNumbersSwitch.IsOn = settings.VoiceNumbers;
    }

    /// <summary>Formats every text again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        var l = _localization.Current;
        Name = l.Format(L.Quick);
        ControlCenterTitle = l.Format(L.OpenCC);
        ControlCenterSubtitle = l.Format(L.CcSub);
        ViewHeading = l.Format(L.View);
        OpacityHeading = l.Format(L.Opacity);
        OpacityLessName = l.Format(L.OpLess);
        OpacityMoreName = l.Format(L.OpMore);
        SizeHeading = l.Format(L.Size);
        SideHeading = l.Format(L.BarSide);
        ThemeHeading = l.Format(L.Theme);
        Label(Views[0], l.Format(L.DFull));
        Label(Views[1], l.Format(L.DCompact));
        Label(Views[2], l.Format(L.DDock));
        Label(Sizes[0], l.Format(L.SzS), l.Format(L.SizeS));
        Label(Sizes[1], l.Format(L.SzM), l.Format(L.SizeM));
        Label(Sizes[2], l.Format(L.SzL), l.Format(L.SizeL));
        Label(Sides[0], string.Empty, l.Format(L.SLeft));
        Label(Sides[1], string.Empty, l.Format(L.STop));
        Label(Sides[2], string.Empty, l.Format(L.SBottom));
        Label(Sides[3], string.Empty, l.Format(L.SRight));
        Label(Themes[0], l.Format(L.ThemeAutoS));
        Label(Themes[1], l.Format(L.Dark));
        Label(Themes[2], l.Format(L.Light));
        Label(Themes[3], l.Format(L.Hc));
        TestModeSwitch.Label = l.Format(L.TmLabel);
        AutoDimSwitch.Label = l.Format(L.AutoDim);
        StickySwitch.Label = l.Format(L.StickyMods);
        VoiceNumbersSwitch.Label = l.Format(L.VoiceNums);
    }

    private static int Percent(double fraction) =>
        (int)Math.Round(fraction * 100, MidpointRounding.AwayFromZero);

    private static void Label(QuickOptionViewModel option, string label) =>
        Label(option, label, label);

    private static void Label(QuickOptionViewModel option, string label, string accessibleName)
    {
        option.Label = label;
        option.AccessibleName = accessibleName;
    }

    private static void Select(IReadOnlyList<QuickOptionViewModel> options, object value)
    {
        foreach (var option in options)
        {
            option.IsSelected = Equals(option.Value, value);
        }
    }

    private QuickOptionViewModel Option(object value, string icon, string path, bool closes) =>
        new(value, icon, () => Write(path, value, closes));

    private void ToggleTestMode()
    {
        if (_testMode.IsOn)
        {
            _testMode.Stop();
            return;
        }

        // AJR-001, TAC-008: switching test mode on closes the sheet so the tiles can be touched.
        Close();
        _testMode.Start();
    }

    private void WriteOpacity(double opacity) =>
        Write(SettingPaths.Opacity, SettingsSchema.Opacity.Snap(opacity), closes: false);

    private void Write(string path, object value, bool closes)
    {
        if (_store.Dispatch(new SetSetting(path, value)).IsSuccess)
        {
            Apply(_store.Current.Settings);
        }

        if (closes)
        {
            Close();
        }
    }
}
