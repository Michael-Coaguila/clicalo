using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>
/// The shortcuts aggregate (the blueprint's <c>Library</c>, renamed so it does not clash with its namespace): the
/// Always visible row and the profiles. Built only through <see cref="CreateValidated"/> and changed only through
/// operations that return a new library or a <see cref="Failure"/>, so these invariants always hold (blueprint §6.2):
/// <list type="bullet">
/// <item>I1: ids are unique in the whole library, profiles and shortcuts together (DAT-004).</item>
/// <item>I2: a shortcut lives in one list; <see cref="MoveShortcut"/> is atomic.</item>
/// <item>I3: General and Always visible exist and cannot be removed.</item>
/// <item>I4: General has no process.</item>
/// <item>I5: a process belongs to one profile, without distinguishing case (ATJ-007).</item>
/// <item>I6: macro steps have a valid type and waits are in range.</item>
/// </list>
/// Immutable, with value equality. An operation that changes nothing returns the same instance, so the document store
/// sees no touched slice (§6.4).
/// </summary>
/// <remarks>
/// Failure codes: <c>library.shortcut.not_found</c>, <c>library.profile.not_found</c>, <c>library.id.duplicate</c>,
/// <c>library.id.empty</c>, <c>library.general.protected</c>, <c>library.general.unbound</c>,
/// <c>library.process.bound</c>, <c>library.process.empty</c>, <c>library.profile.name_empty</c> and
/// <c>library.invalid.&lt;invariant&gt;</c>. Positions outside a list are clamped to it.
/// </remarks>
public sealed class ShortcutLibrary : IEquatable<ShortcutLibrary>
{
    private ShortcutLibraryIndex? _index;

    private ShortcutLibrary(ValueList<Shortcut> alwaysVisible, ValueList<Profile> profiles)
    {
        AlwaysVisible = alwaysVisible;
        Profiles = profiles;
    }

    /// <summary>The Always visible row, in display order.</summary>
    public ValueList<Shortcut> AlwaysVisible { get; }

    /// <summary>The profiles in display order (docs/02 <c>order</c>); General is always one of them.</summary>
    public ValueList<Profile> Profiles { get; }

    /// <summary>The General profile (I3).</summary>
    public Profile General => Profiles[Index.Profiles[ProfileId.General]];

    private ShortcutLibraryIndex Index
    {
        get
        {
            var index = Volatile.Read(ref _index);
            if (index is not null)
            {
                return index;
            }

            var built = ShortcutLibraryIndex.Build(AlwaysVisible, Profiles);
            return Interlocked.CompareExchange(ref _index, built, null) ?? built;
        }
    }

    /// <summary>
    /// Validates and builds a library: the only entry point for persistence, the seed and imports. Fails with
    /// the first broken invariant; the persistence mapper repairs what is repairable before calling it (§6.5).
    /// </summary>
    /// <param name="alwaysVisible">The Always visible row.</param>
    /// <param name="profiles">The profiles in display order, General included.</param>
    /// <exception cref="ArgumentNullException">A list holds a <see langword="null"/> element (a defect).</exception>
    public static Result<ShortcutLibrary> CreateValidated(
        ValueList<Shortcut> alwaysVisible,
        ValueList<Profile> profiles
    )
    {
        var violations = LibraryRules.FindViolations(alwaysVisible, profiles);
        return violations.IsEmpty
            ? Results.Ok(new ShortcutLibrary(alwaysVisible, profiles))
            : Results.Fail<ShortcutLibrary>(LibraryFailures.Invalid(violations[0]));
    }

    /// <summary>
    /// Every broken invariant of the given lists, so the persistence mapper can report and repair them before
    /// <see cref="CreateValidated"/>.
    /// </summary>
    /// <param name="alwaysVisible">The Always visible row.</param>
    /// <param name="profiles">The profiles in display order.</param>
    public static ImmutableArray<LibraryViolation> FindViolations(
        ValueList<Shortcut> alwaysVisible,
        ValueList<Profile> profiles
    ) => LibraryRules.FindViolations(alwaysVisible, profiles);

