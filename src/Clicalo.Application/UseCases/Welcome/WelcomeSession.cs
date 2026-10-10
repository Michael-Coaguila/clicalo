using System.Collections.Immutable;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Welcome;

/// <summary>
/// The welcome (docs/06, BIE-001 to BIE-010): five steps with [Atrás] · [Omitir] · [Siguiente]/[Empezar]. What it
/// applies and when:
/// <list type="bullet">
/// <item>language (step 0), view (step 3), size and theme (step 4) apply at once, as presentation settings;</item>
/// <item>the answers of step 1 apply when leaving it with [Siguiente] (<see cref="ApplyWelcomeUses"/>), and again
/// every time it is passed with answers other than the ones last applied (EC-BIE-01). A repeated welcome starts from
/// the recorded answers and only touches the settings the person did not change by hand since
/// (<see cref="PendingChanges"/>, BIE-010);</item>
/// <item>[Empezar] installs what step 2 marks (<see cref="InstallStarterKit"/>), marks the welcome finished and
/// ends <see cref="WelcomeEnd.Finished"/>;</item>
/// <item>[Omitir] (or Alt+F4) keeps what was applied, installs the default of the kit («Basics» only) on a first
/// welcome and nothing on a repeated one, marks it finished and ends <see cref="WelcomeEnd.Skipped"/>;</item>
/// <item>either way the answers and what they left in the settings are recorded in the document
/// (<see cref="FinishOnboarding.Answers"/>, ADR-0028).</item>
/// </list>
/// Installing only adds (<see cref="WelcomeKit"/>): unmarking something installed never uninstalls it (REG-08). On the
/// first start of a new installation that found data from before (proposal P6), step 0 also offers to start from
/// scratch (<see cref="StartFromScratch"/>); keeping the data is the default. Not thread-safe: the UI thread owns it.
/// </summary>
public sealed class WelcomeSession
{
    /// <summary>The number of steps (the five progress bars).</summary>
    public const int StepCount = 5;

    /// <summary>
    /// The entry of the closed list of destructive operations that «Empezar de cero» is (REG-04): it runs as a
    /// <see cref="RestoreBackup"/> of the document of a new installation, with this as the target of its confirmation.
    /// </summary>
    public const string StartFromScratchOperation = "StartFromScratchOnReinstall";

    private readonly DocumentStore _store;
    private readonly WelcomeFreshStart? _fresh;
    private readonly TwoStepConfirm? _confirm;
    private readonly ValueList<string>? _recordedKit;
    private ImmutableHashSet<WelcomeUse>? _appliedUses;
    private WelcomeBaseline? _baseline;
    private DateTimeOffset _armedUntil;

    /// <summary>Creates the session on the document of <paramref name="store"/>.</summary>
    /// <param name="store">The document.</param>
    /// <param name="content">The kit, the seed and the templates; without it step 2 offers nothing.</param>
    /// <param name="repeat">
    /// Whether the document already finished a welcome (General › Ver la bienvenida otra vez, or a reinstallation);
    /// otherwise it is the welcome of a first start.
    /// </param>
    /// <param name="fresh">
    /// How to start from scratch, on the first start of a new installation that found data from before (P6);
    /// <see langword="null"/> everywhere else.
    /// </param>
    public WelcomeSession(
        DocumentStore store,
        StarterContent? content,
        bool repeat,
        WelcomeFreshStart? fresh = null
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _fresh = fresh;
        _confirm = fresh is null ? null : new TwoStepConfirm(fresh.Time);
        Content = content;
        Repeat = repeat;
        OffersFreshStart = fresh is not null;
        var document = store.Current;
        if (repeat)
        {
            // BIE-010: the recorded answers; a document that never recorded them shows what its settings reflect.
            var answers = document.Onboarding.Answers;
            _baseline = answers?.Baseline;
            _appliedUses = answers is null
                ? WelcomeEffects.Read(document.Settings)
                : WelcomeEffects.UsesOf(answers);
            _recordedKit = answers?.Kit;
        }

        Uses = _appliedUses ?? [];
        Installed = content is null
            ? StarterSelection.Empty
            : WelcomeKit.Installed(document.Library, content);
        Kit =
            content is null ? StarterSelection.Empty
            : repeat ? Installed
            : content.Kit.DefaultSelection;
    }

