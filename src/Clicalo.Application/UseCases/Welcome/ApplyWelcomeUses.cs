using System.Collections.Immutable;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.UseCases.Welcome;

/// <summary>
/// Applies the effects of «¿Cómo usas tu equipo?» (BIE-005, <see cref="WelcomeEffects"/>) as one undoable step;
/// passing the step again with other answers recalculates them and joins the same step.
/// </summary>
/// <param name="Uses">The marked options.</param>
public sealed record ApplyWelcomeUses(ImmutableHashSet<WelcomeUse> Uses) : IDocumentCommand
{
    /// <summary>The coalescing key of the step.</summary>
    public const string CoalesceKey = "welcome:uses";

    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var next = document with { Settings = WelcomeEffects.Apply(document.Settings, Uses) };
        return Results.Ok(
            new DocumentChange(
                next,
                [],
                new UndoIntent.Record(L.Ob1t.Key, CoalesceKey),
                new BackupRequirement.None()
            )
        );
    }
}
