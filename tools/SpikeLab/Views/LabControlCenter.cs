using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Clicalo.Tools.SpikeLab.Views;

/// <summary>
/// The Control Center of the laboratory (S4 rows 9 and 10): a normal, activatable window opened and closed with the
/// <c>ControlCenter</c> lease, with three free-text fields («Nombre», «Texto» of several lines, «Web»), each with its
/// «Dictar» button (ACC-011, UIA010), and «Cerrar». The session gives the foreground back BEFORE the window closes, so
/// Windows never picks another window of SpikeLab in between. The texts are never read, only whether they are empty.
/// </summary>
internal sealed class LabControlCenter : Window
{
    private readonly TextBox[] _fields;
    private readonly Func<LabControlCenter, Task> _closing;
    private readonly Action<TextBox> _dictate;
    private readonly Action<TextBox> _focused;
    private bool _closeAllowed;

    /// <summary>Creates the window (not shown).</summary>
    /// <param name="closing">Runs when the maintainer closes it: gives the foreground back, then calls <see cref="CloseNow"/>.</param>
    /// <param name="dictate">«Dictar» next to a field: focus it and start Windows dictation.</param>
    /// <param name="focused">A field received the keyboard focus: show the touch keyboard.</param>
    public LabControlCenter(
        Func<LabControlCenter, Task> closing,
        Action<TextBox> dictate,
        Action<TextBox> focused
    )
    {
        _closing = closing;
        _dictate = dictate;
        _focused = focused;
        Title = "Centro de control de laboratorio";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontSize = 18;

        var layout = new StackPanel { Margin = new Thickness(16) };
        layout.Children.Add(
            new TextBlock
            {
                Text = "Rellena los tres campos sin teclado físico y ciérralo con «Cerrar».",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
            }
        );
        _fields =
        [
            AddField(layout, "Nombre", multiline: false),
            AddField(layout, "Texto", multiline: true),
            AddField(layout, "Web", multiline: false),
        ];

        var close = new Button
        {
            Content = "Cerrar",
            MinHeight = 56,
            MinWidth = 160,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        close.Click += (_, _) => Close();
        layout.Children.Add(close);
        Content = new ScrollViewer
        {
            Content = layout,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    /// <summary>The fields that have text (lengths only).</summary>
    public int FieldsWithText => _fields.Count(box => box.Text.Length > 0);

    /// <summary>The number of fields.</summary>
    public int FieldCount => _fields.Length;

    /// <summary>True once the window closed (it may close while its lease is still being requested).</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Gives the first field the keyboard focus (after the lease brought the window to the front).</summary>
    public void FocusFirstField() => Keyboard.Focus(_fields[0]);

    /// <summary>Closes the window without asking the session again.</summary>
    public void CloseNow()
    {
        _closeAllowed = true;
        Close();
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!_closeAllowed)
        {
            // Give the foreground back first; the session then calls CloseNow.
            e.Cancel = true;
            _ = _closing(this);
        }

        base.OnClosing(e);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        IsClosed = true;
        base.OnClosed(e);
    }

    private TextBox AddField(StackPanel layout, string name, bool multiline)
    {
        var label = new TextBlock { Text = name, Margin = new Thickness(0, 8, 0, 2) };
        var field = new TextBox
        {
            MinHeight = multiline ? 120 : 48,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
        };
        AutomationProperties.SetName(field, name);
        AutomationProperties.SetLabeledBy(field, label);
        field.GotKeyboardFocus += (_, _) => _focused(field);

        var dictate = new Button
        {
            Content = "🎤 Dictar",
            MinHeight = 48,
            MinWidth = 120,
            Margin = new Thickness(8, 0, 0, 0),
        };
        // UIA010: the sibling button of every free-text field is called «Dictar».
        AutomationProperties.SetName(dictate, "Dictar");
        dictate.Click += (_, _) => _dictate(field);

        var row = new DockPanel();
        DockPanel.SetDock(dictate, Dock.Right);
        row.Children.Add(dictate);
        row.Children.Add(field);
        layout.Children.Add(label);
        layout.Children.Add(row);
        return field;
    }
}