    /// <summary>Every broken invariant of this library; always empty unless a defect broke one.</summary>
    public ImmutableArray<LibraryViolation> FindViolations() =>
        LibraryRules.FindViolations(AlwaysVisible, Profiles);

    /// <summary>Every shortcut with its location, Always visible first and then each profile in order.</summary>
    public IEnumerable<LocatedShortcut> EnumerateShortcuts()
    {
        var always = new ListRef.AlwaysVisible();
        for (var i = 0; i < AlwaysVisible.Count; i++)
        {
            yield return new LocatedShortcut(AlwaysVisible[i], new ShortcutLocation(always, i));
        }

        foreach (var profile in Profiles)
        {
            var list = new ListRef.InProfile(profile.Id);
            for (var i = 0; i < profile.Shortcuts.Count; i++)
            {
                yield return new LocatedShortcut(
                    profile.Shortcuts[i],
                    new ShortcutLocation(list, i)
                );
            }
        }
    }

    /// <summary>Whether a profile or a shortcut already uses <paramref name="id"/> (I1).</summary>
    /// <param name="id">The id text.</param>
    public bool ContainsId(string id) =>
        !string.IsNullOrEmpty(id)
        && (
            Index.Shortcuts.ContainsKey(new ShortcutId(id))
            || Index.Profiles.ContainsKey(new ProfileId(id))
        );

    /// <summary>Finds where a shortcut lives, through a lazily built frozen index.</summary>
    /// <param name="id">The shortcut.</param>
    /// <param name="location">Its list and position.</param>
    public bool TryLocate(ShortcutId id, out ShortcutLocation location) =>
        Index.Shortcuts.TryGetValue(id, out location);

    /// <summary>Finds a shortcut by id.</summary>
    /// <param name="id">The shortcut.</param>
    /// <param name="shortcut">The shortcut.</param>
    public bool TryGetShortcut(ShortcutId id, [NotNullWhen(true)] out Shortcut? shortcut)
    {
        if (TryLocate(id, out var location) && TryGetList(location.List, out var list))
        {
            shortcut = list[location.Index];
            return true;
        }

        shortcut = null;
        return false;
    }

    /// <summary>Finds a profile by id.</summary>
    /// <param name="id">The profile.</param>
    /// <param name="profile">The profile.</param>
    public bool TryGetProfile(ProfileId id, [NotNullWhen(true)] out Profile? profile)
    {
        if (Index.Profiles.TryGetValue(id, out var position))
        {
            profile = Profiles[position];
            return true;
        }

        profile = null;
        return false;
    }

    /// <summary>The shortcuts of a list, or <see langword="false"/> when the profile does not exist.</summary>
    /// <param name="list">The list.</param>
    /// <param name="shortcuts">Its shortcuts in display order.</param>
    public bool TryGetList(ListRef list, out ValueList<Shortcut> shortcuts)
    {
        ArgumentNullException.ThrowIfNull(list);
        switch (list)
        {
            case ListRef.AlwaysVisible:
                shortcuts = AlwaysVisible;
                return true;
            case ListRef.InProfile inProfile when TryGetProfile(inProfile.Id, out var profile):
                shortcuts = profile.Shortcuts;
                return true;
            default:
                shortcuts = default;
                return false;
        }
    }

    /// <summary>
    /// The first profile, in order, whose processes include <paramref name="process"/> without distinguishing case;
    /// General never matches (PER-002). <see langword="null"/> when none does.
    /// </summary>
    /// <param name="process">Executable in the foreground (Store apps already resolved to their process).</param>
    public Profile? ProfileFor(ProcessName process)
    {
        if (process.IsEmpty)
        {
            return null;
        }

        foreach (var profile in Profiles)
        {
            if (
                profile.Id != ProfileId.General
                && LibraryRules.ProcessesOf(profile.Binding).Contains(process)
            )
            {
                return profile;
            }
        }

        return null;
    }

