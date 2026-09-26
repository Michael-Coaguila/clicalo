using Clicalo.Application.Confirmation;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Tests.Generators;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.Store;

/// <summary>A document store with a fake clock and backups, and the two taps that confirm a destructive command.</summary>
internal sealed class StoreHarness
{
    public StoreHarness(UserDocument initial)
    {
        Time = new FakeTimeProvider(DomainGen.Now);
        Store = new DocumentStore(initial, new SequentialIds(), Backups, Time);
        Confirm = new TwoStepConfirm(Time);
        Store.Changed += (_, change) => Changes.Add(change);
    }

    public FakeTimeProvider Time { get; }

    public FakeBackupService Backups { get; } = new();

    public DocumentStore Store { get; }

    public TwoStepConfirm Confirm { get; }

    public List<DocumentChangedEventArgs> Changes { get; } = [];

    /// <summary>Dispatches any command, confirming a destructive one with two taps first.</summary>
    public Result<UserDocument> Dispatch(IDocumentCommand command) =>
        command is IDestructiveCommand destructive
            ? Store.Dispatch(destructive, TokenFor(destructive))
            : Store.Dispatch(command);

    /// <summary>The token of two taps on the control of <paramref name="command"/>.</summary>
    public ConfirmationToken TokenFor(IDestructiveCommand command)
    {
        var subject = new ConfirmationSubject(command.GetType().Name, "target");
        Confirm.Tap(subject).ShouldBeOfType<TwoStepResult.Armed>();
        return Confirm.Tap(subject).ShouldBeOfType<TwoStepResult.Confirmed>().Token;
    }

    /// <summary>Undoes everything that can be undone and returns how many steps it took.</summary>
    public int UndoAll()
    {
        var steps = 0;
        while (Store.CanUndo)
        {
            Store.Undo().IsSuccess.ShouldBeTrue();
            steps++;
        }

        return steps;
    }
}
