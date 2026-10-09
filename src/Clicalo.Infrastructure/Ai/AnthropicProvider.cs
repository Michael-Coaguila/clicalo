using Anthropic;
using Anthropic.Exceptions;
using Clicalo.Application.Ports;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.AI;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// Claude by Anthropic with the official SDK and its <see cref="IChatClient"/> (ADR-0014, user decision D5): a fast,
/// low-cost model, no retries (the person retries from the error card) and the time limit of
/// <c>Timings.Ai.AiRequestTimeout</c>. The address and the key are always set explicitly, so no environment variable
/// can redirect the request or add a credential.
/// </summary>
internal sealed class AnthropicProvider : IAiProvider
{
    /// <summary>The model: Claude Haiku 5.5, fast and economical for a short structured answer.</summary>
    public const string Model = "claude-haiku-5-5";

    private const string Endpoint = "https://api.anthropic.com";

    private static readonly HttpClient SharedHttp = new();

    private readonly HttpClient? _http;

    /// <summary>Creates the provider.</summary>
    /// <param name="http">The HTTP client; null for the SDK's own. Tests pass one with a fake handler.</param>
    public AnthropicProvider(HttpClient? http = null) => _http = http;

    /// <inheritdoc />
    public string Id => "anthropic";

    /// <inheritdoc />
    public ChatOptions Options =>
        new() { ModelId = Model, MaxOutputTokens = Timings.Ai.AiMaxOutputTokens };

    /// <inheritdoc />
    public IChatClient Create(Sensitive<string> key)
    {
        var client = new AnthropicClient
        {
            ApiKey = key.Value,
            BaseUrl = Endpoint,
            MaxRetries = 0,
            Timeout = Timings.Ai.AiRequestTimeout,
            HttpClient = _http ?? SharedHttp,
        };
        return client.AsIChatClient(Model);
    }

    /// <inheritdoc />
    public AiFailure Classify(Exception exception) =>
        exception switch
        {
            AnthropicUnauthorizedException
            or AnthropicForbiddenException
            or AnthropicRateLimitException => AiFailure.BadKey,
            Anthropic5xxException => AiFailure.Unavailable,
            AnthropicIOException or HttpRequestException or TimeoutException => AiFailure.Offline,
            AnthropicApiException => AiFailure.Unavailable,
            _ => AiFailure.Invalid,
        };
}