    /// <summary>Raised after every change of the session.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised once, when the welcome ends.</summary>
    public event EventHandler<WelcomeEndedEventArgs>? Ended;

    /// <summary>The kit, the seed and the templates, if they could be read.</summary>
    public StarterContent? Content { get; }

    /// <summary>Whether the document had already finished a welcome when this one opened.</summary>
    public bool Repeat { get; private set; }

    /// <summary>The step in view, from 0 to 4.</summary>
    public int Step { get; private set; }

    /// <summary>Whether it ended.</summary>
    public bool HasEnded { get; private set; }

    /// <summary>The marked options of step 1.</summary>
    public ImmutableHashSet<WelcomeUse> Uses { get; private set; }

    /// <summary>The marked options of step 2.</summary>
    public StarterSelection Kit { get; private set; }

    /// <summary>The options of step 2 already installed when it opened.</summary>
    public StarterSelection Installed { get; private set; }

    /// <summary>The current settings.</summary>
    public UserSettings Settings => _store.Current.Settings;

    /// <summary>Whether this is the last step: [Siguiente] says [Empezar].</summary>
    public bool IsLastStep => Step == StepCount - 1;

    /// <summary>
    /// Whether step 0 offers «Conservar mis datos» (the default) or «Empezar de cero»: the first start of a new
    /// installation that found data from before, until the person starts from scratch (P6).
    /// </summary>
    public bool OffersFreshStart { get; private set; }

    /// <summary>Whether the person started from scratch in this welcome.</summary>
    public bool StartedFresh { get; private set; }

    /// <summary>Until when the first tap of «Empezar de cero» is armed, or <see langword="null"/> (REG-04).</summary>
    public DateTimeOffset? FreshStartArmedUntil =>
        _confirm?.ArmedSubject is null ? null : _armedUntil;

    /// <summary>
    /// What [Siguiente] of step 1 will do to the settings on a repeated welcome (BIE-010): what changes and what stays
    /// as the person left it. <see langword="null"/> on a first welcome, outside step 1, and while the answers are the
    /// ones last applied (nothing changes then).
    /// </summary>
    public WelcomeStepPlan? PendingChanges =>
        Repeat && Step == 1 && !HasEnded && AnswersChanged
            ? WelcomeEffects.Plan(Settings, _baseline, Uses, HadTremor)
            : null;

    private bool AnswersChanged => _appliedUses is null || !Uses.SetEquals(_appliedUses);

    private bool HadTremor => _appliedUses?.Contains(WelcomeUse.Tremor) ?? false;

    /// <summary>A language button of step 0: the language changes at once in every window (BIE-004).</summary>
    /// <param name="language">The language.</param>
    public void SetLanguage(LangCode language) => Set(SettingPaths.Language, language);

