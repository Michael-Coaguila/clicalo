using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;
using PanelSize = Clicalo.Domain.Settings.PanelSize;
using SettingsSchema = Clicalo.Domain.Settings.SettingsSchema;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// The pure decisions of the v1 conversion (catalog §7.4 and §7.5, MIG-002 to MIG-008): missing values take the v1
/// defaults, every button is kept (separators only in the report), and whatever has no direct equivalent becomes a
/// report line. Needs nothing from the model's factories, so its rules are testable on their own.
/// </summary>
internal static class V1Planner
{
    /// <summary>The name of the v1 General profile (its identity in v1; catalog §7.4).</summary>
    public const string GeneralName = "General";

    /// <summary>The v1 default <c>window_opacity</c> (catalog §7.2).</summary>
    public const double DefaultOpacity = 0.92;

    /// <summary>The v1 default button height (<c>button_size</c> [85, 62], catalog §7.2).</summary>
    public const int DefaultButtonHeight = 62;

    /// <summary>The highest v1 button height that becomes size S (catalog §7.4).</summary>
    public const int SmallMaxHeight = 66;

    /// <summary>The highest v1 button height that becomes size M; anything taller is L (catalog §7.4).</summary>
    public const int MediumMaxHeight = 78;

    /// <summary>The «Lock computer» system command and its icon (<c>data/catalogs/system-commands.json</c>).</summary>
    public static V1ActionPlan.SystemCommand LockComputer { get; } =
        new(new SystemCommandId("lock"), new IconRef("lock"));

    /// <summary>Plans the conversion of <paramref name="document"/>.</summary>
    /// <param name="document">The v1 document as read.</param>
    /// <param name="monitors">The monitors of this machine.</param>
    public static V1Plan Plan(V1Document document, ValueList<V1Monitor> monitors)
    {
        ArgumentNullException.ThrowIfNull(document);
        var notes = ImmutableArray.CreateBuilder<MigrationNote>();
        foreach (var key in document.UnknownKeys)
        {
            notes.Add(new MigrationNote(MigrationNoteKind.UnknownKey, null, null, key));
        }

        var profiles = PlanProfiles(document, notes, out var planIndexOfV1);
        var settings = PlanSettings(document, monitors, profiles, planIndexOfV1, notes);
        if (profiles.Any(static p => p.Process is not null))
        {
            notes.Add(new MigrationNote(MigrationNoteKind.ReturnsToGeneral, null, null, null));
        }

        return new V1Plan(
            new ValueList<V1ProfilePlan>(profiles.ToImmutable()),
            settings,
            new ValueList<MigrationNote>(notes.ToImmutable()),
            V1Counts.Of(document)
        );
    }

    private static ImmutableArray<V1ProfilePlan>.Builder PlanProfiles(
        V1Document document,
        ImmutableArray<MigrationNote>.Builder notes,
        out int[] planIndexOfV1
    )
    {
        var profiles = ImmutableArray.CreateBuilder<V1ProfilePlan>(document.Profiles.Count + 1);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasGeneral = false;
        foreach (var profile in document.Profiles)
        {
            var shortcuts = ImmutableArray.CreateBuilder<V1ShortcutPlan>(profile.Buttons.Count);
            foreach (var button in profile.Buttons)
            {
                if (button.Kind == V1ButtonKind.Separator)
                {
                    // Separators are not converted (PQ-09); the report keeps the count equal (MIG-004).
                    notes.Add(
                        new MigrationNote(
                            MigrationNoteKind.Separator,
                            profile.Name,
                            button.Label,
                            null
                        )
                    );
                    continue;
                }

                shortcuts.Add(PlanButton(profile.Name, button, notes));
            }

            if (profile.ButtonsPerPage is { } perPage)
            {
                notes.Add(
                    new MigrationNote(
                        MigrationNoteKind.ButtonsPerPage,
                        profile.Name,
                        null,
                        perPage.ToString(CultureInfo.InvariantCulture)
                    )
                );
            }

            var process = NormalizeProcess(profile.Process);
            var isGeneral =
                !hasGeneral && string.Equals(profile.Name, GeneralName, StringComparison.Ordinal);
            var name = profile.Name;
            if (isGeneral && process is not null)
            {
                // EC-MIG-04: General never has a process; its buttons and process move to «General (process)».
                notes.Add(
                    new MigrationNote(
                        MigrationNoteKind.GeneralHadProcess,
                        profile.Name,
                        null,
                        process
                    )
                );
                isGeneral = false;
                name = profile.Name + " (" + process + ")";
            }

            hasGeneral |= isGeneral;
            if (process is not null && !claimed.Add(process))
            {
                // I5: the first profile in order keeps the process, the others become manual.
                notes.Add(
                    new MigrationNote(
                        MigrationNoteKind.DuplicateProcess,
                        profile.Name,
                        null,
                        process
                    )
                );
                process = null;
            }

            profiles.Add(
                new V1ProfilePlan(
                    isGeneral,
                    IsCreated: false,
                    name,
                    process,
                    new ValueList<V1ShortcutPlan>(shortcuts.ToImmutable())
                )
            );
        }

        var offset = 0;
        if (!hasGeneral)
        {
            profiles.Insert(0, new V1ProfilePlan(true, IsCreated: true, GeneralName, null, []));
            notes.Add(new MigrationNote(MigrationNoteKind.GeneralCreated, null, null, null));
            offset = 1;
        }

        planIndexOfV1 = new int[document.Profiles.Count];
        for (var i = 0; i < planIndexOfV1.Length; i++)
        {
            planIndexOfV1[i] = i + offset;
        }

        return profiles;
    }

