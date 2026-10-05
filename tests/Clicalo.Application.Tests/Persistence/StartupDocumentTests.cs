using System.Collections.Immutable;
using Clicalo.Application.Persistence;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.Tests.Persistence;

/// <summary>
/// The usage of <c>usage.json</c> joins the loaded document at start (blueprint §6.5), without expired marks and
/// without the marks of shortcuts that no longer exist (FRE-002).
/// </summary>
[Trait("Req", "FRE-002")]
public sealed class StartupDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Recent_usage_of_existing_shortcuts_is_kept()
    {
        var document = PersistenceDocuments.Document(1) with
        {
            Library = PersistenceDocuments.Library(
                [],
                [PersistenceDocuments.Profile("general", [PersistenceDocuments.Shortcut("copy")])]
            ),
        };
        var usage = History(("copy", Now.AddDays(-1)), ("copy", Now.AddHours(-1)));

        var started = StartupDocument.WithUsage(document, usage, Now);

        Marks(started.Frequents.Usage).ShouldBe(Marks(usage));
        started.Library.ShouldBeSameAs(document.Library);
    }

    [Fact]
    public void Expired_marks_and_deleted_shortcuts_are_purged()
    {
        var document = PersistenceDocuments.Document(1) with
        {
            Library = PersistenceDocuments.Library(
                [],
                [PersistenceDocuments.Profile("general", [PersistenceDocuments.Shortcut("copy")])]
            ),
        };
        var expired = Now - Timings.Frequents.UsageWindow - TimeSpan.FromMinutes(1);
        var usage = History(
            ("copy", expired),
            ("copy", Now.AddHours(-1)),
            ("gone", Now.AddHours(-2))
        );

        var started = StartupDocument.WithUsage(document, usage, Now);

        Marks(started.Frequents.Usage).ShouldBe([("copy", Now.AddHours(-1))]);
    }

    [Fact]
    public void Nothing_to_merge_keeps_the_same_document()
    {
        var document = PersistenceDocuments.Document(1);

        StartupDocument.WithUsage(document, UsageHistory.Empty, Now).ShouldBeSameAs(document);
    }

    private static List<(string Id, DateTimeOffset At)> Marks(UsageHistory usage) =>
        [
            .. usage
                .Entries.OrderBy(static entry => entry.Key.Value, StringComparer.Ordinal)
                .SelectMany(static entry => entry.Value.Select(at => (entry.Key.Value, at))),
        ];

    private static UsageHistory History(params (string Id, DateTimeOffset At)[] marks) =>
        new(
            marks
                .GroupBy(static mark => mark.Id, StringComparer.Ordinal)
                .ToImmutableDictionary(
                    static group => new ShortcutId(group.Key),
                    static group => ValueListBuilder.From(group.Select(static mark => mark.At))
                )
        );
}
