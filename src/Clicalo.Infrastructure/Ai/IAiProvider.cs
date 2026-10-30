using Clicalo.Application.Ports;
using Clicalo.Domain.Privacy;
using Microsoft.Extensions.AI;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// One AI provider behind <see cref="ByoKeyTemplateGenerator"/> (ADR-0014): changing provider is writing another of
/// these, never touching the interface.
/// </summary>
internal interface IAiProvider
{
    /// <summary>The provider id, part of the Credential Manager target (<c>Clicalo/ai/{id}</c>, ADR-0008).</summary>
    string Id { get; }

    /// <summary>The chat options of a generation: the model and the output ceiling.</summary>
    ChatOptions Options { get; }

    /// <summary>A chat client that authenticates with <paramref name="key"/>.</summary>
    /// <param name="key">The person's own key.</param>
    IChatClient Create(Sensitive<string> key);

    /// <summary>The failure an exception of the client means.</summary>
    /// <param name="exception">What the client threw.</param>
    AiFailure Classify(Exception exception);
}
