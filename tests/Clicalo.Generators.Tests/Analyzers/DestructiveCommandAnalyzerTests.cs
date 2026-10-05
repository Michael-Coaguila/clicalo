using Microsoft.CodeAnalysis.Testing;
using Verify = Clicalo.Generators.Tests.Analyzers.AnalyzerVerifier<Clicalo.Analyzers.Safety.DestructiveCommandAnalyzer>;

namespace Clicalo.Generators.Tests.Analyzers;

[Trait("Req", "REG-04")]
public sealed class DestructiveCommandAnalyzerTests
{
    [Fact]
    public Task Dispatching_a_destructive_command_without_a_token_is_an_error() =>
        Verify.VerifyAsync(
            """
            using Clicalo.Application.Store;
            using Clicalo.Domain.Commands;

            namespace Clicalo.Presentation.Editor;

            public sealed class EditorViewModel(DocumentStore store)
            {
                public void Delete(int id)
                {
                    store.Dispatch({|CLC0010:new DeleteShortcut(id)|});
                    IDestructiveCommand command = new DeleteShortcut(id);
                    store.Dispatch({|CLC0010:command|});
                    store.DispatchOptional({|CLC0010:command|});
                    store.DispatchAll(new RenameShortcut(id), {|CLC0010:command|});
                    store.Queue({|CLC0010:new DeleteShortcut(id)|});
                }
            }
            """,
            Stubs.Commands
        );

    [Fact]
    public Task A_batch_that_carries_a_destructive_command_needs_a_token_too() =>
        Verify.VerifyAsync(
            """
            using System.Collections.Generic;
            using Clicalo.Application.Store;
            using Clicalo.Domain.Commands;

            namespace Clicalo.Presentation.Editor;

            public sealed class EditorViewModel(DocumentStore store)
            {
                public void Delete(int id, IDestructiveCommand[] pending, List<IDocumentCommand> history)
                {
                    store.DispatchAll(new IDocumentCommand[] { new RenameShortcut(id), {|CLC0010:new DeleteShortcut(id)|} });
                    store.DispatchAll([new RenameShortcut(id), {|CLC0010:new DeleteShortcut(id)|}]);
                    store.DispatchBatch([new RenameShortcut(id), .. {|CLC0010:pending|}]);
                    store.DispatchBatch({|CLC0010:pending|});
                    store.DispatchBatch([new RenameShortcut(id)]);
                    history.Add(new DeleteShortcut(id));
                    history.AddRange(pending);
                }
            }
            """,
            Stubs.Commands
        );

    [Fact]
    public Task Dispatching_with_the_token_issued_by_two_step_confirm_is_allowed() =>
        Verify.VerifyAsync(
            """
            using System;
            using Clicalo.Application.Confirmation;
            using Clicalo.Application.Store;
            using Clicalo.Domain.Commands;

            namespace Clicalo.Presentation.Editor;

            public sealed class EditorViewModel(DocumentStore store, TwoStepConfirm confirm)
            {
                private ConfirmationToken _pending = null;

                public void Delete(int id)
                {
                    var command = new DeleteShortcut(id);
                    confirm.Arm(command);
                    store.Dispatch(command, confirm.Confirm());
                    store.DispatchOptional(command, confirm.Confirm());
                    store.Dispatch(new RenameShortcut(id));
                    store.Remember(command);
                    store.Keep(command);
                    ArgumentNullException.ThrowIfNull(command);
                    _ = command.Equals(new DeleteShortcut(id));
                    _pending = null;
                }
            }
            """,
            Stubs.Commands
        );

    [Fact]
    public Task Only_two_step_confirm_may_create_a_token() =>
        Verify.VerifyAsync(
            """
            using Clicalo.Application.Confirmation;
            using Clicalo.Application.Store;
            using Clicalo.Domain.Commands;

            namespace Clicalo.Application.Confirmation
            {
                public sealed class ForgedToken : ConfirmationToken
                {
                    public ForgedToken() : {|CLC0010:base(7)|} { }
                }

                public static class Shortcuts
                {
                    public static ConfirmationToken Skip() => {|CLC0010:new ConfirmationToken(1)|};
                    public static ConfirmationToken SkipTargetTyped() => {|CLC0010:new()|};
                    public static ConfirmationToken Sneak() => {|CLC0010:new ForgedToken()|};
                }
            }

            namespace Clicalo.Presentation.Editor
            {
                public sealed class EditorViewModel(DocumentStore store)
                {
                    public void Delete(int id)
                    {
                        store.Dispatch(new DeleteShortcut(id), {|CLC0010:null|});
                        store.Dispatch(new DeleteShortcut(id), {|CLC0010:default|});
                    }
                }
            }
            """,
            Stubs.Commands
        );

    [Fact]
    public Task Struct_tokens_cannot_be_defaulted_or_copied_outside_the_issuer() =>
        Verify.VerifyAsync(
            """
            using Clicalo.Application.Confirmation;
            using Clicalo.Application.Store;
            using Clicalo.Domain.Commands;

            namespace Clicalo.Presentation.Profiles;

            public sealed class ProfilesViewModel(DocumentStore store, TwoStepConfirm confirm)
            {
                private ConfirmationToken? _pending = null;

                public void Delete(int id)
                {
                    var token = confirm.Confirm();
                    store.Dispatch(new DeleteProfile(id), {|CLC0010:token with { Serial = 2 }|});
                    store.Dispatch(new DeleteProfile(id), {|CLC0010:default(ConfirmationToken)|});
                    store.Dispatch(new DeleteProfile(id), {|CLC0010:default|});
                    store.DispatchMaybe(new DeleteProfile(id), {|CLC0010:null|});
                    store.DispatchMaybe(new DeleteProfile(id), {|CLC0010:default|});
                    store.DispatchMaybe(new DeleteProfile(id), _pending);
                    store.Dispatch(new DeleteProfile(id), token);
                }
            }
            """,
            Stubs.CommandsWithStructToken
        );

    [Fact]
    public Task Without_the_contract_types_the_rule_stays_silent() =>
        Verify.VerifyAsync(
            """
            public interface IDestructiveCommand { }
            public sealed class ConfirmationToken { public ConfirmationToken() { } }
            public sealed record DeleteShortcut : IDestructiveCommand;

            public static class Store
            {
                public static void Dispatch(IDestructiveCommand command) { }
                public static void Run() { Dispatch(new DeleteShortcut()); _ = new ConfirmationToken(); }
            }
            """
        );

    [Fact]
    public Task A_justified_suppression_silences_the_rule() =>
        Verify.VerifyAsync(
            """
            using System.Diagnostics.CodeAnalysis;
            using Clicalo.Application.Store;
            using Clicalo.Domain.Commands;

            namespace Clicalo.Application.Migration;

            public static class Upgrade
            {
                [SuppressMessage("Clicalo.Safety", "CLC0010", Justification = "Undo replays commands the user already confirmed.")]
                public static void Replay(DocumentStore store, IDestructiveCommand command) => store.Dispatch(command);
            }
            """,
            Stubs.Commands
        );

    [Fact]
    public async Task Reports_the_command_and_the_call_in_the_message()
    {
        var test = Verify.Create(
            """
            using Clicalo.Application.Store;
            using Clicalo.Domain.Commands;

            public static class Editor
            {
                public static void Delete(DocumentStore store) => store.Dispatch({|#0:new DeleteShortcut(1)|});
            }
            """,
            Stubs.Commands
        );
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(Clicalo.Analyzers.Descriptors.MissingConfirmationToken)
                .WithLocation(0)
                .WithArguments("DeleteShortcut", "DocumentStore.Dispatch")
        );

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
