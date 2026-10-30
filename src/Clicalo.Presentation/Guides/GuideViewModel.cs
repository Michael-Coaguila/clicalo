using System.Collections.Immutable;
using Clicalo.Application.Localization;
using Clicalo.Domain.Guides;
using Clicalo.Domain.Messages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Guides;

/// <summary>
/// The first-time guide on show in one place (R-39): beside the panel, the bar or the bubble, or inside the Control
/// Center. The composition says which guide is due (<see cref="Show"/>) and what the person did
/// (<see cref="Signal"/>); this keeps where each guide is, goes on by itself when a step gets the action it waits for,
/// skips a step whose control is not on screen, and reports a guide finished or skipped so it is remembered. The card
/// shows «i / n», the title and the text of the step, [next] ([understood] on the last one) and [skip], and points at
/// <see cref="Target"/>.
/// </summary>
public sealed class GuideViewModel : ObservableObject
{
    private readonly ILocalizationContext _localization;
    private readonly Action<GuideId> _done;
    private readonly Dictionary<GuideId, int> _reached = [];
    private Func<GuideTarget, bool> _onScreen = static _ => true;
    private GuideId? _guide;
    private int _step;
    private GuideTarget? _target;
    private string _name = string.Empty;
    private string _stepLabel = string.Empty;
    private string _title = string.Empty;
    private string _text = string.Empty;
    private string _nextLabel = string.Empty;
    private string _skipLabel = string.Empty;
    private IReadOnlyList<(string Title, string Text)> _steps = [];
    private (GuideId Guide, string Language)? _stepsFor;

    /// <summary>Creates a guide with nothing on show.</summary>
    /// <param name="localization">The interface language.</param>
    /// <param name="done">A guide was finished or skipped: remember it.</param>
    public GuideViewModel(ILocalizationContext localization, Action<GuideId> done)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(done);
        _localization = localization;
        _done = done;
    }

    /// <summary>The guide on show, or <see langword="null"/>.</summary>
    public GuideId? Guide
    {
        get => _guide;
        private set
        {
            if (SetProperty(ref _guide, value))
            {
                OnPropertyChanged(nameof(IsShown));
            }
        }
    }

    /// <summary>Whether a guide is on show.</summary>
    public bool IsShown => _guide is not null;

    /// <summary>The step on show, from 0 among all the steps of the guide.</summary>
    public int Step
    {
        get => _step;
        private set => SetProperty(ref _step, value);
    }

    /// <summary>The control the step points at.</summary>
    public GuideTarget? Target
    {
        get => _target;
        private set => SetProperty(ref _target, value);
    }

    /// <summary>The name of the guide for UI Automation, the same in every step.</summary>
    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    /// <summary>«i / n», counting only the steps that can show now.</summary>
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

    /// <summary>The text of the step; a ★ is drawn as the star icon.</summary>
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

    /// <summary>[skip]: the guide ends and does not show again.</summary>
    public string SkipLabel
    {
        get => _skipLabel;
        private set => SetProperty(ref _skipLabel, value);
    }

    /// <summary>
    /// The title and the text of every step of the guide, in the current language: its card keeps the size of the
    /// longest, so it never changes size from one step to the next (D12).
    /// </summary>
    public IReadOnlyList<(string Title, string Text)> Steps
    {
        get => _steps;
        private set => SetProperty(ref _steps, value);
    }

    /// <summary>Shows <paramref name="guide"/> where it was left, or hides the card.</summary>
    /// <param name="guide">The guide due now, or <see langword="null"/>.</param>
    /// <param name="onScreen">
    /// Whether a control is on screen now; a step that only shows with its control is left out without it. Every control
    /// is when it is <see langword="null"/>.
    /// </param>
    public void Show(GuideId? guide, Func<GuideTarget, bool>? onScreen = null)
    {
        _onScreen = onScreen ?? (static _ => true);
        if (guide is not { } shown)
        {
            Guide = null;
            Target = null;
            return;
        }

        if (GuideRules.Showing(shown, _reached.GetValueOrDefault(shown), _onScreen) is not { } step)
        {
            Finish(shown);
            return;
        }

        _reached[shown] = step;
        Guide = shown;
        Step = step;
        Format();
    }

    /// <summary>
    /// What the person did: the step on show goes on by itself when it was waiting for it. Call it before
    /// <see cref="Show"/>, while the guide that waited is still the one on show.
    /// </summary>
    /// <param name="signal">The action.</param>
    public void Signal(GuideSignal signal)
    {
        if (_guide is { } guide && GuideRules.Completes(guide, _step, signal))
        {
            Next();
        }
    }

    /// <summary>[next]: the next step that can show, or the end of the guide after the last one.</summary>
    public void Next()
    {
        if (_guide is not { } guide)
        {
            return;
        }

        if (GuideRules.Showing(guide, _step + 1, _onScreen) is { } next)
        {
            _reached[guide] = next;
            Step = next;
            Format();
        }
        else
        {
            Finish(guide);
        }
    }

    /// <summary>[skip]: the guide ends, and is remembered as done.</summary>
    public void Skip()
    {
        if (_guide is { } guide)
        {
            Finish(guide);
        }
    }

    /// <summary>Formats the texts again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        if (_guide is not null)
        {
            Format();
        }
    }

    private void Finish(GuideId guide)
    {
        _ = _reached.Remove(guide);
        Guide = null;
        Target = null;
        _done(guide);
    }

    private void Format()
    {
        if (_guide is not { } guide)
        {
            return;
        }

        var l = _localization.Current;
        var steps = GuideCatalog.Steps(guide);
        var step = steps[_step];
        var shown = Applicable(steps);
        var position = shown.IndexOf(_step);
        Name = GuideCatalog.Section(guide) is { } section
            ? l.Format(L.GuideOf(name: l.Format(section)))
            : l.Format(GuideCatalog.Name(guide)!);
        StepLabel = l.Format(L.CoachStep(index: position + 1, total: shown.Length));
        Title = l.Format(step.Title);
        Text = l.Format(step.Text);
        NextLabel = l.Format(position + 1 < shown.Length ? L.Next : L.Understood);
        SkipLabel = l.Format(L.Skip);
        if (_stepsFor != (guide, l.Locale.Code))
        {
            _stepsFor = (guide, l.Locale.Code);
            Steps = [.. steps.Select(s => (l.Format(s.Title), l.Format(s.Text)))];
        }

        Target = step.Target;
    }

    /// <summary>The indexes of the steps that can show now.</summary>
    private ImmutableArray<int> Applicable(ImmutableArray<GuideStep> steps) =>
        [
            .. Enumerable
                .Range(0, steps.Length)
                .Where(i => !steps[i].OnlyWithTarget || _onScreen(steps[i].Target)),
        ];
}
