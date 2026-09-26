using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Tests.Generators;
using CsCheck;
using static Clicalo.Domain.Tests.Library.LibraryBuilder;

namespace Clicalo.Domain.Tests.Document;

/// <summary>
/// The document invariants beyond the library (DAT-004, DAT-005, PER-008), slices compared by reference and undo by
/// slices (blueprint §6.4, DAT-006, FRE-004, FRE-005).
/// </summary>
public sealed class UserDocumentTests
{
    private static readonly UserDocument Valid = UserDocument.Create(
        Sample(),
        SettingsSchema.Defaults
    );

    [Fact]
    [Trait("Req", "DAT-001")]
    public void A_new_document_is_valid()
    {
        Valid.Validate().ShouldBeEmpty();
        Valid.Revision.ShouldBe(0);
        Valid.Frequents.ShouldBe(FrequentsState.Empty);
        Valid.Onboarding.Completed.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PER-008")]
    public void The_last_profile_must_exist()
    {
        var document = Valid with
        {
            Settings = Valid.Settings with { LastProfile = new ProfileId("ghost") },
        };

        document
            .Validate()
            .Select(v => v.Invariant)
            .ShouldBe([DocumentInvariant.LastProfileExists]);
    }

    [Fact]
    [Trait("Req", "DAT-001")]
    public void Every_setting_must_be_in_range()
    {
        var document = Valid with { Settings = Valid.Settings with { Columns = 7, DimTo = 2 } };

        document
            .Validate()
            .ShouldBe([
                new DocumentViolation(DocumentInvariant.SettingsInRange, SettingPaths.Columns),
                new DocumentViolation(DocumentInvariant.SettingsInRange, SettingPaths.DimTo),
            ]);
    }

    [Fact]
    public void Counters_are_not_negative_and_Frequents_are_well_formed()
    {
        (Valid with { Revision = -1 })
            .Validate()
            .Select(v => v.Invariant)
            .ShouldBe([DocumentInvariant.CountersNotNegative]);
        var copy = new ShortcutId("copy");
        (Valid with { Frequents = Valid.Frequents with { Pins = [copy, copy] } })
            .Validate()
            .Select(v => v.Invariant)
            .ShouldBe([DocumentInvariant.FrequentsWellFormed]);
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public void Only_the_changed_references_are_touched_slices()
    {
        SliceDiff.Touched(Valid, Valid with { Revision = 9 }).ShouldBe(DocumentSlices.None);
        SliceDiff
            .Touched(Valid, Valid with { Library = Sample() })
            .ShouldBe(DocumentSlices.Library);
        SliceDiff
            .Touched(
                Valid,
                Valid with
                {
                    Frequents = Valid.Frequents.WithPin(new ShortcutId("copy")),
                }
            )
            .ShouldBe(DocumentSlices.FrequentsCuration);
        SliceDiff
            .Touched(
                Valid,
                Valid with
                {
                    Frequents = Valid.Frequents with
                    {
                        Usage = DomainGen.Usage([(new ShortcutId("copy"), 1)]),
                    },
                }
            )
            .ShouldBe(DocumentSlices.FrequentsUsage);
        var pinned = Valid with { Frequents = Valid.Frequents.WithPin(new ShortcutId("copy")) };
        SliceDiff
            .Touched(pinned, pinned with { Frequents = pinned.Frequents.Reset() })
            .ShouldBe(DocumentSlices.FrequentsUsage | DocumentSlices.FrequentsCuration);
        SliceDiff
            .Touched(Valid, Valid with { Frequents = Valid.Frequents.Reset() })
            .ShouldBe(DocumentSlices.FrequentsUsage);
        SliceDiff
            .Touched(Valid, Valid with { Duplicates = new DuplicatePolicy([]) })
            .ShouldBe(DocumentSlices.Duplicates);
        SliceDiff
            .Touched(Valid, Valid with { Settings = Valid.Settings with { } })
            .ShouldBe(DocumentSlices.Settings);
        SliceDiff
            .Touched(Valid, Valid with { Onboarding = new OnboardingState(true) })
            .ShouldBe(DocumentSlices.Onboarding);
        (DocumentSlices.Significant & DocumentSlices.FrequentsUsage).ShouldBe(DocumentSlices.None);
    }

    [Fact]
    [Trait("Req", "FRE-005")]
    public void Restoring_the_library_keeps_the_usage_recorded_afterwards()
    {
        var copy = new ShortcutId("copy");
        var before = Valid with { Frequents = Valid.Frequents.WithPin(copy) };
        var deleted = before with { Library = before.Library.RemoveShortcut(copy).Value };
        var used = deleted with
        {
            Frequents = deleted.Frequents with
            {
                Usage = DomainGen.Usage([(new ShortcutId("bold"), 1)]),
            },
        };

        var restored = used.RestoreSlices(before, DocumentSlices.Library);

        restored.Library.ShouldBeSameAs(before.Library);
        restored.Frequents.ShouldBeSameAs(used.Frequents);
        restored.Frequents.Pins.ShouldBe([copy]);
    }

    [Fact]
    [Trait("Req", "FRE-004")]
    public void Restoring_both_Frequents_slices_brings_back_usage_pins_hidden_and_epoch()
    {
        var copy = new ShortcutId("copy");
        var before = Valid with
        {
            Frequents = new FrequentsState(
                [copy],
                [new ShortcutId("undo")],
                2,
                DomainGen.Usage([(copy, 1)])
            ),
        };
        var reset = before with { Frequents = before.Frequents.Reset() };

        var restored = reset.RestoreSlices(
            before,
            DocumentSlices.FrequentsCuration | DocumentSlices.FrequentsUsage
        );

        restored.Frequents.ShouldBe(before.Frequents);
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public void Restoring_settings_keeps_what_undo_does_not_cover()
    {
        var before = Valid;
        var after = Valid with
        {
            Settings = Valid.Settings with
            {
                Theme = ThemeChoice.Light,
                AutoSuggestProfiles = false,
                LastProfile = new ProfileId("word"),
            },
        };

        var restored = after.RestoreSlices(before, DocumentSlices.Settings);

        restored.Settings.Theme.ShouldBe(ThemeChoice.Light);
        restored.Settings.LastProfile.ShouldBe(new ProfileId("word"));
        restored.Settings.AutoSuggestProfiles.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public void Restoring_every_slice_of_itself_changes_nothing() =>
        Gen.Select(DomainGen.Document, DomainGen.Document)
            .Sample(
                (current, other) =>
                {
                    current.RestoreSlices(current, (DocumentSlices)63).ShouldBe(current);
                    var restored = current.RestoreSlices(
                        other,
                        DocumentSlices.Library | DocumentSlices.Duplicates
                    );
                    restored.Library.ShouldBeSameAs(other.Library);
                    restored.Duplicates.ShouldBeSameAs(other.Duplicates);
                    restored.Settings.ShouldBeSameAs(current.Settings);
                    SliceDiff
                        .Touched(current, restored)
                        .ShouldBe(
                            (
                                ReferenceEquals(current.Library, other.Library)
                                    ? DocumentSlices.None
                                    : DocumentSlices.Library
                            )
                                | (
                                    ReferenceEquals(current.Duplicates, other.Duplicates)
                                        ? DocumentSlices.None
                                        : DocumentSlices.Duplicates
                                )
                        );
                },
                iter: 2_000
            );
}
