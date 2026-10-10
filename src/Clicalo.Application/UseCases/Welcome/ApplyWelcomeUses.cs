using System.Collections.Immutable;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.UseCases.Welcome;

/// <summary>
/// Applies the effects of «¿Cómo usas tu equipo?» (BIE-005, <see cref="WelcomeEffects.Plan"/>) as one undoable step;
/// passing the step again with other answers recalculates them and joins the same step (EC-BIE-01). A setting the
/// person changed by hand after the welcome last set it is left alone (BIE-010).
/// </summary>
/// <param name="Uses">The marked options.</param>
public sealed record ApplyWelcomeUses(ImmutableHashSet<WelcomeUse> Uses) : IDocumentCommand
{
    /// <summary>The coalescing key of the step.</summary>
    public const string CoalesceKey = "welcome:uses";

    /// <summary>What the welcome last left in the four settings; <see langword="null"/> applies every effect.</summary>
    public WelcomeBaseline? Baseline { get; init; }

    /// <summary>Whether the answers <see cref="Baseline"/> comes from had tremor (the size was made L).</summary>
    public bool HadTremor { get; init; }

    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var next = document with
        {
            Settings = WelcomeEffects.Plan(document.Settings, Baseline, Uses, HadTremor).Settings,
        };
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
