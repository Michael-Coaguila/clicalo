using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Marks the welcome as finished (BIE-009): a single one-way step, never undone, because undoing it would reopen the
/// welcome over the imported data (<c>undo-exemptions.json</c>).
/// </summary>
public sealed record FinishOnboarding : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Changes.Transparent(
            document.Onboarding.Completed
                ? document
                : document with
                {
                    Onboarding = new OnboardingState(Completed: true),
                }
        );
    }
}
