using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// One of the three buttons of a <see cref="TitleBar"/> (D13): a zone of <see cref="ComponentSize.CaptionButton"/> ×
/// <see cref="ComponentSize.CaptionButton"/>, as in every window of Windows, with its icon in a box of
/// <see cref="IconBox.Small"/>. Under the pointer the zone takes <c>cardHi</c>; the close button takes
/// <c>danger</c> with its icon in <c>onDanger</c>. UI Automation sees a Button with Invoke named by the view
/// (minimize, maximize or restore, close), never by its glyph (UIA008).
/// </summary>
public sealed class CaptionButton : Button
{
    /// <summary>Identifies <see cref="Kind"/>.</summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(CaptionKind),
        typeof(CaptionButton),
        new FrameworkPropertyMetadata(CaptionKind.Minimize, OnLookChanged)
    );

    /// <summary>Identifies <see cref="IsMaximized"/>.</summary>
    public static readonly DependencyProperty IsMaximizedProperty = DependencyProperty.Register(
        nameof(IsMaximized),
        typeof(bool),
        typeof(CaptionButton),
        new FrameworkPropertyMetadata(false, OnLookChanged)
    );

    /// <summary>Identifies <see cref="IsFrameHot"/>.</summary>
    public static readonly DependencyProperty IsFrameHotProperty = DependencyProperty.Register(
        nameof(IsFrameHot),
        typeof(bool),
        typeof(CaptionButton),
        new FrameworkPropertyMetadata(false)
    );

    private static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        "Symbol",
        typeof(string),
        typeof(CaptionButton),
        new FrameworkPropertyMetadata("remove")
    );

    private const string FillPart = "PART_Fill";
    private const string IconPart = "PART_Icon";
    private static readonly ControlTemplate DefaultTemplate = CreateTemplate();

    static CaptionButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CaptionButton),
            new FrameworkPropertyMetadata(typeof(CaptionButton))
        );
        TemplateProperty.OverrideMetadata(
            typeof(CaptionButton),
            new FrameworkPropertyMetadata(DefaultTemplate)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(CaptionButton),
            new FrameworkPropertyMetadata(FocusRingStyle.Create(0))
        );
        TouchTarget.Enforce(typeof(CaptionButton));
    }

    /// <summary>Creates a minimize button.</summary>
    public CaptionButton()
    {
        Width = ComponentSize.CaptionButton;
        Height = ComponentSize.CaptionButton;
        Background = Brushes.Transparent;
        SetResourceReference(ForegroundProperty, ThemeBrushKey.For(ColorToken.Text));
    }

    /// <summary>Which button it is.</summary>
    public CaptionKind Kind
    {
        get => (CaptionKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Whether the window is maximized: the maximize button then shows and does «restore».</summary>
    public bool IsMaximized
    {
        get => (bool)GetValue(IsMaximizedProperty);
        set => SetValue(IsMaximizedProperty, value);
    }

    /// <summary>
    /// Whether the pointer is over it as Windows sees it: over the maximize button Windows answers the non-client
    /// messages (Snap layouts), so WPF does not see the pointer and the title bar says it here.
    /// </summary>
    public bool IsFrameHot
    {
        get => (bool)GetValue(IsFrameHotProperty);
        set => SetValue(IsFrameHotProperty, value);
    }

    /// <summary>Clicks it as a tap does: the title bar calls it when Windows reports a click on the maximize zone.</summary>
    internal void ClickFromFrame() => OnClick();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new CaptionButtonAutomationPeer(this);

    private static void OnLookChanged(DependencyObject button, DependencyPropertyChangedEventArgs e)
    {
        var caption = (CaptionButton)button;
        caption.SetValue(
            SymbolProperty,
            caption.Kind switch
            {
                CaptionKind.Maximize => caption.IsMaximized ? "filter_none" : "crop_square",
                CaptionKind.Close => "close",
                _ => "remove",
            }
        );
    }

    private static ControlTemplate CreateTemplate()
    {
        var icon = Templates
            .Element<SymbolIcon>(IconPart)
            .With(SymbolIcon.SymbolProperty, Templates.Bind(SymbolProperty))
            .With(SymbolIcon.SizeProperty, IconBox.Small)
            .With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center)
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(SymbolIcon.ForegroundProperty, Templates.Bind(ForegroundProperty));
        var fill = Templates
            .Element<Border>(FillPart)
            .With(Border.BackgroundProperty, Templates.Bind(BackgroundProperty))
            .Add(icon);

        Trigger Hot(DependencyProperty property) =>
            Templates.When(
                property,
                true,
                Templates.Brush(Border.BackgroundProperty, ColorToken.CardHi, FillPart)
            );

        MultiTrigger Red(DependencyProperty property)
        {
            var trigger = new MultiTrigger();
            trigger.Conditions.Add(new Condition(KindProperty, CaptionKind.Close));
            trigger.Conditions.Add(new Condition(property, true));
            trigger.Setters.Add(
                Templates.Brush(Border.BackgroundProperty, ColorToken.Danger, FillPart)
            );
            trigger.Setters.Add(
                Templates.Brush(SymbolIcon.ForegroundProperty, ColorToken.OnDanger, IconPart)
            );
            return trigger;
        }

        return Templates.Seal(
            typeof(CaptionButton),
            fill,
            Hot(IsMouseOverProperty),
            Hot(IsFrameHotProperty),
            Hot(ButtonBase.IsPressedProperty),
            Red(IsMouseOverProperty),
            Red(ButtonBase.IsPressedProperty)
        );
    }
}
