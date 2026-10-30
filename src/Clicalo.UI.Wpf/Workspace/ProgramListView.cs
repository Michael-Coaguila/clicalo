using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Presentation.ControlCenter.Editor;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace;

/// <summary>
/// The installed programs of «Elegir programa» (EDI-014): a line that says they are being read the first time, a
/// search filter with its dictation button, and one choice per program in an area the finger scrolls. A computer has
/// hundreds of programs, so the choices are built once per list and the filter only hides and shows them: typing a
/// letter creates no button. The editor keeps one of these for its whole life.
/// </summary>
public sealed class ProgramListView : StackPanel
{
    /// <summary>The height of the list: four rows of choices, so the editor below it stays in reach.</summary>
    private const double ListHeight = (ControlHeight.Small + Space.S2) * 4;

    private readonly Action<string> _pick;
    private readonly TextInput _search;
    private readonly DockPanel _searchRow;
    private readonly StyledText _label;
    private readonly StyledText _note;
    private readonly LiveAnnouncer _noteAnnouncer;
    private readonly WrapPanel _choices;
    private readonly ThemedScrollViewer _scroll;
    private readonly List<(ProgramChip Program, OptionButton Button)> _buttons = [];
    private int _version = -1;

    /// <summary>Creates the list, empty.</summary>
    /// <param name="filter">The filter changed: what it holds now.</param>
    /// <param name="pick">A program was chosen: what the App field gets.</param>
    /// <param name="dictate">The dictation button: the box that gets the keyboard and the dictation.</param>
    internal ProgramListView(Action<string> filter, Action<string> pick, Action<TextBox> dictate)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(pick);
        ArgumentNullException.ThrowIfNull(dictate);
        _pick = pick;
        _label = new StyledText(string.Empty, TextStyleToken.Caption, ColorToken.Muted)
        {
            Margin = new Thickness(0, 0, 0, Space.S2),
        };
        _search = new TextInput(string.Empty, string.Empty) { Symbol = "search" };
        _search.Changed += (_, _) => filter(_search.Text);
        _search.DictateRequested += (_, _) => dictate(_search.Box);
        _searchRow = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, 0, 0, Space.S2),
        };
        _searchRow.Children.Add(_search);
        _note = new StyledText(string.Empty, TextStyleToken.BodySmall, ColorToken.Muted)
        {
            MaxLines = 0,
        };
        _noteAnnouncer = new LiveAnnouncer(_note);
        _choices = new WrapPanel { Margin = new Thickness(0, 0, -Space.S2, -Space.S2) };
        _scroll = new ThemedScrollViewer { Content = _choices, MaxHeight = ListHeight };
        Children.Add(_label);
        Children.Add(_searchRow);
        Children.Add(_note);
        Children.Add(_scroll);
    }

    /// <summary>The choices built for the list on show, in its order, hidden ones included.</summary>
    public IReadOnlyList<ToggleButton> Buttons => [.. _buttons.Select(static b => b.Button)];

    /// <summary>The names of the programs the filter leaves on show, in order.</summary>
    public IReadOnlyList<string> ShownNames =>
        [
            .. _buttons
                .Where(static b => b.Button.Visibility == Visibility.Visible)
                .Select(static b => b.Program.Name),
        ];

    /// <summary>The box of the search filter.</summary>
    public TextBox SearchBox => _search.Box;

    /// <summary>The line on show: «Leyendo los programas instalados…», «Nada coincide…» or nothing.</summary>
    public string Note => _note.Visibility == Visibility.Visible ? _note.Text : string.Empty;

    /// <summary>What the line announced last, and how (ACC-001).</summary>
    public LiveAnnouncer NoteAnnouncer => _noteAnnouncer;

    /// <summary>Shows <paramref name="target"/>: builds the choices only for another list, then filters and marks.</summary>
    /// <param name="target">The target of the App shortcut.</param>
    internal void Apply(TargetModel target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.ProgramsVersion != _version)
        {
            Build(target);
        }

        _label.Text = target.ProgramsLabel;
        AutomationProperties.SetName(_search.Box, target.ProgramSearch);
        _search.SetPlaceholder(target.ProgramSearch);
        _search.DictateName = target.DictateName;
        _search.Show(target.ProgramQuery);

        var filter = ProgramFilter.Fold(target.ProgramQuery);
        foreach (var (program, button) in _buttons)
        {
            var visibility = ProgramFilter.MatchesFolded(program, filter)
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (button.Visibility != visibility)
            {
                button.Visibility = visibility;
            }

            var chosen = string.Equals(program.Target, target.Value, StringComparison.Ordinal);
            if (button.IsChecked != chosen)
            {
                Ui.PaintChoice(button, chosen);
            }
        }

        var note = target.ProgramsLoading ?? target.ProgramsNoMatch;
        if (!string.Equals(_note.Text, note ?? string.Empty, StringComparison.Ordinal))
        {
            // ACC-001: the line stays, so «Leyendo los programas instalados…» and «Nada coincide…» are read out when
            // they appear; going back to nothing is not.
            _noteAnnouncer.Announce(note ?? string.Empty, AnnouncementUrgency.Polite);
        }

        _note.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
        var any = _buttons.Count > 0;
        _searchRow.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        _scroll.Visibility =
            any && target.ProgramsNoMatch is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Build(TargetModel target)
    {
        _version = target.ProgramsVersion;
        _buttons.Clear();
        _choices.Children.Clear();
        foreach (var program in target.Programs)
        {
            var button = new OptionButton
            {
                Kind = OptionKind.Chip,
                Content = program.Name,
                Margin = new Thickness(0, 0, Space.S2, Space.S2),
            };
            AutomationProperties.SetName(button, program.Name);
            button.Click += (_, _) => _pick(program.Target);
            _buttons.Add((program, button));
            _choices.Children.Add(button);
        }

        _scroll.ScrollToVerticalOffset(0);
    }
}
