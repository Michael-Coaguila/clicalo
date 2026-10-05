using System.Collections.Immutable;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Library;

/// <summary>The checks behind the invariants of <see cref="ShortcutLibrary"/> (blueprint §6.2).</summary>
internal static class LibraryRules
{
    /// <summary>Every broken invariant of the given lists, in a stable order.</summary>
    public static ImmutableArray<LibraryViolation> FindViolations(
        ValueList<Shortcut> alwaysVisible,
        ValueList<Profile> profiles
    )
    {
        var violations = ImmutableArray.CreateBuilder<LibraryViolation>();
        var profileIds = new HashSet<string>(StringComparer.Ordinal);
        var general = 0;
        foreach (var profile in profiles)
        {
            ArgumentNullException.ThrowIfNull(profile, nameof(profiles));
            if (string.IsNullOrEmpty(profile.Id.Value))
            {
                violations.Add(new(LibraryInvariant.UniqueIds, "profile with an empty id"));
            }
            else if (!profileIds.Add(profile.Id.Value))
            {
                violations.Add(new(LibraryInvariant.UniqueIds, "profile " + profile.Id.Value));
            }

            if (profile.Id == ProfileId.General)
            {
                general++;
                if (HasProcesses(profile.Binding))
                {
                    violations.Add(
                        new(LibraryInvariant.GeneralUnbound, "profile " + profile.Id.Value)
                    );
                }
            }
        }

        if (general == 0)
        {
            violations.Add(
                new(LibraryInvariant.FixedListsExist, "profile " + ProfileId.General.Value)
            );
        }

        CheckShortcuts(alwaysVisible, profiles, profileIds, violations);
        CheckProcesses(profiles, violations);
        return violations.ToImmutable();
    }

    /// <summary>Whether every step of a macro action has a valid type and every wait is in range (I6).</summary>
    public static bool HasValidSteps(ShortcutAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action is not MacroAction macro)
        {
            return true;
        }

        var range = Timings.Macro.MacroWaitRange;
        foreach (var step in macro.Steps)
        {
            var valid = step switch
            {
                KeysStep keys => keys.Chord is not null,
                WaitStep wait => wait.Duration >= range.Min && wait.Duration <= range.Max,
                TextStep text => text.Text is not null,
                MouseStep mouse => Enum.IsDefined(mouse.Op),
                _ => false,
            };
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether a binding follows at least one process.</summary>
    public static bool HasProcesses(AppBinding binding) =>
        binding is AppBinding.Processes { Names.IsEmpty: false };

    /// <summary>The processes a binding follows (none for a manual binding).</summary>
    public static ValueList<ProcessName> ProcessesOf(AppBinding binding) =>
        binding is AppBinding.Processes processes ? processes.Names : [];

    /// <summary>Whether a profile name has text in at least one language.</summary>
    public static bool HasName(LocalizedText name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (var pair in name.Values)
        {
            if (!string.IsNullOrWhiteSpace(pair.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static void CheckShortcuts(
        ValueList<Shortcut> alwaysVisible,
        ValueList<Profile> profiles,
        HashSet<string> profileIds,
        ImmutableArray<LibraryViolation>.Builder violations
    )
    {
        // Id → the list it was first seen in ("" for Always visible), to tell I1 from I2.
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        Check(alwaysVisible, string.Empty);
        foreach (var profile in profiles)
        {
            Check(profile.Shortcuts, profile.Id.Value ?? string.Empty);
        }

        void Check(ValueList<Shortcut> list, string owner)
        {
            foreach (var shortcut in list)
            {
                ArgumentNullException.ThrowIfNull(shortcut, nameof(list));
                ArgumentNullException.ThrowIfNull(shortcut.Action, nameof(list));
                var id = shortcut.Id.Value;
                if (string.IsNullOrEmpty(id))
                {
                    violations.Add(new(LibraryInvariant.UniqueIds, "shortcut with an empty id"));
                }
                else if (profileIds.Contains(id))
                {
                    violations.Add(new(LibraryInvariant.UniqueIds, "shortcut " + id));
                }
                else if (owners.TryGetValue(id, out var first))
                {
                    violations.Add(
                        string.Equals(first, owner, StringComparison.Ordinal)
                            ? new(LibraryInvariant.UniqueIds, "shortcut " + id)
                            : new(LibraryInvariant.SingleList, "shortcut " + id)
                    );
                }
                else
                {
                    owners.Add(id, owner);
                }

                if (!HasValidSteps(shortcut.Action))
                {
                    violations.Add(new(LibraryInvariant.MacroStepsValid, "shortcut " + id));
                }
            }
        }
    }

    private static void CheckProcesses(
        ValueList<Profile> profiles,
        ImmutableArray<LibraryViolation>.Builder violations
    )
    {
        var owners = new HashSet<ProcessName>();
        foreach (var profile in profiles)
        {
            foreach (var process in ProcessesOf(profile.Binding))
            {
                if (process.IsEmpty || !owners.Add(process))
                {
                    violations.Add(
                        new(LibraryInvariant.ProcessOwnedOnce, "profile " + profile.Id.Value)
                    );
                }
            }
        }
    }
}