    private static V1ShortcutPlan PlanButton(
        string profile,
        V1Button button,
        ImmutableArray<MigrationNote>.Builder notes
    )
    {
        var color = V1Colors.Map(button.Color);
        if (color.IsReported)
        {
            notes.Add(
                new MigrationNote(MigrationNoteKind.HexColor, profile, button.Label, button.Color)
            );
        }

        V1ActionPlan action;
        MigrationNoteKind? review;
        string? original;
        switch (button.Kind)
        {
            case V1ButtonKind.Url:
                var (address, urlReview) = V1ActionTargets.Url(button.Action);
                (action, review, original) = (
                    new V1ActionPlan.Url(address),
                    urlReview,
                    button.Action
                );
                break;
            case V1ButtonKind.App:
                var (app, appReview) = V1ActionTargets.App(button.Action);
                (action, review, original) = (new V1ActionPlan.App(app), appReview, button.Action);
                break;
            default:
                if (button.Kind == V1ButtonKind.Unknown)
                {
                    // v1 sent any other type as a combination (overlay.py): so does the import, with a report line.
                    notes.Add(
                        new MigrationNote(
                            MigrationNoteKind.UnknownButton,
                            profile,
                            button.Label,
                            button.RawType
                        )
                    );
                }

                (action, review) = PlanKeys(button.Hotkey ?? string.Empty);
                original = button.Hotkey;
                break;
        }

        if (review is { } kind)
        {
            notes.Add(new MigrationNote(kind, profile, button.Label, original));
        }

        return new V1ShortcutPlan(button.Label, color.Category, action);
    }

    private static (V1ActionPlan Action, MigrationNoteKind? Review) PlanKeys(string hotkey)
    {
        var none = new V1ActionPlan.KeyPresses([]);
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return (none, MigrationNoteKind.MissingAction);
        }

        var scan = V1ComboTokenizer.Scan(hotkey);
        if (!scan.IsResolved)
        {
            // Partial keys could send something the name does not say: incomplete until reviewed (MIG-005).
            return (none, MigrationNoteKind.UnresolvedToken);
        }

        if (IsLock(scan))
        {
            return (LockComputer, MigrationNoteKind.LockBecameSystemAction);
        }

        var keys = new V1ActionPlan.KeyPresses(scan.Chords);
        if (IsTaskManager(scan))
        {
            return (keys, MigrationNoteKind.SpecialCombination);
        }

