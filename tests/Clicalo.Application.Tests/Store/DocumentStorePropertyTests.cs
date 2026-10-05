using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Tests.Generators;
using CsCheck;

namespace Clicalo.Application.Tests.Store;

/// <summary>
/// Properties of the store over generated documents and random sequences of every command (blueprint §6.4): the
/// published document is always valid, every published change raises the revision by one, and undoing step by step
/// restores exactly the slices each step touched and nothing else, down to the 20 steps kept (DAT-006, REG-07): 10 000 random sequences of up to 30 commands.
/// </summary>
public sealed class DocumentStorePropertyTests
{
    private const int Iterations = 10_000;

    [Fact]
    [Trait("Req", "DAT-005")]
    [Trait("Req", "REG-07")]
    public void The_store_only_publishes_valid_documents_with_consecutive_revisions() =>
        Gen.Select(DomainGen.Document, CommandFactory.Seed.Array[1, 30])
            .Sample(
                (document, seeds) =>
                {
                    var harness = new StoreHarness(document);
                    foreach (var seed in seeds)
                    {
                        var before = harness.Store.Current;
                        var published = harness.Changes.Count;
                        harness.Dispatch(CommandFactory.Create(seed, before));
                        var after = harness.Store.Current;
                        if (harness.Changes.Count == published)
                        {
                            after.ShouldBeSameAs(before);
                            continue;
                        }

                        after.Revision.ShouldBe(before.Revision + 1);
                        after.Validate().ShouldBeEmpty();
                        harness.Changes[^1].Slices.ShouldBe(SliceDiff.Touched(before, after));
                        harness.Changes[^1].Slices.ShouldNotBe(DocumentSlices.None);
                    }
                },
                iter: Iterations
            );

    [Fact]
    [Trait("Req", "DAT-006")]
    [Trait("Req", "REG-07")]
    public void Undo_restores_each_step_slice_by_slice()
    {
        var undone = 0;
        Gen.Select(DomainGen.Document, CommandFactory.Seed.Array[1, 30])
            .Sample(
                (document, seeds) =>
                {
                    var harness = new StoreHarness(document);
                    var steps = new List<DocumentChangedEventArgs>();
                    foreach (var seed in seeds)
                    {
                        var command = CommandFactory.Create(seed, harness.Store.Current);
                        var records =
                            command.Apply(harness.Store.Current, Contexts.Fresh())
                                is { IsSuccess: true } probe
                            && probe.Value.Undo is UndoIntent.Record;
                        var published = harness.Changes.Count;
                        harness.Dispatch(command);
                        harness.Store.SealCoalescing();
                        if (records && harness.Changes.Count > published)
                        {
                            steps.Add(harness.Changes[^1]);
                        }
                    }

                    var expected = steps.TakeLast(20).Reverse().ToArray();
                    foreach (var step in expected)
                    {
                        var before = harness.Store.Current;
                        harness.Store.Undo().IsSuccess.ShouldBeTrue();
                        var after = harness.Store.Current;
                        ShouldRestore(step, before, after);
                        after.Validate().ShouldBeEmpty();
                    }

                    harness.Store.CanUndo.ShouldBeFalse();
                    Interlocked.Add(ref undone, expected.Length);
                },
                iter: Iterations
            );

        // Without this the property could pass on sequences that never record a step.
        undone.ShouldBeGreaterThan(Iterations * 3);
    }

    private static void ShouldRestore(
        DocumentChangedEventArgs step,
        UserDocument before,
        UserDocument after
    )
    {
        var restored = step.Before;
        var slices = step.Slices;
        Slice(slices, DocumentSlices.Library, after.Library, restored.Library, before.Library);
        Slice(
            slices,
            DocumentSlices.FrequentsCuration,
            (after.Frequents.Pins, after.Frequents.Hidden),
            (restored.Frequents.Pins, restored.Frequents.Hidden),
            (before.Frequents.Pins, before.Frequents.Hidden)
        );
        Slice(
            slices,
            DocumentSlices.FrequentsUsage,
            (after.Frequents.UsageEpoch, after.Frequents.Usage),
            (restored.Frequents.UsageEpoch, restored.Frequents.Usage),
            (before.Frequents.UsageEpoch, before.Frequents.Usage)
        );
        Slice(
            slices,
            DocumentSlices.Duplicates,
            after.Duplicates,
            restored.Duplicates,
            before.Duplicates
        );
        Slice(
            slices,
            DocumentSlices.Onboarding,
            after.Onboarding,
            restored.Onboarding,
            before.Onboarding
        );
        var settings = slices.HasFlag(DocumentSlices.Settings)
            ? SettingsSchema.WithUndoableFrom(before.Settings, restored.Settings)
            : before.Settings;
        if (settings.LastProfile is { } last && !after.Library.TryGetProfile(last, out _))
        {
            // A last profile chosen after the step cannot outlive the profile the undo removes (PER-008).
            settings = settings with
            {
                LastProfile = ProfileId.General,
            };
        }

        after.Settings.ShouldBe(settings, "Settings");
    }

    private static void Slice<T>(
        DocumentSlices touched,
        DocumentSlices slice,
        T actual,
        T restored,
        T untouched
    ) => actual.ShouldBe(touched.HasFlag(slice) ? restored : untouched, slice.ToString());
}
