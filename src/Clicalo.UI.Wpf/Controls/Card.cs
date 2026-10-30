using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A card of direction C (D12): a container of radius <see cref="Radii.Large"/> with a padding of
/// <see cref="Space.S4"/>, filled and lined as <see cref="Tone"/> says (its line is drawn inside). It is the second
/// level of elevation (surface → card) and never goes inside another card: no more than two nested borders. It is not
/// interactive: its content is. UI Automation sees a Group, named with <c>AutomationProperties.Name</c> when the card
/// has a title (<see cref="CardAutomationPeer"/>).
/// </summary>
public sealed class Card : ContentControl
{
    /// <summary>Identifies <see cref="Tone"/>.</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone),
        typeof(CardTone),
        typeof(Card),
        new FrameworkPropertyMetadata(CardTone.Neutral, OnToneChanged)
    );

    /// <summary>Name of the rounded border.</summary>
    public const string ChromePart = "PART_Chrome";

    private static readonly ControlTemplate DefaultTemplate = Templates.Seal(
        typeof(Card),
        Templates.Chrome(ChromePart, Radii.Large).Add(Templates.Element<ContentPresenter>())
    );

    static Card()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Card),
            new FrameworkPropertyMetadata(typeof(Card))
        );
        TemplateProperty.OverrideMetadata(
            typeof(Card),
            new FrameworkPropertyMetadata(DefaultTemplate)
        );
        FocusableProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(false));
    }

    /// <summary>Creates a neutral card.</summary>
    public Card()
    {
        Apply(this, CardTone.Neutral);
        Padding = new Thickness(Space.S4);
    }

    /// <summary>The fill of the card (<see cref="CardTone.Neutral"/> by default).</summary>
    public CardTone Tone
    {
        get => (CardTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CardAutomationPeer(this);

    private static void OnToneChanged(
        DependencyObject card,
        DependencyPropertyChangedEventArgs e
    ) => Apply((Card)card, (CardTone)e.NewValue);

    private static void Apply(Card card, CardTone tone)
    {
        var (background, border) = tone switch
        {
            CardTone.Neutral => (ColorToken.Card, ColorToken.Border),
            CardTone.Accent => (ColorToken.AccentWash, ColorToken.Accent),
            CardTone.Warn => (ColorToken.WarnWash, ColorToken.Warn),
            CardTone.Danger => (ColorToken.DangerWash, ColorToken.Danger),
            _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, message: null),
        };
        Templates.UseTheme(card, background, ColorToken.Text, border);
    }
}
