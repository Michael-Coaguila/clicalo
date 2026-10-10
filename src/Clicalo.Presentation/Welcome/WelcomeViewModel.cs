using Clicalo.Application.Localization;
using Clicalo.Application.UseCases.Welcome;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using PanelSize = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Presentation.Welcome;

/// <summary>
/// The welcome (docs/06, BIE-001 to BIE-010) over a <see cref="WelcomeSession"/>, which holds the rules: this view
/// model only projects the texts and the choices of each step in the interface language (<see cref="Screen"/>) and
/// passes the taps on. The composition root calls <see cref="Refresh"/> when the language changes. Step 2 shows the
/// keyboard Windows reports and the programs language, as Plantillas does (BIE-006, PLA-009); step 1 of a repeated
/// welcome says what [Siguiente] changes and what stays as the person left it (BIE-010); step 0 of a reinstallation
/// asks whether to keep the data or start from scratch (P6).
/// </summary>
public sealed class WelcomeViewModel : ObservableObject
{
    private static readonly (WelcomeUse Use, string Icon, Message Label)[] UseOptions =
    [
        (WelcomeUse.Touch, "touch_app", L.UTouch),
        (WelcomeUse.Voice, "mic", L.UVoice),
        (WelcomeUse.NoKeyboard, "keyboard_off", L.UNokb),
        (WelcomeUse.Tremor, "vibration", L.UTremor),
        (WelcomeUse.Mouse, "mouse", L.UMouse),
    ];

    private readonly ILocalizationContext _localization;
    private readonly Func<string>? _detectedLayout;
    private readonly TimeProvider? _time;
    private readonly Action<Action>? _post;
    private WelcomeScreen? _screen;

    /// <summary>Creates the view model of <paramref name="session"/>.</summary>
    /// <param name="session">The welcome.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="detectedLayout">
    /// The layout of the keyboard in use (<c>KeyboardLayouts.Detect</c>), the same Plantillas shows; null assumes the
    /// default one.
    /// </param>
    /// <param name="time">The clock of the armed «Empezar de cero»; null never repaints it on its own.</param>
    /// <param name="post">Runs an action on the UI thread, after the current work.</param>
    public WelcomeViewModel(
        WelcomeSession session,
        ILocalizationContext localization,
        Func<string>? detectedLayout = null,
        TimeProvider? time = null,
        Action<Action>? post = null
    )
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(localization);
        Session = session;
        _localization = localization;
        _detectedLayout = detectedLayout;
        _time = time;
        _post = post;
        session.Changed += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>The welcome.</summary>
    public WelcomeSession Session { get; }

    /// <summary>What the welcome shows.</summary>
    public WelcomeScreen Screen
    {
        get => _screen!;
        private set => SetProperty(ref _screen, value);
    }

    /// <summary>[Siguiente] or [Empezar].</summary>
    public void Next() => Session.Next();

    /// <summary>[Atrás].</summary>
    public void Back() => Session.Back();

    /// <summary>[Omitir], and Alt+F4 (BIE-001).</summary>
    public void Skip() => Session.Skip();

    /// <summary>A language of step 0.</summary>
    /// <param name="code">The language code.</param>
    public void SetLanguage(string code) => Session.SetLanguage(new LangCode(code));

    /// <summary>
    /// «Empezar de cero» of step 0 (P6, REG-04): the first tap arms it and the button says [confirmB] until the window
    /// passes; the second replaces the data, after a backup.
    /// </summary>
    public void StartFromScratch()
    {
        Session.StartFromScratch();
        if (
            Session.FreshStartArmedUntil is { } until
            && _time is { } time
            && _post is { } post
            && until > time.GetUtcNow()
        )
        {
            _ = time.CreateTimer(
                _ => post(Refresh),
                null,
                until - time.GetUtcNow(),
                Timeout.InfiniteTimeSpan
            );
        }
    }

    /// <summary>A chip of step 1.</summary>
    /// <param name="use">The option, as <see cref="WelcomeOption.Id"/> gives it.</param>
    public void ToggleUse(string use)
    {
        if (Enum.TryParse<WelcomeUse>(use, out var parsed))
        {
            Session.ToggleUse(parsed);
        }
    }

    /// <summary>A chip of step 2.</summary>
    /// <param name="optionId">The kit option id.</param>
    public void ToggleKit(string optionId) => Session.ToggleKit(optionId);

