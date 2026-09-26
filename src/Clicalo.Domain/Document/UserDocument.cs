using System.Collections.Immutable;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Document;

/// <summary>
/// The user document, root of all persisted state (blueprint §6.3, DAT-001): immutable and shared between threads;
/// only <c>DocumentStore</c> replaces it, through commands. Its invariants (<see cref="DocumentInvariant"/>) hold for
/// every instance the store publishes; persistence repairs or quarantines anything else before it gets here (§6.5).
/// </summary>
/// <remarks>
/// The invariants: a shortcut lives in one list; General and Always visible exist and cannot be removed; General never
/// has a process; two profiles never share a process; every id is unique in the whole document; the last profile, when
/// set, exists; every setting is inside its range; the counters are not negative; Frequents are well formed. A blank
/// draft (ATJ-011) is valid and can always be discarded without a trace in the undo history.
/// </remarks>
/// <param name="Revision">Raised by the store on every change; a change that only raises it is not saved.</param>
/// <param name="Library">Profiles, shortcuts and the Always visible row.</param>
/// <param name="Frequents">Pins, hidden and usage.</param>
/// <param name="Duplicates">Ignored repeated combinations.</param>
/// <param name="Settings">Settings.</param>
/// <param name="Onboarding">Welcome state.</param>
public sealed record UserDocument(
    long Revision,
    ShortcutLibrary Library,
    FrequentsState Frequents,
    DuplicatePolicy Duplicates,
    UserSettings Settings,
    OnboardingState Onboarding
)
{
    /// <summary>
    /// A new document around <paramref name="library"/> and <paramref name="settings"/>: revision 0, empty Frequents,
    /// nothing ignored and the welcome not finished.
    /// </summary>
    /// <param name="library">The shortcuts (at least General).</param>
    /// <param name="settings">The settings, usually <see cref="SettingsSchema.Defaults"/>.</param>
    public static UserDocument Create(ShortcutLibrary library, UserSettings settings) =>
        new(
            0,
            library,
            FrequentsState.Empty,
            DuplicatePolicy.Empty,
            settings,
            new OnboardingState(Completed: false)
        );

    /// <summary>Every broken invariant; empty for a valid document.</summary>
    public ImmutableArray<DocumentViolation> Validate()
    {
        var violations = ImmutableArray.CreateBuilder<DocumentViolation>();
        foreach (var violation in Library.FindViolations())
        {
            violations.Add(new DocumentViolation(Map(violation.Invariant), violation.Detail));
        }

        if (Settings.LastProfile is { } last && !Library.TryGetProfile(last, out _))
        {
            violations.Add(
                new DocumentViolation(DocumentInvariant.LastProfileExists, "profile " + last.Value)
            );
        }

        SettingsSchema.Clamp(Settings, out var outOfRange);
        foreach (var path in outOfRange)
        {
            violations.Add(new DocumentViolation(DocumentInvariant.SettingsInRange, path));
        }

        if (Revision < 0 || Frequents.UsageEpoch < 0)
        {
            violations.Add(
                new DocumentViolation(
                    DocumentInvariant.CountersNotNegative,
                    "revision or usageEpoch"
                )
            );
        }

        if (!Frequents.IsWellFormed)
        {
            violations.Add(
                new DocumentViolation(DocumentInvariant.FrequentsWellFormed, "frequents")
            );
        }

        return violations.ToImmutable();
    }

    /// <summary>
    /// This document with <paramref name="slices"/> taken from <paramref name="before"/> and every other slice kept:
    /// the undo of the store (§6.4). Frequents are two slices, the curation (pins and hidden) and the usage (with its
    /// epoch). Settings restore only their undoable leaves, so presentation and placement chosen afterwards stay
    /// (<see cref="SettingsSchema.WithUndoableFrom"/>). The revision is raised by the store, not here.
    /// </summary>
    /// <remarks>
    /// A last profile chosen after the undone step is kept, so it may point to a profile the restored library no
    /// longer has (undoing the creation of a profile): it becomes General, as when that profile is deleted (PER-008),
    /// and the document stays valid.
    /// </remarks>
    /// <param name="before">The document of the undo entry.</param>
    /// <param name="slices">The slices the undone change touched.</param>
    public UserDocument RestoreSlices(UserDocument before, DocumentSlices slices)
    {
        ArgumentNullException.ThrowIfNull(before);
        var result = this;
        if (slices.HasFlag(DocumentSlices.Library))
        {
            result = result with { Library = before.Library };
        }

        if (slices.HasFlag(DocumentSlices.FrequentsCuration))
        {
            result = result with
            {
                Frequents = result.Frequents with
                {
                    Pins = before.Frequents.Pins,
                    Hidden = before.Frequents.Hidden,
                },
            };
        }

        if (slices.HasFlag(DocumentSlices.FrequentsUsage))
        {
            result = result with
            {
                Frequents = result.Frequents with
                {
                    UsageEpoch = before.Frequents.UsageEpoch,
                    Usage = before.Frequents.Usage,
                },
            };
        }

        if (slices.HasFlag(DocumentSlices.Duplicates))
        {
            result = result with { Duplicates = before.Duplicates };
        }

        if (slices.HasFlag(DocumentSlices.Settings))
        {
            result = result with
            {
                Settings = SettingsSchema.WithUndoableFrom(result.Settings, before.Settings),
            };
        }

        if (slices.HasFlag(DocumentSlices.Onboarding))
        {
            result = result with { Onboarding = before.Onboarding };
        }

        if (result.Settings.LastProfile is { } last && !result.Library.TryGetProfile(last, out _))
        {
            result = result with
            {
                Settings = result.Settings with { LastProfile = ProfileId.General },
            };
        }

        return result;
    }

    private static DocumentInvariant Map(LibraryInvariant invariant) =>
        invariant switch
        {
            LibraryInvariant.UniqueIds => DocumentInvariant.UniqueIds,
            LibraryInvariant.SingleList => DocumentInvariant.SingleList,
            LibraryInvariant.FixedListsExist => DocumentInvariant.FixedListsExist,
            LibraryInvariant.GeneralUnbound => DocumentInvariant.GeneralUnbound,
            LibraryInvariant.ProcessOwnedOnce => DocumentInvariant.ProcessOwnedOnce,
            _ => DocumentInvariant.MacroStepsValid,
        };
}
