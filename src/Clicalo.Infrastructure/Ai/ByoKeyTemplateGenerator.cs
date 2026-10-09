using Clicalo.Application.Ports;
using Microsoft.Extensions.AI;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// <see cref="ITemplateGenerator"/> with the person's own key (ADR-0014, user decision D5): the key comes from the
/// Credential Manager for each request, the messages are the fixed instruction of <see cref="AiPrompt"/> and the four
/// values of <see cref="TemplateRequest"/> (PLA-008), and the answer must pass <see cref="AiResponseReader"/>. Every
/// expected failure is an <see cref="AiFailure"/>, never an exception; nothing of the request or the answer is logged.
/// </summary>
internal sealed class ByoKeyTemplateGenerator : ITemplateGenerator
{
    private readonly IAiKeyStore _keys;
    private readonly IAiProvider _provider;

    /// <summary>Creates the generator.</summary>
    /// <param name="keys">The person's key.</param>
    /// <param name="provider">The provider.</param>
    public ByoKeyTemplateGenerator(IAiKeyStore keys, IAiProvider provider)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(provider);
        _keys = keys;
        _provider = provider;
    }

    /// <inheritdoc />
    public async ValueTask<TemplateGeneration> GenerateAsync(
        TemplateRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_keys.Read() is not { } key || string.IsNullOrWhiteSpace(key.Value))
        {
            return TemplateGeneration.Fail(AiFailure.NoKey);
        }

        ChatResponse response;
        try
        {
            using var client = _provider.Create(key);
            response = await client
                .GetResponseAsync(
                    [
                        new ChatMessage(ChatRole.System, AiPrompt.Instruction),
                        new ChatMessage(ChatRole.User, AiPrompt.UserMessage(request)),
                    ],
                    _provider.Options,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return TemplateGeneration.Fail(AiFailure.Offline);
        }
#pragma warning disable CA1031 // Any failure of the provider is an expected AI error card (PLA-006), never a crash.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return TemplateGeneration.Fail(_provider.Classify(ex));
        }

        return AiResponseReader.Read(response.Text) is { } proposal
            ? TemplateGeneration.Ok(proposal)
            : TemplateGeneration.Fail(AiFailure.Invalid);
    }
}
