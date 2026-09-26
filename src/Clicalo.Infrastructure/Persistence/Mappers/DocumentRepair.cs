using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Persistence.Dto;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>
/// Repairs what can be repaired before the Domain validates the document (blueprint §6.5 «reparable»): a missing or
/// blank id, a duplicate id (I1), a missing General (I3), a bound General (I4), a process owned by two profiles (I5), a
/// wait out of range (I6), a dangling <c>lastProfile</c> and a repeated pin. Dangling pins and hidden entries are kept
/// on purpose (FRE-005). Each repair is reported by a stable code; the original file is kept as a <c>pre-repair</c>
/// backup by the caller.
/// </summary>
internal static class DocumentRepair
{
    public const string MissingId = "repair.missing_id";
    public const string DuplicateId = "repair.duplicate_id";
    public const string GeneralMissing = "repair.general_missing";
    public const string GeneralBound = "repair.general_bound";
    public const string ProcessRebound = "repair.process_rebound";
    public const string WaitRange = "repair.wait_range";
    public const string LastProfile = "repair.last_profile";
    public const string DuplicatePin = "repair.duplicate_pin";

    private const string GeneralIcon = "apps";

    /// <summary>Repairs <paramref name="payload"/> and reports what changed.</summary>
    /// <param name="payload">The payload as read.</param>
    public static (PayloadDto Payload, ImmutableArray<string> Repairs) Apply(PayloadDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var repairs = new SortedSet<string>(StringComparer.Ordinal);
        var ids = new IdRegistry(payload);

        var profiles = new List<ProfileDto>();
        var generalSeen = false;
        foreach (var profile in payload.Profiles ?? [])
        {
            var id = ids.Claim(profile.Id, repairs);
            var repaired = profile with
            {
                Id = id,
                Shortcuts = RepairShortcuts(profile.Shortcuts, ids, repairs),
            };
            if (string.Equals(id, ProfileId.General.Value, StringComparison.Ordinal))
            {
                generalSeen = true;
                if (repaired.Processes is { Count: > 0 })
                {
                    repairs.Add(GeneralBound);
                    repaired = repaired with { Processes = null };
                }
            }

            profiles.Add(repaired);
        }

        if (!generalSeen)
        {
            repairs.Add(GeneralMissing);
            _ = ids.Claim(ProfileId.General.Value, repairs);
            profiles.Insert(
                0,
                new ProfileDto
                {
                    Id = ProfileId.General.Value,
                    Name = [],
                    Icon = GeneralIcon,
                    Shortcuts = [],
                }
            );
        }

        profiles = UniqueProcesses(profiles, repairs);
        var always = (payload.Always ?? new ShortcutListDto()) with
        {
            Shortcuts = RepairShortcuts(payload.Always?.Shortcuts, ids, repairs),
        };
        var settings = payload.Settings;
        if (
            settings?.LastProfile is { } last
            && !profiles.Any(p => string.Equals(p.Id, last, StringComparison.Ordinal))
        )
        {
            repairs.Add(LastProfile);
            settings = settings with { LastProfile = null };
        }

        var frequents = payload.Frequents;
        if (frequents is not null)
        {
            frequents = frequents with
            {
                Pins = Distinct(frequents.Pins, repairs),
                Hidden = Distinct(frequents.Hidden, repairs),
            };
        }

        var result = payload with
        {
            Profiles = profiles,
            Always = always,
            Settings = settings,
            Frequents = frequents,
        };
        return (result, [.. repairs]);
    }

    private static List<ShortcutDto> RepairShortcuts(
        List<ShortcutDto>? shortcuts,
        IdRegistry ids,
        SortedSet<string> repairs
    )
    {
        var result = new List<ShortcutDto>(shortcuts?.Count ?? 0);
        foreach (var shortcut in shortcuts ?? [])
        {
            var action = shortcut.Action;
            if (action?.Steps is { } steps)
            {
                action = action with
                {
                    Steps = [.. steps.Select(step => RepairStep(step, repairs))],
                };
            }

            result.Add(shortcut with { Id = ids.Claim(shortcut.Id, repairs), Action = action });
        }

        return result;
    }

    private static StepDto RepairStep(StepDto step, SortedSet<string> repairs)
    {
        if (!string.Equals(step.Kind, "wait", StringComparison.Ordinal))
        {
            return step;
        }

        var range = Timings.Macro.MacroWaitRange;
        var ms = step.Ms ?? Timings.Macro.MacroWaitDefault.TotalMilliseconds;
        var clamped = Math.Clamp(ms, range.Min.TotalMilliseconds, range.Max.TotalMilliseconds);
        if (step.Ms is null || !clamped.Equals(ms) || double.IsNaN(ms))
        {
            repairs.Add(WaitRange);
            return step with { Ms = double.IsNaN(ms) ? range.Min.TotalMilliseconds : clamped };
        }

        return step;
    }

    private static List<ProfileDto> UniqueProcesses(
        List<ProfileDto> profiles,
        SortedSet<string> repairs
    )
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ProfileDto>(profiles.Count);
        foreach (var profile in profiles)
        {
            if (profile.Processes is not { } processes)
            {
                result.Add(profile);
                continue;
            }

            var kept = new List<string>(processes.Count);
            foreach (var process in processes)
            {
                var name = new ProcessName(process).Value;
                if (name.Length == 0 || !owned.Add(name))
                {
                    repairs.Add(ProcessRebound);
                    continue;
                }

                kept.Add(name);
            }

            result.Add(profile with { Processes = kept });
        }

        return result;
    }

    private static List<string>? Distinct(List<string>? ids, SortedSet<string> repairs)
    {
        if (ids is null)
        {
            return null;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            if (seen.Add(id))
            {
                result.Add(id);
            }
            else
            {
                repairs.Add(DuplicatePin);
            }
        }

        return result;
    }

    /// <summary>The ids of the whole document (I1: one namespace for profiles and shortcuts).</summary>
    private sealed class IdRegistry
    {
        private readonly HashSet<string> _declared = new(StringComparer.Ordinal);
        private readonly HashSet<string> _claimed = new(StringComparer.Ordinal);

        public IdRegistry(PayloadDto payload)
        {
            foreach (var profile in payload.Profiles ?? [])
            {
                Declare(profile.Id);
                foreach (var shortcut in profile.Shortcuts ?? [])
                {
                    Declare(shortcut.Id);
                }
            }

            foreach (var shortcut in payload.Always?.Shortcuts ?? [])
            {
                Declare(shortcut.Id);
            }
        }

        public string Claim(string? id, SortedSet<string> repairs)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                repairs.Add(MissingId);
                return Fresh("r");
            }

            if (_claimed.Add(id))
            {
                return id;
            }

            repairs.Add(DuplicateId);
            return Fresh(id + "-r");
        }

        private void Declare(string? id)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                _ = _declared.Add(id);
            }
        }

        private string Fresh(string prefix)
        {
            for (var n = 1; ; n++)
            {
                var candidate = prefix + n.ToString(CultureInfo.InvariantCulture);
                if (!_declared.Contains(candidate) && _claimed.Add(candidate))
                {
                    return candidate;
                }
            }
        }
    }
}
