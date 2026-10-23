using Clicalo.Application.Localization;
using Clicalo.Domain.Messages;
using Clicalo.Domain.PanelLayout;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The first-time guide of the Full and Compact views (R-39), as the Tab view has its own (PES-015): «i / 3», the title
/// and the text of the step on show, [next] or [understood] and [coachSkip]. The steps follow the panel from what to show
/// to the tools: «Qué ver» ([co2t] and [co2d], the same row the bar's guide explains), the grid with its pages
/// ([pco2t], [pco2d]) and the header ([pco3t], [pco3d]). When it shows, and what [next] and [coachSkip] change, is the
/// composition's (<see cref="PanelCoachRules"/>).
/// </summary>
public sealed class PanelCoachViewModel : ObservableObject
{
    private static readonly (Message Title, Message Text)[] StepTexts =
    [
        (L.Co2t, L.Co2d),
        (L.Pco2t, L.Pco2d),
        (L.Pco3t, L.Pco3d),
    ];

    private readonly ILocalizationContext _localization;
    private readonly Action _next;
    private readonly Action _skip;
    private int _step;
    private bool _isShown;
    private string _name = string.Empty;
    private string _stepLabel = string.Empty;
    private string _title = string.Empty;
    private string _text = string.Empty;
    private string _nextLabel = string.Empty;
    private string _skipLabel = string.Empty;

    /// <summary>Creates the guide on its first step, hidden.</summary>
    /// <param name="localization">The interface language.</param>
    /// <param name="next">[next], or [understood] on the last step.</param>
    /// <param name="skip">[coachSkip].</param>
    public PanelCoachViewModel(ILocalizationContext localization, Action next, Action skip)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(skip);
        _localization = localization;
        _next = next;
        _skip = skip;
        Relocalize();
    }

    /// <summary>Whether the guide shows beside the panel.</summary>
    public bool IsShown
    {
        get => _isShown;
        private set => SetProperty(ref _isShown, value);
    }

    /// <summary>[panelCoachName]: the window of the guide for UI Automation, the same in every step.</summary>
    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    /// <summary>«i / 3».</summary>
    public string StepLabel
    {
        get => _stepLabel;
        private set => SetProperty(ref _stepLabel, value);
    }

    /// <summary>The title of the step.</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>The text of the step.</summary>
    public string Text
    {
        get => _text;
        private set => SetProperty(ref _text, value);
    }

    /// <summary>[next], or [understood] on the last step.</summary>
    public string NextLabel
    {
        get => _nextLabel;
        private set => SetProperty(ref _nextLabel, value);
    }

    /// <summary>[coachSkip].</summary>
    public string SkipLabel
    {
        get => _skipLabel;
        private set => SetProperty(ref _skipLabel, value);
    }

    /// <summary>
    /// The title and the text of every step, in order, in the current language: the guide keeps the size of the
    /// longest, so it never changes size from one step to the next (D12).
    /// </summary>
    public IReadOnlyList<(string Title, string Text)> Steps
    {
        get
        {
            var l = _localization.Current;
            return [.. StepTexts.Select(step => (l.Format(step.Title), l.Format(step.Text)))];
        }
    }

    /// <summary>Shows <paramref name="step"/>, or hides the guide.</summary>
    /// <param name="step">The step on show, from 0.</param>
    /// <param name="shown">Whether the guide shows beside the panel.</param>
    public void Apply(int step, bool shown)
    {
        _step = Math.Clamp(step, 0, PanelCoachRules.Steps - 1);
        Format();
        IsShown = shown;
    }

    /// <summary>Formats the texts again in the current language (IDI-001).</summary>
    public void Relocalize() => Format();

    /// <summary>[next], or [understood] on the last step.</summary>
    public void Next() => _next();

    /// <summary>[coachSkip].</summary>
    public void Skip() => _skip();

    private void Format()
    {
        var l = _localization.Current;
        Name = l.Format(L.PanelCoachName);
        SkipLabel = l.Format(L.CoachSkip);
        StepLabel = l.Format(L.CoachStep(index: _step + 1, total: PanelCoachRules.Steps));
        Title = l.Format(StepTexts[_step].Title);
        Text = l.Format(StepTexts[_step].Text);
        NextLabel = l.Format(PanelCoachRules.NextStep(_step) is null ? L.Understood : L.Next);
    }
}
