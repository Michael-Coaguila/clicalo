using System.Collections.Immutable;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
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
/// when it is passed with other answers; a repeated welcome applies them only if the answers changed (BIE-010);</item>
/// <item>[Empezar] installs what step 2 marks (<see cref="InstallStarterKit"/>), marks the welcome finished and
/// ends <see cref="WelcomeEnd.Finished"/>;</item>
/// <item>[Omitir] (or Alt+F4) keeps what was applied, installs the default of the kit («Basics» only) on a first
/// welcome and nothing on a repeated one, marks it finished and ends <see cref="WelcomeEnd.Skipped"/>.</item>
/// </list>
/// Installing only adds (<see cref="WelcomeKit"/>): unmarking something installed never uninstalls it (REG-08). Not
/// thread-safe: the UI thread owns it.
/// </summary>
public sealed class WelcomeSession
{
    /// <summary>The number of steps (the five progress bars).</summary>
    public const int StepCount = 5;

    private readonly DocumentStore _store;
    private readonly ImmutableHashSet<WelcomeUse> _initialUses;

    /// <summary>Creates the session on the document of <paramref name="store"/>.</summary>
    /// <param name="store">The document.</param>
    /// <param name="content">The kit, the seed and the templates; without it step 2 offers nothing.</param>
    /// <param name="repeat">
    /// Whether it was opened from General › Ver la bienvenida otra vez; otherwise it is the welcome of a first start.
    /// </param>
    public WelcomeSession(DocumentStore store, StarterContent? content, bool repeat)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        Content = content;
        Repeat = repeat;
        var document = store.Current;
        _initialUses = repeat ? WelcomeEffects.Read(document.Settings) : [];
        Uses = _initialUses;
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

    /// <summary>Whether it was opened again from General.</summary>
    public bool Repeat { get; }

    /// <summary>The step in view, from 0 to 4.</summary>
    public int Step { get; private set; }

    /// <summary>Whether it ended.</summary>
    public bool HasEnded { get; private set; }

    /// <summary>The marked options of step 1.</summary>
    public ImmutableHashSet<WelcomeUse> Uses { get; private set; }

    /// <summary>The marked options of step 2.</summary>
    public StarterSelection Kit { get; private set; }

    /// <summary>The options of step 2 already installed when it opened.</summary>
    public StarterSelection Installed { get; }

    /// <summary>The current settings.</summary>
    public UserSettings Settings => _store.Current.Settings;

    /// <summary>Whether this is the last step: [Siguiente] says [Empezar].</summary>
    public bool IsLastStep => Step == StepCount - 1;

    /// <summary>A language button of step 0: the language changes at once in every window (BIE-004).</summary>
    /// <param name="language">The language.</param>
    public void SetLanguage(LangCode language) => Set(SettingPaths.Language, language);

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

        if (Step == 1 && (!Repeat || !Uses.SetEquals(_initialUses)))
        {
            _ = _store.Dispatch(new ApplyWelcomeUses(Uses));
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

    private void End(StarterSelection install, WelcomeEnd end)
    {
        HasEnded = true;
        if (Content is not null && !install.Chosen.IsEmpty)
        {
            _ = _store.Dispatch(new InstallStarterKit(Content, install));
        }

        _ = _store.Dispatch(new FinishOnboarding());
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