        return V1LegacyHotkey.NeverWorked(hotkey)
            ? (keys, MigrationNoteKind.NeverWorkedInV1)
            : (keys, null);
    }

    /// <summary>Win+L (either Win key), which Windows reserves: it becomes «Lock computer» (MIG-007).</summary>
    private static bool IsLock(V1ComboScan scan) =>
        scan.Chords.Count == 1 && IsExactly(scan.Chords[0], KeyIds.Win, KeyIds.L);

    /// <summary>Ctrl+Shift+Esc, kept with the special combination warning (MIG-007).</summary>
    private static bool IsTaskManager(V1ComboScan scan) =>
        scan.Chords.Count == 1
        && IsExactly(scan.Chords[0], KeyIds.Ctrl, KeyIds.Shift, KeyIds.Escape);

    private static bool IsExactly(ValueList<KeyStroke> chord, params ReadOnlySpan<KeyId> keys)
    {
        var distinct = chord.Select(static s => s.Key).Distinct().ToList();
        if (distinct.Count != keys.Length)
        {
            return false;
        }

        foreach (var key in keys)
        {
            if (!distinct.Contains(key))
            {
                return false;
            }
        }

        return true;
    }

    private static V1SettingsPlan PlanSettings(
        V1Document document,
        ValueList<V1Monitor> monitors,
        ImmutableArray<V1ProfilePlan>.Builder profiles,
        int[] planIndexOfV1,
        ImmutableArray<MigrationNote>.Builder notes
    )
    {
        var v1Opacity =
            document.WindowOpacity is { } read && double.IsFinite(read) ? read : DefaultOpacity;
        var opacity = SettingsSchema.Opacity.Snap(v1Opacity);
        if (Math.Abs(opacity - v1Opacity) > 1e-9)
        {
            notes.Add(
                new MigrationNote(MigrationNoteKind.OpacityRounded, null, null, Format(v1Opacity))
            );
        }

        var height = document.ButtonSize?.Second ?? DefaultButtonHeight;
        var size = height switch
        {
            <= SmallMaxHeight => PanelSize.Small,
            <= MediumMaxHeight => PanelSize.Medium,
            _ => PanelSize.Large,
        };
        notes.Add(
            new MigrationNote(
                MigrationNoteKind.SizeChanged,
                null,
                null,
                height.ToString(CultureInfo.InvariantCulture)
            )
        );

        var (position, moved) = V1Placement.ToPanelPosition(
            document.WindowPosition ?? V1Placement.DefaultPosition,
            monitors
        );
        if (moved)
        {
            notes.Add(
                new MigrationNote(
                    MigrationNoteKind.PositionMoved,
                    null,
                    null,
                    Format(document.WindowPosition)
                )
            );
        }

        if (document.WindowSize is { } windowSize)
        {
            notes.Add(
                new MigrationNote(MigrationNoteKind.WindowSize, null, null, Format(windowSize))
            );
        }

        if (document.EditSize is { } editSize)
        {
            notes.Add(new MigrationNote(MigrationNoteKind.EditSize, null, null, Format(editSize)));
        }

        if (!string.IsNullOrEmpty(document.PinnedProfile))
        {
            // PQ-06: no base profile in Clícalo; the report suggests pinning its most used shortcuts.
            notes.Add(
                new MigrationNote(
                    MigrationNoteKind.PinnedProfile,
                    document.PinnedProfile,
                    null,
                    document.PinnedProfile
                )
            );
        }

        var general = 0;
        while (!profiles[general].IsGeneral)
        {
            general++;
        }

        var active = document.ActiveProfile ?? GeneralName;
        var lastProfile = general;
        var v1Index = FindProfile(document, active);
        if (v1Index >= 0)
        {
            lastProfile = planIndexOfV1[v1Index];
        }
        else if (document.ActiveProfile is not null)
        {
            // EC-MIG-03: a dangling active profile is ignored and reported.
            notes.Add(
                new MigrationNote(MigrationNoteKind.ActiveProfileMissing, active, null, active)
            );
        }

        return new V1SettingsPlan(opacity, size, lastProfile, position);
    }

    private static int FindProfile(V1Document document, string name)
    {
        for (var i = 0; i < document.Profiles.Count; i++)
        {
            if (string.Equals(document.Profiles[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The executable name in lower case without path (catalog §7.4), or <see langword="null"/> for manual.</summary>
    private static string? NormalizeProcess(string process)
    {
        var name = (process ?? string.Empty).Trim();
        var slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }

        return name.Length == 0 ? null : name.ToLowerInvariant();
    }

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string? Format(V1Pair? pair) =>
        pair is { } p
            ? string.Create(CultureInfo.InvariantCulture, $"{p.First}, {p.Second}")
            : null;
}
