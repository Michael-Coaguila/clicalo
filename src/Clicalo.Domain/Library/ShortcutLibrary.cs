using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>
/// The shortcuts aggregate (the blueprint's <c>Library</c>, renamed so it does not clash with its namespace): the
/// Always visible row and the profiles. Built only through <see cref="CreateValidated"/> and changed only through
/// operations that return a new library or a <see cref="Failure"/>, so these invariants always hold (blueprint §6.2):
/// <list type="bullet">
/// <item>I1: ids are unique in the whole library.</item>
/// <item>I2: a shortcut lives in one list; <see cref="MoveShortcut"/> is atomic.</item>
/// <item>I3: General and Always visible exist and cannot be removed.</item>
/// <item>I4: General has no process.</item>
/// <item>I5: a process belongs to one profile, without distinguishing case (ATJ-007).</item>
/// <item>I6: macro steps have a valid type and waits are in range.</item>
/// </list>
/// Immutable, with value equality.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the domain package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class ShortcutLibrary : IEquatable<ShortcutLibrary>
{
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
    public Profile General => throw new NotImplementedException();

    /// <summary>
    /// Validates and builds a library: the only entry point for persistence, the seed and the v1 import. Fails with
    /// the first broken invariant; the persistence mapper repairs what is repairable before calling it (§6.5).
    /// </summary>
    /// <param name="alwaysVisible">The Always visible row.</param>
    /// <param name="profiles">The profiles in display order, General included.</param>
    public static Result<ShortcutLibrary> CreateValidated(
        ValueList<Shortcut> alwaysVisible,
        ValueList<Profile> profiles
    ) => throw new NotImplementedException();

    /// <summary>Finds where a shortcut lives, through a lazily built frozen index.</summary>
    /// <param name="id">The shortcut.</param>
    /// <param name="location">Its list and position.</param>
    public bool TryLocate(ShortcutId id, out ShortcutLocation location) =>
        throw new NotImplementedException();

    /// <summary>Finds a shortcut by id.</summary>
    /// <param name="id">The shortcut.</param>
    /// <param name="shortcut">The shortcut.</param>
    public bool TryGetShortcut(ShortcutId id, [NotNullWhen(true)] out Shortcut? shortcut) =>
        throw new NotImplementedException();

    /// <summary>Finds a profile by id.</summary>
    /// <param name="id">The profile.</param>
    /// <param name="profile">The profile.</param>
    public bool TryGetProfile(ProfileId id, [NotNullWhen(true)] out Profile? profile) =>
        throw new NotImplementedException();

    /// <summary>
    /// The first profile, in order, whose processes include <paramref name="process"/> without distinguishing case;
    /// General never matches (PER-002). <see langword="null"/> when none does.
    /// </summary>
    /// <param name="process">Executable in the foreground (Store apps already resolved to their process).</param>
    public Profile? ProfileFor(ProcessName process) => throw new NotImplementedException();

    /// <summary>Adds a shortcut with a new id to a list (I1, I2).</summary>
    /// <param name="list">Target list.</param>
    /// <param name="shortcut">The shortcut.</param>
    /// <param name="at">Position in the list.</param>
    public Result<ShortcutLibrary> AddShortcut(ListRef list, Shortcut shortcut, ListPosition at) =>
        throw new NotImplementedException();

    /// <summary>Replaces a shortcut with the same id in place.</summary>
    /// <param name="shortcut">The new version.</param>
    public Result<ShortcutLibrary> ReplaceShortcut(Shortcut shortcut) =>
        throw new NotImplementedException();

    /// <summary>Removes a shortcut (a destructive command wraps it, REG-04).</summary>
    /// <param name="id">The shortcut.</param>
    public Result<ShortcutLibrary> RemoveShortcut(ShortcutId id) =>
        throw new NotImplementedException();

    /// <summary>Moves a shortcut to another list or position, atomically (I2): pinning moves, never copies.</summary>
    /// <param name="id">The shortcut.</param>
    /// <param name="to">Target list.</param>
    /// <param name="at">Position in the target list.</param>
    public Result<ShortcutLibrary> MoveShortcut(ShortcutId id, ListRef to, ListPosition at) =>
        throw new NotImplementedException();

    /// <summary>Adds a profile with a new id.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="at">Position among the profiles.</param>
    public Result<ShortcutLibrary> AddProfile(Profile profile, ListPosition at) =>
        throw new NotImplementedException();

    /// <summary>Replaces a profile's name, icon, binding or mode, keeping its shortcuts and id (PER-008).</summary>
    /// <param name="profile">The new version.</param>
    public Result<ShortcutLibrary> ReplaceProfile(Profile profile) =>
        throw new NotImplementedException();

    /// <summary>Removes a profile and its shortcuts; fails for General (I3).</summary>
    /// <param name="id">The profile.</param>
    public Result<ShortcutLibrary> RemoveProfile(ProfileId id) =>
        throw new NotImplementedException();

    /// <summary>
    /// Binds a process to a profile (ATJ-007). Fails for General (I4). If another profile owns the process, fails
    /// unless <paramref name="takeOver"/>, which unbinds it there (I5; the UI asks first and retries).
    /// </summary>
    /// <param name="id">The profile.</param>
    /// <param name="process">The process.</param>
    /// <param name="takeOver">Whether to take the process from the profile that has it.</param>
    public Result<ShortcutLibrary> Bind(ProfileId id, ProcessName process, bool takeOver) =>
        throw new NotImplementedException();

    /// <summary>Unbinds a process from a profile.</summary>
    /// <param name="id">The profile.</param>
    /// <param name="process">The process.</param>
    public Result<ShortcutLibrary> Unbind(ProfileId id, ProcessName process) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public bool Equals(ShortcutLibrary? other) =>
        other is not null && AlwaysVisible == other.AlwaysVisible && Profiles == other.Profiles;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ShortcutLibrary);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(AlwaysVisible, Profiles);
}
