using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Marks the welcome as finished (BIE-009): a single one-way step, never undone, because undoing it would reopen the
/// welcome over the imported data (<c>undo-exemptions.json</c>). With <see cref="Answers"/> it also records what this
/// welcome answered (BIE-010, ADR-0028), also on a repeated welcome; without them it keeps the recorded ones.
/// </summary>
public sealed record FinishOnboarding : IDocumentCommand
{
    /// <summary>The answers to record, or <see langword="null"/> to keep the ones already recorded.</summary>
    public WelcomeAnswers? Answers { get; init; }

    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var current = document.Onboarding;
        var answers = Answers ?? current.Answers;
        return Changes.Transparent(
            current.Completed && answers == current.Answers
                ? document
                : document with
                {
                    Onboarding = current with { Completed = true, Answers = answers },
                }
        );
    }
}