    /// <summary>
    /// «Empezar de cero» of step 0 (P6, REG-04): the first tap arms, the second replaces the document with the one of
    /// a new installation in the same language, after a backup of the current one that Sistema › Copias de seguridad
    /// restores, and the welcome goes on as a first one. Nothing while it is not offered.
    /// </summary>
    public void StartFromScratch()
    {
        if (HasEnded || _fresh is null || _confirm is null || !OffersFreshStart)
        {
            return;
        }

        // The store only runs a destructive command with a token of its own operation (CLC0010).
        switch (
            _confirm.Tap(
                new ConfirmationSubject(nameof(RestoreBackup), StartFromScratchOperation)
            )
        )
        {
            case TwoStepResult.Armed armed:
                _armedUntil = armed.Until;
                break;
            case TwoStepResult.Confirmed confirmed:
                if (
                    _fresh.Create(_store.Current) is { } empty
                    && _store.Dispatch(new RestoreBackup(empty), confirmed.Token).IsSuccess
                )
                {
                    OffersFreshStart = false;
                    StartedFresh = true;
                    Repeat = false;
                    _baseline = null;
                    _appliedUses = null;
                    Uses = [];
                    Installed = StarterSelection.Empty;
                    Kit = Content?.Kit.DefaultSelection ?? StarterSelection.Empty;
                }

                break;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A chip of step 1.</summary>
    /// <param name="use">The option.</param>
    public void ToggleUse(WelcomeUse use)
    {
        if (HasEnded)
        {
            return;
        }

        var without = Uses.Remove(use);
        Uses = without.Count == Uses.Count ? Uses.Add(use) : without;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A chip of step 2.</summary>
    /// <param name="optionId">The option id.</param>
    public void ToggleKit(string optionId)
    {
        ArgumentNullException.ThrowIfNull(optionId);
        if (HasEnded)
        {
            return;
        }

        Kit = Kit.Toggle(optionId);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A card of step 3 (BIE-007).</summary>
    /// <param name="density">The view.</param>
    public void SetDensity(PanelDensity density) => Set(SettingPaths.Density, density);

    /// <summary>A size of step 4 (BIE-008).</summary>
    /// <param name="size">The size.</param>
    public void SetSize(PanelSize size) => Set(SettingPaths.Size, size);

    /// <summary>A theme of step 4 (BIE-008).</summary>
    /// <param name="theme">The theme.</param>
    public void SetTheme(ThemeChoice theme) => Set(SettingPaths.Theme, theme);

    /// <summary>[Siguiente], or [Empezar] on the last step.</summary>
    public void Next()
    {
        if (HasEnded)
        {
            return;
        }

        if (Step == 1 && AnswersChanged)
        {
            ApplyUses();
        }

        if (!IsLastStep)
        {
            Step++;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        End(Kit, WelcomeEnd.Finished);
    }

    /// <summary>[Atrás] (hidden on step 0).</summary>
    public void Back()
    {
        if (HasEnded || Step == 0)
        {
            return;
        }

        Step--;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>[Omitir] or Alt+F4 (BIE-001, BIE-003).</summary>
    public void Skip()
    {
        if (HasEnded)
        {
            return;
        }

        End(
            Repeat || Content is null ? StarterSelection.Empty : Content.Kit.DefaultSelection,
            WelcomeEnd.Skipped
        );
    }

    /// <summary>
    /// The effects of the answers, from what the welcome last left (EC-BIE-01: going back and passing the step with
    /// other answers recalculates them, also back to the answers it opened with).
    /// </summary>
    private void ApplyUses()
    {
        var plan = WelcomeEffects.Plan(Settings, _baseline, Uses, HadTremor);
        var applied = _store.Dispatch(
            new ApplyWelcomeUses(Uses) { Baseline = _baseline, HadTremor = HadTremor }
        );
        if (applied.IsSuccess)
        {
            _baseline = plan.Baseline;
            _appliedUses = Uses;
        }
    }

    private void End(StarterSelection install, WelcomeEnd end)
    {
        HasEnded = true;
        if (Content is not null && !install.Chosen.IsEmpty)
        {
            _ = _store.Dispatch(new InstallStarterKit(Content, install));
        }

        // BIE-010: what was answered and what it left in the settings, for the next time. A repeated welcome that is
        // skipped keeps the kit it had recorded.
        IEnumerable<string> kit =
            end != WelcomeEnd.Skipped || !Repeat ? install.Chosen
            : _recordedKit is { } recorded ? recorded.Items
            : Installed.Chosen;
        var answers = WelcomeAnswers.Create(
            WelcomeEffects.ToAnswers(_appliedUses ?? []),
            kit,
            _baseline ?? WelcomeBaseline.Of(Settings)
        );
        _ = _store.Dispatch(new FinishOnboarding { Answers = answers });
        Changed?.Invoke(this, EventArgs.Empty);
        Ended?.Invoke(this, new WelcomeEndedEventArgs(end));
    }

    private void Set(string path, object value)
    {
        if (HasEnded)
        {
            return;
        }

        _ = _store.Dispatch(new SetSetting(path, value));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
