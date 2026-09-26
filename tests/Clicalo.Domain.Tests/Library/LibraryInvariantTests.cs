using Clicalo.Domain.Tests.Generators;
using CsCheck;

namespace Clicalo.Domain.Tests.Library;

/// <summary>
/// The invariants I1 to I6 and those of the document hold after any sequence of commands on any generated document
/// (blueprint §6.2, <c>LibraryInvariantTests</c>): 10 000 random sequences of up to 25 commands of every kind, with
/// missing targets, invalid values and blank drafts on purpose.
/// </summary>
[Trait("Req", "DAT-004")]
[Trait("Req", "DAT-005")]
public sealed class LibraryInvariantTests
{
    [Fact]
    public void Every_generated_document_is_valid() =>
        DomainGen.Document.Sample(document => document.Validate().ShouldBeEmpty(), iter: 10_000);

    [Fact]
    public void No_sequence_of_commands_breaks_an_invariant() =>
        Gen.Select(DomainGen.Document, CommandFactory.Seed.Array[1, 25])
            .Sample(
                (document, seeds) =>
                {
                    var context = Contexts.Fresh();
                    foreach (var seed in seeds)
                    {
                        var command = CommandFactory.Create(seed, document);
                        var result = command.Apply(document, context);
                        if (!result.TryGetValue(out var change))
                        {
                            continue;
                        }

                        var violations = change.Next.Validate();
                        violations.ShouldBeEmpty(
                            command.GetType().Name + ": " + string.Join(", ", violations)
                        );
                        document = change.Next with { Revision = document.Revision + 1 };
                    }
                },
                iter: 10_000
            );

    [Fact]
    public void A_command_never_throws_and_every_failure_has_a_stable_code() =>
        Gen.Select(DomainGen.Document, CommandFactory.Seed)
            .Sample(
                (document, seed) =>
                {
                    var result = CommandFactory
                        .Create(seed, document)
                        .Apply(document, Contexts.Fresh());
                    if (result.IsFailure)
                    {
                        result.Failure.Code.ShouldMatch(
                            @"^(library|command)\.[a-z_]+(\.[a-z_]+)*$"
                        );
                    }
                },
                iter: 10_000
            );
}
