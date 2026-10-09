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
/// passes the taps on. The composition root calls <see cref="Refresh"/> when the language changes.
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
    private WelcomeScreen? _screen;

    /// <summary>Creates the view model of <paramref name="session"/>.</summary>
    /// <param name="session">The welcome.</param>
    /// <param name="localization">The interface language.</param>
    public WelcomeViewModel(WelcomeSession session, ILocalizationContext localization)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(localization);
        Session = session;
        _localization = localization;
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
            T(
                L.ObKbLine(
                    app: settings.Keyboard.AppsLanguage == LangCode.En ? L.KbAppsEn : L.KbAppsEs
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
