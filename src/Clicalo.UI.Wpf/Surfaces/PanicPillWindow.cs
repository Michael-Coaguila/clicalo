using System.Windows;
using System.Windows.Automation;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;
using TouchTargetSize = Clicalo.UI.Wpf.Controls.TouchTarget;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The floating «Release all» of the Tab view and the bubble (PES-013, BUR-002, REG-03): a red pill of 44 with ⚠ and
/// [releaseAll], always at 100 %, that releases everything in one tap. It shows only while something is held.
/// </summary>
public sealed class PanicPillWindow : TouchSurface
{
    private const double PanicTextPx = 13;
    private const double PanicIconPx = 18;

    private readonly Action _releaseAll;
    private readonly TouchButton _button;

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
            new SurfaceLook(default, Clicalo.UI.Wpf.Theming.Generated.Shadows.Menu, Round: true)
        )
    {
        ArgumentNullException.ThrowIfNull(releaseAll);
        _releaseAll = releaseAll;
        _button = SurfaceParts.Button(
            "warning",
            PanicIconPx,
            ButtonAppearance.Danger,
            releaseAll,
            height: TouchTargetSize.MinimumSize
        );
        _button.Padding = new Thickness(14, 0, 14, 0);
        _button.SetResourceReference(FontSizeProperty, ThemeKeys.TextSize(PanicTextPx));
        AutomationProperties.SetLiveSetting(_button, AutomationLiveSetting.Assertive);
        Content = _button;
    }

    /// <summary>The button.</summary>
    public TouchButton Button => _button;

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
}