    /// <summary>Adds a shortcut with a new id to a list (I1, I2).</summary>
    /// <param name="list">Target list.</param>
    /// <param name="shortcut">The shortcut.</param>
    /// <param name="at">Position in the list.</param>
    public Result<ShortcutLibrary> AddShortcut(ListRef list, Shortcut shortcut, ListPosition at)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(shortcut);
        ArgumentNullException.ThrowIfNull(shortcut.Action, nameof(shortcut));
        if (string.IsNullOrEmpty(shortcut.Id.Value))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.EmptyId());
        }

        if (ContainsId(shortcut.Id.Value))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.DuplicateId());
        }

        if (!LibraryRules.HasValidSteps(shortcut.Action))
        {
            return Results.Fail<ShortcutLibrary>(InvalidSteps(shortcut.Id));
        }

        if (!TryGetList(list, out var items))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ProfileNotFound());
        }

        return Results.Ok(WithList(list, Insert(items.Items, shortcut, at)));
    }

    /// <summary>Replaces a shortcut with the same id in place.</summary>
    /// <param name="shortcut">The new version.</param>
    public Result<ShortcutLibrary> ReplaceShortcut(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        ArgumentNullException.ThrowIfNull(shortcut.Action, nameof(shortcut));
        if (!TryLocate(shortcut.Id, out var location) || !TryGetList(location.List, out var items))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ShortcutNotFound());
        }

        if (!LibraryRules.HasValidSteps(shortcut.Action))
        {
            return Results.Fail<ShortcutLibrary>(InvalidSteps(shortcut.Id));
        }

        return items[location.Index].Equals(shortcut)
            ? Results.Ok(this)
            : Results.Ok(WithList(location.List, items.Items.SetItem(location.Index, shortcut)));
    }

    /// <summary>Removes a shortcut (a destructive command wraps it, REG-04).</summary>
    /// <param name="id">The shortcut.</param>
    public Result<ShortcutLibrary> RemoveShortcut(ShortcutId id)
    {
        if (!TryLocate(id, out var location) || !TryGetList(location.List, out var items))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ShortcutNotFound());
        }

        return Results.Ok(WithList(location.List, items.Items.RemoveAt(location.Index)));
    }

    /// <summary>Moves a shortcut to another list or position, atomically (I2): pinning moves, never copies.</summary>
    /// <param name="id">The shortcut.</param>
    /// <param name="to">Target list.</param>
    /// <param name="at">Position in the target list once the shortcut has left its current place.</param>
    public Result<ShortcutLibrary> MoveShortcut(ShortcutId id, ListRef to, ListPosition at)
    {
        ArgumentNullException.ThrowIfNull(to);
        if (!TryLocate(id, out var from) || !TryGetList(from.List, out var source))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ShortcutNotFound());
        }

        if (!TryGetList(to, out _))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ProfileNotFound());
        }

        var shortcut = source[from.Index];
        var without = source.Items.RemoveAt(from.Index);
        if (from.List.Equals(to))
        {
            var moved = Insert(without, shortcut, at);
            return moved.SequenceEqual(source.Items)
                ? Results.Ok(this)
                : Results.Ok(WithList(to, moved));
        }

        var left = WithList(from.List, without);
        left.TryGetList(to, out var target);
        return Results.Ok(left.WithList(to, Insert(target.Items, shortcut, at)));
    }

    /// <summary>Adds a profile with a new id.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="at">Position among the profiles.</param>
    public Result<ShortcutLibrary> AddProfile(Profile profile, ListPosition at)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrEmpty(profile.Id.Value))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.EmptyId());
        }

        if (ContainsId(profile.Id.Value))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.DuplicateId());
        }

        if (!LibraryRules.HasName(profile.Name))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ProfileNameEmpty());
        }

        var ids = new HashSet<string>(StringComparer.Ordinal) { profile.Id.Value };
        foreach (var shortcut in profile.Shortcuts)
        {
            ArgumentNullException.ThrowIfNull(shortcut, nameof(profile));
            ArgumentNullException.ThrowIfNull(shortcut.Action, nameof(profile));
            if (string.IsNullOrEmpty(shortcut.Id.Value))
            {
                return Results.Fail<ShortcutLibrary>(LibraryFailures.EmptyId());
            }

            if (!ids.Add(shortcut.Id.Value) || ContainsId(shortcut.Id.Value))
            {
                return Results.Fail<ShortcutLibrary>(LibraryFailures.DuplicateId());
            }

            if (!LibraryRules.HasValidSteps(shortcut.Action))
            {
                return Results.Fail<ShortcutLibrary>(InvalidSteps(shortcut.Id));
            }
        }

        if (CheckBinding(profile) is { } failure)
        {
            return Results.Fail<ShortcutLibrary>(failure);
        }

        return Results.Ok(
            new ShortcutLibrary(AlwaysVisible, new(Insert(Profiles.Items, profile, at)))
        );
    }

    /// <summary>Replaces a profile's name, icon, binding or mode, keeping its shortcuts and id (PER-008).</summary>
    /// <param name="profile">The new version; its shortcuts are ignored.</param>
    public Result<ShortcutLibrary> ReplaceProfile(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Index.Profiles.TryGetValue(profile.Id, out var position))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ProfileNotFound());
        }

        if (!LibraryRules.HasName(profile.Name))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ProfileNameEmpty());
        }

        if (CheckBinding(profile) is { } failure)
        {
            return Results.Fail<ShortcutLibrary>(failure);
        }

        var existing = Profiles[position];
        var updated = profile with { Shortcuts = existing.Shortcuts };
        return updated.Equals(existing)
            ? Results.Ok(this)
            : Results.Ok(
                new ShortcutLibrary(AlwaysVisible, new(Profiles.Items.SetItem(position, updated)))
            );
    }

    /// <summary>Moves a profile to another position among the profiles.</summary>
    /// <param name="id">The profile.</param>
    /// <param name="at">Position once the profile has left its current place.</param>
    public Result<ShortcutLibrary> MoveProfile(ProfileId id, ListPosition at)
    {
        if (!Index.Profiles.TryGetValue(id, out var position))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ProfileNotFound());
        }

        var moved = Insert(Profiles.Items.RemoveAt(position), Profiles[position], at);
        return moved.SequenceEqual(Profiles.Items)
            ? Results.Ok(this)
            : Results.Ok(new ShortcutLibrary(AlwaysVisible, new(moved)));
    }

    /// <summary>Removes a profile and its shortcuts; fails for General (I3).</summary>
    /// <param name="id">The profile.</param>
    public Result<ShortcutLibrary> RemoveProfile(ProfileId id)
    {
        if (id == ProfileId.General)
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.GeneralProtected());
        }

        if (!Index.Profiles.TryGetValue(id, out var position))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ProfileNotFound());
        }

        return Results.Ok(
            new ShortcutLibrary(AlwaysVisible, new(Profiles.Items.RemoveAt(position)))
        );
    }

    /// <summary>
    /// Binds a process to a profile (ATJ-007). Fails for General (I4). If another profile owns the process, fails
    /// unless <paramref name="takeOver"/>, which unbinds it there (I5; the UI asks first and retries).
    /// </summary>
    /// <param name="id">The profile.</param>
    /// <param name="process">The process.</param>
    /// <param name="takeOver">Whether to take the process from the profile that has it.</param>
    public Result<ShortcutLibrary> Bind(ProfileId id, ProcessName process, bool takeOver)
    {
        if (process.IsEmpty)
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ProcessEmpty());
        }

        if (id == ProfileId.General)
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.GeneralUnbound());
        }

        if (!Index.Profiles.TryGetValue(id, out var position))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ProfileNotFound());
        }

        var target = Profiles[position];
        var names = LibraryRules.ProcessesOf(target.Binding);
        if (names.Contains(process))
        {
            return Results.Ok(this);
        }

        var profiles = Profiles.Items;
        var owner = OwnerOf(process, except: id);
        if (owner is { } ownerPosition)
        {
            if (!takeOver)
            {
                return Results.Fail<ShortcutLibrary>(LibraryFailures.ProcessBound(process));
            }

            profiles = profiles.SetItem(ownerPosition, Without(Profiles[ownerPosition], process));
        }

        var bound = target with
        {
            Binding = new AppBinding.Processes(new(names.Items.Add(process))),
        };
        return Results.Ok(
            new ShortcutLibrary(AlwaysVisible, new(profiles.SetItem(position, bound)))
        );
    }

    /// <summary>Unbinds a process from a profile; a profile left without processes becomes manual.</summary>
    /// <param name="id">The profile.</param>
    /// <param name="process">The process.</param>
    public Result<ShortcutLibrary> Unbind(ProfileId id, ProcessName process)
    {
        if (!Index.Profiles.TryGetValue(id, out var position))
        {
            return Results.Fail<ShortcutLibrary>(LibraryFailures.ProfileNotFound());
        }

        var profile = Profiles[position];
        if (!LibraryRules.ProcessesOf(profile.Binding).Contains(process))
        {
            return Results.Ok(this);
        }

        return Results.Ok(
            new ShortcutLibrary(
                AlwaysVisible,
                new(Profiles.Items.SetItem(position, Without(profile, process)))
            )
        );
    }

    /// <inheritdoc />
    public bool Equals(ShortcutLibrary? other) =>
        other is not null
        && (
            ReferenceEquals(this, other)
            || (AlwaysVisible == other.AlwaysVisible && Profiles == other.Profiles)
        );

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ShortcutLibrary);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(AlwaysVisible, Profiles);

    private static ImmutableArray<T> Insert<T>(ImmutableArray<T> items, T item, ListPosition at)
    {
        var index = at.Index is { } requested
            ? Math.Clamp(requested, 0, items.Length)
            : items.Length;
        return items.Insert(index, item);
    }

    private static Profile Without(Profile profile, ProcessName process)
    {
        var remaining = LibraryRules.ProcessesOf(profile.Binding).Items.Remove(process);
        return profile with
        {
            Binding = remaining.IsEmpty
                ? new AppBinding.Manual()
                : new AppBinding.Processes(new(remaining)),
        };
    }

    private static Failure InvalidSteps(ShortcutId id) =>
        LibraryFailures.Invalid(new(LibraryInvariant.MacroStepsValid, "shortcut " + id.Value));

    private int? OwnerOf(ProcessName process, ProfileId except)
    {
        for (var i = 0; i < Profiles.Count; i++)
        {
            var profile = Profiles[i];
            if (profile.Id != except && LibraryRules.ProcessesOf(profile.Binding).Contains(process))
            {
                return i;
            }
        }

        return null;
    }

    private Failure? CheckBinding(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile.Binding, nameof(profile));
        var names = LibraryRules.ProcessesOf(profile.Binding);
        if (names.IsEmpty)
        {
            return null;
        }

        if (profile.Id == ProfileId.General)
        {
            return LibraryFailures.GeneralUnbound();
        }

        var seen = new HashSet<ProcessName>();
        foreach (var process in names)
        {
            if (process.IsEmpty)
            {
                return LibraryFailures.ProcessEmpty();
            }

            if (!seen.Add(process) || OwnerOf(process, except: profile.Id) is not null)
            {
                return LibraryFailures.ProcessBound(process);
            }
        }

        return null;
    }

    private ShortcutLibrary WithList(ListRef list, ImmutableArray<Shortcut> items)
    {
        if (list is ListRef.InProfile inProfile)
        {
            var position = Index.Profiles[inProfile.Id];
            var profile = Profiles[position] with { Shortcuts = new(items) };
            return new ShortcutLibrary(
                AlwaysVisible,
                new(Profiles.Items.SetItem(position, profile))
            );
        }

        return new ShortcutLibrary(new(items), Profiles);
    }
}
