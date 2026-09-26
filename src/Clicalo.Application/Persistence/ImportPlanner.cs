using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Persistence;

/// <summary>
/// The pure part of importing (COP-002, DAT-004, DAT-007): the plan a command then applies with undo (blueprint §6.3).
/// Nothing imported is executed (LOG-006); only the library changes, settings and Frequents stay the user's.
/// </summary>
/// <remarks>
/// <b>Merge</b>: profiles and shortcuts that are missing are added; when an id matches, the existing one wins. An
/// imported shortcut identical to the one already in the same list is skipped; one whose id is taken by something
/// different is added with a new id, never lost. A process already bound to another profile keeps its binding (I5).
/// <b>Replace</b> swaps the library and is destructive: its command needs two taps and a <c>pre-import</c> backup.
/// </remarks>
public static class ImportPlanner
{
    /// <summary>Merges <paramref name="imported"/> into <paramref name="current"/>.</summary>
    /// <param name="current">The user's document.</param>
    /// <param name="imported">The validated imported document.</param>
    /// <param name="ids">New ids for what collides.</param>
    public static Result<ImportPlan> Merge(
        UserDocument current,
        UserDocument imported,
        IIdGenerator ids
    )
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(imported);
        ArgumentNullException.ThrowIfNull(ids);
        var merge = new Merger(current, ids);
        var always = merge.Into(current.Library.AlwaysVisible, imported.Library.AlwaysVisible);
        var profiles = current.Library.Profiles.ToList();
        foreach (var profile in imported.Library.Profiles)
        {
            var index = profiles.FindIndex(p => p.Id == profile.Id);
            if (index >= 0)
            {
                profiles[index] = profiles[index] with
                {
                    Shortcuts = merge.Into(profiles[index].Shortcuts, profile.Shortcuts),
                };
            }
            else
            {
                profiles.Add(merge.NewProfile(profile));
            }
        }

        return ShortcutLibrary
            .CreateValidated(always, ValueListBuilder.From(profiles))
            .Map(library => new ImportPlan(current with { Library = library }, merge.Summary()));
    }

    /// <summary>Replaces the library of <paramref name="current"/> with the imported one (destructive).</summary>
    /// <param name="current">The user's document.</param>
    /// <param name="imported">The validated imported document.</param>
    public static Result<ImportPlan> Replace(UserDocument current, UserDocument imported)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(imported);
        var library = imported.Library;
        return Results.Ok(
            new ImportPlan(
                current with
                {
                    Library = library,
                },
                new ImportSummary(
                    library.Profiles.Count,
                    library.AlwaysVisible.Count + library.Profiles.Sum(p => p.Shortcuts.Count),
                    0,
                    0,
                    0
                )
            )
        );
    }

    /// <summary>Installs a shared profile (DAT-007) whose ids are already new, after its preview.</summary>
    /// <param name="current">The user's document.</param>
    /// <param name="shared">The profile read from the shared file.</param>
    /// <param name="ids">New ids for what collides.</param>
    public static Result<ImportPlan> AddProfile(
        UserDocument current,
        Profile shared,
        IIdGenerator ids
    )
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(ids);
        var merge = new Merger(current, ids);
        var profiles = current.Library.Profiles.ToList();
        profiles.Add(merge.NewProfile(shared));
        return ShortcutLibrary
            .CreateValidated(current.Library.AlwaysVisible, ValueListBuilder.From(profiles))
            .Map(library => new ImportPlan(current with { Library = library }, merge.Summary()));
    }

    /// <summary>The merge rules over one document.</summary>
    private sealed class Merger
    {
        private readonly IIdGenerator _ids;
        private readonly HashSet<string> _taken = new(StringComparer.Ordinal);
        private readonly HashSet<ProcessName> _bound = [];
        private int _profilesAdded;
        private int _added;
        private int _renamed;
        private int _kept;
        private int _bindingsDropped;

        public Merger(UserDocument current, IIdGenerator ids)
        {
            _ids = ids;
            foreach (var shortcut in current.Library.AlwaysVisible)
            {
                _ = _taken.Add(shortcut.Id.Value);
            }

            foreach (var profile in current.Library.Profiles)
            {
                _ = _taken.Add(profile.Id.Value);
                foreach (var shortcut in profile.Shortcuts)
                {
                    _ = _taken.Add(shortcut.Id.Value);
                }

                if (profile.Binding is AppBinding.Processes processes)
                {
                    _bound.UnionWith(processes.Names);
                }
            }
        }

        public ValueList<Shortcut> Into(ValueList<Shortcut> target, ValueList<Shortcut> source)
        {
            var result = target.ToList();
            foreach (var shortcut in source)
            {
                var sameInList = result.FirstOrDefault(s => s.Id == shortcut.Id);
                if (sameInList is not null && sameInList.Equals(shortcut))
                {
                    _kept++;
                    continue;
                }

                result.Add(Claim(shortcut));
            }

            return ValueListBuilder.From(result);
        }

        public Profile NewProfile(Profile profile)
        {
            _profilesAdded++;
            var id = _taken.Add(profile.Id.Value) ? profile.Id : _ids.NewProfileId();
            _ = _taken.Add(id.Value);
            var binding = profile.Binding;
            if (binding is AppBinding.Processes processes)
            {
                var kept = processes.Names.Where(name => _bound.Add(name)).ToList();
                _bindingsDropped += processes.Names.Count - kept.Count;
                binding = new AppBinding.Processes(ValueListBuilder.From(kept));
            }

            return profile with
            {
                Id = id,
                Binding = binding,
                Shortcuts = ValueListBuilder.From(profile.Shortcuts.Select(Claim)),
            };
        }

        public ImportSummary Summary() =>
            new(_profilesAdded, _added, _renamed, _kept, _bindingsDropped);

        private Shortcut Claim(Shortcut shortcut)
        {
            _added++;
            if (_taken.Add(shortcut.Id.Value))
            {
                return shortcut;
            }

            _renamed++;
            var id = _ids.NewShortcutId();
            _ = _taken.Add(id.Value);
            return shortcut with { Id = id };
        }
    }
}
