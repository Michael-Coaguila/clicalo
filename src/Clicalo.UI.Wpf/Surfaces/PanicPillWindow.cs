using System.Windows.Automation;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The floating «Release all» of the Tab view and the bubble (PES-013, BUR-002, REG-03): the one filled danger button of
/// direction C (D12), <see cref="ControlHeight.Small"/> high with the radius of every button, ⚠ and [releaseAll] in
/// bold <c>onDanger</c>, always at 100 %, that releases everything in one tap. It shows only while something is held. It is an
/// assertive live region (ACC-001): when it appears, and whenever what is held changes while it is on screen, it
/// announces what is held, as the panic strip of the panel does (SEG-002); what is held is also its help text.
/// </summary>
public sealed class PanicPillWindow : TouchSurface
{
    private readonly Action _releaseAll;
    private readonly TouchButton _button;
    private readonly LiveAnnouncer _announcer;
    private string _held = string.Empty;
    private string _announced = string.Empty;

    /// <summary>Creates the button on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    /// <param name="releaseAll">What a tap does.</param>
    public PanicPillWindow(
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch,
        Action releaseAll
    )
        : base(
            new SurfaceId(SurfaceKind.Notice, 1),
            registry,
            time,
            theme,
            touch,
            static (_, _, _) => { },
            new SurfaceLook(default, Shadows.Menu, Round: true)
        )
    {
        ArgumentNullException.ThrowIfNull(releaseAll);
        _releaseAll = releaseAll;
        _button = SurfaceParts.Button(
            "warning",
            IconBox.Small,
            ButtonVariant.Danger,
            releaseAll,
            height: ControlHeight.Small
        );
        // SEG-002: «Soltar todo» is the panic itself: the danger fill with its text, the one filled danger button.
        _button.SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Danger));
        _button.SetResourceReference(ForegroundProperty, ThemeBrushKey.For(ColorToken.OnDanger));
        AutomationProperties.SetLiveSetting(_button, AutomationLiveSetting.Assertive);
        _announcer = new LiveAnnouncer(_button);
        Content = _button;

        // The button is on screen exactly while its window is: it tells when the pill appears.
        _button.IsVisibleChanged += (_, _) => Say();
    }

    /// <summary>The button.</summary>
    public TouchButton Button => _button;

    /// <summary>What announces the pill (tests read what it said).</summary>
    public LiveAnnouncer Announcer => _announcer;

    /// <summary>
    /// Takes what is held, already localized (the message of the panic strip): the help text of the button and what the
    /// pill announces while it is on screen.
    /// </summary>
    /// <param name="held">The message; empty when nothing is held.</param>
    public void ApplyHeld(string held)
    {
        ArgumentNullException.ThrowIfNull(held);
        _held = held;
        AutomationProperties.SetHelpText(_button, held);
        Say();
    }

    /// <summary>Applies the localized [releaseAll].</summary>
    /// <param name="label">The text.</param>
    public void ApplyLabel(string label)
    {
        _button.Content = label;
        SurfaceParts.Name(_button, label);
        Title = label;
    }

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets() =>
        [SurfaceTarget.Button(_button, _releaseAll)];

    /// <summary>
    /// Announces what is held once per change, and only while the pill is on screen: a hidden surface announces
    /// nothing, and it does when it appears.
    /// </summary>
    private void Say()
    {
        if (!_button.IsVisible || _held.Length == 0)
        {
            _announced = string.Empty;
            return;
        }

        if (string.Equals(_held, _announced, StringComparison.Ordinal))
        {
            return;
        }

        _announced = _held;
        _announcer.Say(_held, AnnouncementUrgency.Assertive);
    }
}