    /// <summary>A card of step 3.</summary>
    /// <param name="density">The view.</param>
    public void SetDensity(PanelDensity density) => Session.SetDensity(density);

    /// <summary>A size of step 4.</summary>
    /// <param name="size">The size.</param>
    public void SetSize(PanelSize size) => Session.SetSize(size);

    /// <summary>A theme of step 4.</summary>
    /// <param name="theme">The theme, as <see cref="WelcomeOption.Id"/> gives it.</param>
    public void SetTheme(string theme)
    {
        if (Enum.TryParse<ThemeChoice>(theme, out var parsed))
        {
            Session.SetTheme(parsed);
        }
    }

    /// <summary>Projects everything again (the language changed, or the session).</summary>
    public void Refresh()
    {
        var settings = Session.Settings;
        var step = Session.Step;
        var (title, body) = step switch
        {
            0 => (L.Ob0t, L.Ob0b),
            1 => (L.Ob1t, L.Ob1b),
            2 => (L.Ob2t, L.Ob2b),
            3 => (L.ObVt, L.ObVb),
            _ => (L.Ob3t, null),
        };
        Screen = new WelcomeScreen(
            step,
            WelcomeSession.StepCount,
            T(L.ObStep(index: step + 1, total: WelcomeSession.StepCount)),
            T(L.WelcomeTitle),
            T(L.AppName),
            T(L.Tagline),
            T(title),
            body is null ? string.Empty : T(body),
            T(L.Story1),
            T(L.CreatorName),
            T(L.CreatorInitials),
            T(L.CreatorRole),
            Reinstall(),
            [
                .. _localization.Languages.Select(locale => new WelcomeOption(
                    locale.Code,
                    string.Empty,
                    locale.NativeName,
                    string.Empty,
                    string.Equals(locale.Code, settings.Language.Value, StringComparison.Ordinal)
                )),
            ],
            [
                .. UseOptions.Select(option => new WelcomeOption(
                    option.Use.ToString(),
                    option.Icon,
                    T(option.Label),
                    string.Empty,
                    Session.Uses.Contains(option.Use)
                )),
            ],
            Changes(settings),
            T(
                L.KbForLine(
                    app: settings.Keyboard.AppsLanguage == LangCode.En ? L.KbAppsEn : L.KbAppsEs,
                    name: LayoutLabel(
                        KeyboardLayouts.Effective(
                            settings.Keyboard.Layout,
                            _detectedLayout?.Invoke() ?? KeyboardLayouts.Detect(null)
                        )
                    )
                )
            ),
            Kit(settings.Language),
            Views(settings.Density),
            Sizes(settings.Size),
            T(L.Copy),
            Themes(settings.Theme),
            step > 0,
            T(L.Back),
            T(L.Skip),
            T(Session.IsLastStep ? L.Finish : L.Next)
        );
    }

    /// <summary>The question of a reinstallation (P6): keep the data, the default, or start from scratch.</summary>
    private WelcomeReinstallCard? Reinstall()
    {
        if (!Session.OffersFreshStart && !Session.StartedFresh)
        {
            return null;
        }

        var armed = Session.FreshStartArmedUntil is not null;
        return new WelcomeReinstallCard(
            T(L.ReinstallT),
            T(L.ReinstallD),
            T(L.ReinstallKeep),
            T(armed ? L.ConfirmB : L.ReinstallFresh),
            armed,
            Session.StartedFresh ? T(L.ReinstallDone) : string.Empty
        );
    }

    /// <summary>What [Siguiente] of step 1 changes and what it leaves as the person set it (BIE-010).</summary>
    private WelcomeChangesNote? Changes(UserSettings settings)
    {
        if (
            Session.PendingChanges is not { } plan
            || (plan.Changes.IsEmpty && plan.Kept.IsEmpty)
        )
        {
            return null;
        }

        return new WelcomeChangesNote(
            T(L.ObChangesT),
            [.. plan.Changes.Items.Select(setting => T(Line(setting, plan.Settings)))],
            T(L.ObKeptT),
            [.. plan.Kept.Items.Select(setting => T(Line(setting, settings)))]
        );
    }

