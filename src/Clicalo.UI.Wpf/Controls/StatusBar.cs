using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The status bar of direction C (D12), at the bottom of the panel and of the Control Center: one row of
/// <see cref="ControlHeight.Small"/> on <c>side</c> with a line above it, the icon of its <see cref="Tone"/> (✓ in
/// <c>success</c> for a confirmation, never the information icon), the message in one line with «…»
/// (<c>bodySmall</c>) and, at the end, an optional <see cref="Action"/> (Deshacer, Repetir) of at least 44. It is
/// its own row: it never covers the content above it.
/// </summary>
/// <remarks>
/// <see cref="Message"/> is the text element a <c>LiveAnnouncer</c> reads when it changes (ACC-006). Texts are
/// product text, already localized (CLC0006).
/// </remarks>
public sealed class StatusBar : Border
{
    private readonly SymbolIcon _icon;
    private readonly ContentControl _action;
    private StatusTone _tone;

    /// <summary>Creates an empty, neutral status bar.</summary>
    public StatusBar()
    {
        MinHeight = ControlHeight.Small;
        Padding = new Thickness(Space.S6, 0, Space.S2, 0);
        BorderThickness = new Thickness(0, Stroke.Line, 0, 0);
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Side));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
        _icon = new SymbolIcon
        {
            Size = IconBox.Small,
            Margin = new Thickness(0, 0, Space.S2, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        Message = new StyledText(string.Empty, TextStyleToken.BodySmall)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        _action = new ContentControl
        {
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.S2, 0, 0, 0),
        };
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_icon, Dock.Left);
        DockPanel.SetDock(_action, Dock.Right);
        row.Children.Add(_icon);
        row.Children.Add(_action);
        row.Children.Add(Message);
        Child = row;
    }

    /// <summary>The message (one line with «…»): set its <c>Text</c>.</summary>
    public StyledText Message { get; }

    /// <summary>The text of the message.</summary>
    public string Text
    {
        get => Message.Text;
        set => Message.Text = value;
    }

    /// <summary>What the message says; it chooses the icon and its color.</summary>
    public StatusTone Tone
    {
        get => _tone;
        set
        {
            _tone = value;
            var (symbol, ink) = value switch
            {
                StatusTone.Success => ("check_circle", ColorToken.Success),
                StatusTone.Warning => ("warning", ColorToken.WarnText),
                StatusTone.Danger => ("error", ColorToken.DangerText),
                _ => ((string?)null, ColorToken.Muted),
            };
            _icon.Symbol = symbol;
            _icon.SetResourceReference(SymbolIcon.ForegroundProperty, ThemeBrushKey.For(ink));
            _icon.Visibility = symbol is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    /// <summary>The control at the end of the bar (a tertiary button or an icon button); null for none.</summary>
    public UIElement? Action
    {
        get => _action.Content as UIElement;
        set => _action.Content = value;
    }
}
