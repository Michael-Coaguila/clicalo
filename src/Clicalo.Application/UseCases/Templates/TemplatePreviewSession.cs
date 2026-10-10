using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Sharing;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Templates;

/// <summary>
/// The preview of Plantillas (PLA-012 to PLA-017): a template, an AI proposal or a shared profile, with a checkbox and
/// an editable name per shortcut, and the install that follows. Nothing is written until the final button; every
/// install is one undoable step (REG-07) and gives every element a new id (DAT-004). An installed template is never
/// overwritten: the preview offers what is missing (PLA-013). Used by the Workspace role only.
/// </summary>
public sealed class TemplatePreviewSession
{
    private readonly DocumentStore _store;
    private readonly Dictionary<int, bool> _checks = [];
    private readonly Dictionary<int, string> _names = [];
    private ProfileTemplate? _template;
    private SharedProfile? _shared;

    /// <summary>Creates the session, with nothing in preview.</summary>
    /// <param name="store">The document.</param>
    public TemplatePreviewSession(DocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Where the content in preview comes from; null with nothing in preview.</summary>
    public PreviewSource? Source { get; private set; }

    /// <summary>Whether the AI does not know the program of the proposal in preview (PLA-007).</summary>
    public bool IsUnknown { get; private set; }

    /// <summary>The template in preview, its id for a template; null otherwise.</summary>
    public string? TemplateId => Source == PreviewSource.Template ? _template?.Id : null;

    /// <summary>Texts left out of the shared profile in preview (COP-005).</summary>
    public int UnavailableTexts => _shared?.UnavailableTexts ?? 0;

    private LangCode AppsLanguage => _store.Current.Settings.Keyboard.AppsLanguage;

    /// <summary>A template card (PLA-012): its shortcuts in preview, all checked.</summary>
    /// <param name="template">The template.</param>
    public void ShowTemplate(ProfileTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        Reset(PreviewSource.Template);
        _template = template;
    }

    /// <summary>An AI proposal (PLA-002, PLA-007): in preview, not installed.</summary>
    /// <param name="proposal">The validated proposal.</param>
    public void ShowAi(AiTemplate proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        Reset(PreviewSource.Ai);
        _template = proposal.Template;
        IsUnknown = !proposal.Known;
    }

    /// <summary>
    /// An imported profile (PLA-014): in preview without installing ([importedPv]); its Web, App, Macro and Text
    /// shortcuts start unchecked (LOG-008).
    /// </summary>
    /// <param name="shared">The profile read from the file.</param>
    public void ShowShared(SharedProfile shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        Reset(PreviewSource.Shared);
        _shared = shared;
    }

    /// <summary>Clears the preview.</summary>
    public void Clear() => Reset(null);

    /// <summary>
    /// The profile the preview shows, as it would be created: name, icon, processes and the shortcuts with the
    /// combination of the programs language (PLA-009). Null with nothing in preview.
    /// </summary>
    public Profile? Draft()
    {
        switch (Source)
        {
            case PreviewSource.Template or PreviewSource.Ai when _template is { } template:
                var library = _store.Current.Library;
                var profile = TemplateInstaller.CreateProfile(
                    template,
                    AppsLanguage,
                    new PreviewIds(),
                    process => library.ProfileFor(process) is not null
                );
                return Source == PreviewSource.Ai
                    ? profile with
                    {
                        Origin = null,
                        Shortcuts =
                        [
                            .. profile.Shortcuts.Items.Select(s => s with { Origin = null }),
                        ],
                    }
                    : profile;
            case PreviewSource.Shared when _shared is { } shared:
                return shared.Profile;
            default:
                return null;
        }
    }

    /// <summary>The profile installed from the template in preview (PLA-013), or null.</summary>
    public Profile? Installed() =>
        Source == PreviewSource.Template && _template is { } template
            ? TemplatePreviewRules.InstalledFrom(_store.Current.Library, template.Id)
            : null;

    /// <summary>Whether the template in preview has no review for the programs language ([onlyEs], PLA-015).</summary>
    public bool OnlyOtherLanguage =>
        Source == PreviewSource.Template
        && _template is { } template
        && !template.IsReviewedFor(AppsLanguage);

    /// <summary>The rows of the preview.</summary>
    public ImmutableArray<PreviewRow> Rows()
    {
        if (Draft() is not { } draft)
        {
            return [];
        }

        var installed = Installed();
        var rows = ImmutableArray.CreateBuilder<PreviewRow>(draft.Shortcuts.Count);
        var index = 0;
        foreach (var shortcut in draft.Shortcuts)
        {
            var alreadyIn =
                installed is not null && TemplatePreviewRules.IsAlreadyIn(installed, shortcut);
            var risky = Source == PreviewSource.Shared && TemplatePreviewRules.IsRisky(shortcut);
            var named = _names.TryGetValue(index, out var edit)
                ? shortcut with
                {
                    Name = TemplatePreviewRules.NameFor(shortcut.Name, edit),
                }
                : shortcut;
            var dangerous =
                shortcut.Action is TapAction tap && TemplateSchema.IsDangerous(tap.Chord);
            rows.Add(
                new PreviewRow(
                    index,
                    named,
                    alreadyIn,
                    !alreadyIn && (_checks.TryGetValue(index, out var on) ? on : !risky),
                    risky,
                    dangerous,
                    !ReferenceEquals(named, shortcut)
                )
            );
            index++;
        }

        return rows.MoveToImmutable();
    }

    /// <summary>The final button (PLA-017).</summary>
    public PreviewAction Action
    {
        get
        {
            var installed = Installed() is not null;
            var missing = installed ? Rows().Count(r => !r.AlreadyIn) : 0;
            return TemplatePreviewRules.ActionFor(installed, missing, Source == PreviewSource.Ai);
        }
    }

    /// <summary>N of the final button: the checked rows that are not installed yet.</summary>
    public int Count => Rows().Count(r => r.Checked);

    /// <summary>The checkbox of a row; a row that is already in cannot change.</summary>
    /// <param name="index">The row.</param>
    public void Toggle(int index)
    {
        var rows = Rows();
        if (index < 0 || index >= rows.Length || rows[index].AlreadyIn)
        {
            return;
        }

        _checks[index] = !rows[index].Checked;
    }

    /// <summary>✏ of a row (PLA-015): the name it installs with; an empty name goes back to the original.</summary>
    /// <param name="index">The row.</param>
    /// <param name="text">What the person typed or dictated.</param>
    public void Rename(int index, string text)
    {
        if ((text ?? string.Empty).Trim().Length == 0)
        {
            _ = _names.Remove(index);
        }
        else
        {
            _names[index] = text!;
        }
    }

    /// <summary>
    /// The final button (PLA-013, PLA-017): creates the profile with the checked shortcuts, or adds the checked missing
    /// ones to the installed template. For «Editar atajos» nothing changes and the installed profile is returned.
    /// </summary>
    /// <param name="uiLanguage">The interface language, for the names in the notice.</param>
    public Result<InstallOutcome> Install(LangCode uiLanguage)
    {
        if (Draft() is not { } draft)
        {
            return Results.Fail<InstallOutcome>(TemplateFailures.NothingToInstall());
        }

        var rows = Rows();
        var chosen = rows.Where(r => r.Checked).Select(r => r.Shortcut).ToImmutableArray();
        if (Installed() is { } installed)
        {
            if (chosen.IsEmpty)
            {
                return Results.Ok(new InstallOutcome(installed.Id, null));
            }

            var name = installed.Name.Get(uiLanguage, LangCode.Es);
            return _store
                .Dispatch(new AddShortcuts(installed.Id, chosen, name))
                .Map(_ => new InstallOutcome(
                    installed.Id,
                    L.AddedCountToProf(profile: name, count: chosen.Length)
                ));
        }

        if (chosen.IsEmpty)
        {
            return Results.Fail<InstallOutcome>(TemplateFailures.NothingToInstall());
        }

        var library = _store.Current.Library;
        var profile = draft with
        {
            Shortcuts = [.. chosen],
            Binding = FreeProcesses(draft.Binding, library),
        };
        var outcome = Create(profile, uiLanguage);
        if (outcome.IsSuccess)
        {
            Clear();
        }

        return outcome;
    }

    /// <summary>
    /// [Instalar] of a template card (PLA-011, PLA-012): installs all of it without preview, with undo. An installed
    /// template gets what is missing instead.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="uiLanguage">The interface language, for the notice.</param>
    public Result<InstallOutcome> InstallAll(ProfileTemplate template, LangCode uiLanguage)
    {
        ArgumentNullException.ThrowIfNull(template);
        var previous = (Source, _template, _shared, IsUnknown);
        var checks = new Dictionary<int, bool>(_checks);
        var names = new Dictionary<int, string>(_names);
        ShowTemplate(template);
        var result = Install(uiLanguage);
        if (previous.Source is not null && !ReferenceEquals(previous._template, template))
        {
            (Source, _template, _shared, IsUnknown) = previous;
            Restore(checks, names);
        }
        else if (result.IsSuccess)
        {
            Clear();
        }

        return result;
    }

    private static AppBinding FreeProcesses(AppBinding binding, ShortcutLibrary library)
    {
        if (binding is not AppBinding.Processes processes)
        {
            return binding;
        }

        var free = processes
            .Names.Items.Where(p => library.ProfileFor(p) is null)
            .ToImmutableArray();
        return free.IsEmpty
            ? new AppBinding.Manual()
            : new AppBinding.Processes(new ValueList<ProcessName>(free));
    }

    private Result<InstallOutcome> Create(Profile profile, LangCode uiLanguage) =>
        _store
            .Dispatch(new CreateProfile(profile, ListPosition.End))
            .Map(next => new InstallOutcome(
                next.Library.Profiles[next.Library.Profiles.Count - 1].Id,
                L.InstalledApp(app: profile.Name.Get(uiLanguage, LangCode.Es))
            ));

    private void Reset(PreviewSource? source)
    {
        Source = source;
        _template = null;
        _shared = null;
        IsUnknown = false;
        _checks.Clear();
        _names.Clear();
    }

    private void Restore(Dictionary<int, bool> checks, Dictionary<int, string> names)
    {
        _checks.Clear();
        _names.Clear();
        foreach (var (key, value) in checks)
        {
            _checks[key] = value;
        }

        foreach (var (key, value) in names)
        {
            _names[key] = value;
        }
    }

    /// <summary>Placeholder ids of the preview: the store gives the final ones when it installs (DAT-004).</summary>
    private sealed class PreviewIds : IIdGenerator
    {
        private int _next;

        public ProfileId NewProfileId() => new("preview");

        public ShortcutId NewShortcutId() =>
            new("preview-" + (++_next).ToString(CultureInfo.InvariantCulture));
    }
}