    private static Message Line(WelcomeSetting setting, UserSettings settings) =>
        setting switch
        {
            WelcomeSetting.TouchPreset => L.ObChPreset(name: PresetLabel(settings.Touch.Preset)),
            WelcomeSetting.Size => L.ObChSize(
                name: settings.Size switch
                {
                    PanelSize.Small => L.SizeS,
                    PanelSize.Large => L.SizeL,
                    _ => L.SizeM,
                }
            ),
            WelcomeSetting.VoiceNumbers => settings.VoiceNumbers
                ? L.ObChVoiceOn
                : L.ObChVoiceOff,
            _ => settings.NoKeyboardUser ? L.ObChNoKbOn : L.ObChNoKbOff,
        };

    private static Message PresetLabel(string preset) =>
        string.Equals(preset, TouchPresets.Standard.Id, StringComparison.Ordinal) ? L.PStd
        : string.Equals(preset, TouchPresets.MildTremor.Id, StringComparison.Ordinal) ? L.PLeve
        : string.Equals(preset, TouchPresets.StrongTremor.Id, StringComparison.Ordinal)
            ? L.PFuerte
        : L.PCustom;

    private static Message LayoutLabel(string layout) =>
        layout switch
        {
            KeyboardLayouts.SpanishSpain => L.KbEsEs,
            KeyboardLayouts.EnglishUs => L.KbEnUs,
            KeyboardLayouts.EnglishInternational => L.KbEnInt,
            _ => L.KbEsLa,
        };

    private ValueList<WelcomeOption> Kit(LangCode language)
    {
        if (Session.Content is not { } content)
        {
            return [];
        }

        var options = new List<WelcomeOption>();
        foreach (var option in content.Kit.Options)
        {
            if (option.Kind == StarterOptionKind.Basics)
            {
                options.Add(
                    new WelcomeOption(
                        option.Id,
                        option.Icon?.Name ?? string.Empty,
                        option.Label is { } label ? T(new Message(label)) : option.Id,
                        option.Description is { } description
                            ? T(new Message(description))
                            : string.Empty,
                        Session.Kit.IsChosen(option.Id)
                    )
                );
            }
            else if (content.Template(option.Id) is { } template)
            {
                options.Add(
                    new WelcomeOption(
                        option.Id,
                        template.Icon.Name,
                        template.Name.Get(language, LangCode.Es),
                        string.Empty,
                        Session.Kit.IsChosen(option.Id)
                    )
                );
            }
        }

        return [.. options];
    }

    private ValueList<WelcomeViewCard> Views(PanelDensity current) =>
        [
            new(
                PanelDensity.Full,
                "view_agenda",
                T(L.DFull),
                T(L.DFullD),
                30,
                38,
                current == PanelDensity.Full
            ),
            new(
                PanelDensity.Compact,
                "view_compact",
                T(L.DCompact),
                T(L.DCompactD),
                30,
                26,
                current == PanelDensity.Compact
            ),
            new(
                PanelDensity.Dock,
                "view_sidebar",
                T(L.DDock),
                T(L.DDockD),
                14,
                38,
                current == PanelDensity.Dock
            ),
        ];

    private ValueList<WelcomeSizeCard> Sizes(PanelSize current) =>
        [
            Size(PanelSize.Small, PanelSizes.S, L.SizeS, current),
            Size(PanelSize.Medium, PanelSizes.M, L.SizeM, current),
            Size(PanelSize.Large, PanelSizes.L, L.SizeL, current),
        ];

    private WelcomeSizeCard Size(
        PanelSize size,
        SizeMetrics metrics,
        Message label,
        PanelSize current
    ) =>
        new(
            size,
            T(label),
            metrics.TileWidthPx,
            metrics.TileHeightPx,
            metrics.TileIconPx,
            metrics.TileLabelPx,
            size == current
        );

    private ValueList<WelcomeOption> Themes(ThemeChoice current) =>
        [
            Theme(ThemeChoice.Auto, L.ThemeAutoS, current),
            Theme(ThemeChoice.Dark, L.Dark, current),
            Theme(ThemeChoice.Light, L.Light, current),
            Theme(ThemeChoice.HighContrast, L.Hc, current),
        ];

    private WelcomeOption Theme(ThemeChoice theme, Message label, ThemeChoice current) =>
        new(theme.ToString(), string.Empty, T(label), string.Empty, theme == current);

    private string T(Message message) => _localization.Current.Format(message);
}
