using System.Net;
using System.Text;
using System.Text.Json;
using Clicalo.Application.Ports;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Infrastructure.Ai;
using Microsoft.Extensions.AI;

namespace Clicalo.Infrastructure.Tests.Ai;

/// <summary>
/// The template generator with the person's own key (ADR-0014, user decision D5, PLA-008): what leaves the machine is
/// the fixed instruction and exactly the four values; the answer passes the contract; every failure is an error card.
/// Deterministic: a fake <see cref="IChatClient"/>, and a fake HTTP handler under the real Anthropic client.
/// </summary>
[Trait("Req", "PLA-008")]
public sealed class ByoKeyTemplateGeneratorTests
{
    private const string Answer =
        "{\"known\":true,\"app\":\"WhatsApp\",\"process\":\"WhatsApp.exe\",\"icon\":\"chat\",\"buttons\":["
        + "{\"name\":{\"es\":\"Nuevo chat\",\"en\":\"New chat\"},\"icon\":\"add\",\"keys\":[\"ctrl\",\"n\"],\"cat\":\"file\",\"confidence\":0.9}]}";

    private static readonly TemplateRequest Request = new(
        "WhatsApp",
        "es-LA",
        LangCode.En,
        LangCode.Es
    );

    [Fact]
    public async Task The_messages_are_the_fixed_instruction_and_exactly_the_four_values()
    {
        var chat = new FakeChatClient(Answer);
        var generator = new ByoKeyTemplateGenerator(new Keys("sk-1"), new FakeProvider(chat));

        var generation = await generator.GenerateAsync(Request, CancellationToken.None);

        generation.Failure.ShouldBe(AiFailure.None);
        generation.Proposal.ShouldNotBeNull().App.ShouldBe("WhatsApp");
        chat.Messages.Count.ShouldBe(2);
        chat.Messages[0].Role.ShouldBe(ChatRole.System);
        chat.Messages[0].Text.ShouldBe(AiPrompt.Instruction);
        chat.Messages[1].Role.ShouldBe(ChatRole.User);
        UserValues(chat.Messages[1].Text)
            .ShouldBe(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["app"] = "WhatsApp",
                    ["layout"] = "es-LA",
                    ["programsLang"] = "en",
                    ["uiLang"] = "es",
                }
            );
        chat.Key.ShouldBe("sk-1");
    }

    [Fact]
    [Trait("Req", "PLA-005")]
    public async Task Without_a_key_nothing_is_sent()
    {
        var chat = new FakeChatClient(Answer);
        var generator = new ByoKeyTemplateGenerator(new Keys(null), new FakeProvider(chat));

        (await generator.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(
            AiFailure.NoKey
        );

        chat.Messages.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "PLA-006")]
    public async Task An_answer_outside_the_contract_and_a_client_error_are_error_cards()
    {
        var invalid = new ByoKeyTemplateGenerator(
            new Keys("k"),
            new FakeProvider(new FakeChatClient("no"))
        );
        (await invalid.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(
            AiFailure.Invalid
        );

        var failing = new ByoKeyTemplateGenerator(
            new Keys("k"),
            new FakeProvider(
                new FakeChatClient(Answer) { Throw = new HttpRequestException("down") }
            )
        );
        (await failing.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(
            AiFailure.Offline
        );
    }

    [Fact]
    public async Task The_anthropic_request_carries_only_the_instruction_and_the_four_values()
    {
        using var handler = new FakeHandler(HttpStatusCode.OK, MessageResponse(Answer));
        using var http = new HttpClient(handler);
        var generator = new ByoKeyTemplateGenerator(
            new Keys("sk-ant-test"),
            new AnthropicProvider(http)
        );

        var generation = await generator.GenerateAsync(Request, CancellationToken.None);

        generation.Failure.ShouldBe(AiFailure.None);
        handler.Url.ShouldNotBeNull().Host.ShouldBe("api.anthropic.com");
        handler.ApiKey.ShouldBe("sk-ant-test");
        using var body = JsonDocument.Parse(handler.Body.ShouldNotBeNull());
        var root = body.RootElement;
        root.GetProperty("model").GetString().ShouldBe(AnthropicProvider.Model);
        root.TryGetProperty("metadata", out _)
            .ShouldBeFalse("no identifier of the installation or the person");
        SystemText(root.GetProperty("system")).ShouldBe(AiPrompt.Instruction);
        var message = root.GetProperty("messages").EnumerateArray().ShouldHaveSingleItem();
        message.GetProperty("role").GetString().ShouldBe("user");
        UserValues(ContentText(message.GetProperty("content")))
            .Keys.Order(StringComparer.Ordinal)
            .ShouldBe(["app", "layout", "programsLang", "uiLang"]);

        (await generator.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(
            AiFailure.None,
            "disposing the client of one generation never breaks the shared HTTP client of the next"
        );
    }

    [Theory]
    [Trait("Req", "PLA-006")]
    [InlineData(HttpStatusCode.Unauthorized, AiFailure.BadKey)]
    [InlineData(HttpStatusCode.Forbidden, AiFailure.BadKey)]
    [InlineData(HttpStatusCode.TooManyRequests, AiFailure.BadKey)]
    [InlineData(HttpStatusCode.InternalServerError, AiFailure.Unavailable)]
    public async Task Provider_errors_become_their_cards(HttpStatusCode status, AiFailure expected)
    {
        using var handler = new FakeHandler(
            status,
            "{\"type\":\"error\",\"error\":{\"type\":\"x\",\"message\":\"x\"}}"
        );
        using var http = new HttpClient(handler);
        var generator = new ByoKeyTemplateGenerator(new Keys("k"), new AnthropicProvider(http));

        (await generator.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(expected);
    }

    private static Dictionary<string, string> UserValues(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document
            .RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
    }

    private static string SystemText(JsonElement system) =>
        system.ValueKind == JsonValueKind.String ? system.GetString()! : ContentText(system);

    private static string ContentText(JsonElement content) =>
        content.ValueKind == JsonValueKind.String
            ? content.GetString()!
            : string.Concat(
                content.EnumerateArray().Select(b => b.GetProperty("text").GetString())
            );

    private static string MessageResponse(string text) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = "msg_test",
                ["type"] = "message",
                ["role"] = "assistant",
                ["model"] = AnthropicProvider.Model,
                ["content"] = new[]
                {
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["type"] = "text",
                        ["text"] = text,
                    },
                },
                ["stop_reason"] = "end_turn",
                ["stop_sequence"] = null,
                ["usage"] = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["input_tokens"] = 10,
                    ["output_tokens"] = 20,
                },
            }
        );

    private sealed class Keys(string? key) : IAiKeyStore
    {
        public string Target => "Clicalo/ai/test";

        public bool HasKey() => key is not null;

        public bool Save(Sensitive<string> value) => false;

        public Sensitive<string>? Read() =>
            key is null ? null : new Sensitive<string>(key, RedactionKind.Secret);

        public bool Delete() => true;
    }

    private sealed class FakeProvider(FakeChatClient chat) : IAiProvider
    {
        public string Id => "fake";

        public ChatOptions Options => new() { ModelId = "fake-model" };

        public IChatClient Create(Sensitive<string> key)
        {
            chat.Key = key.Value;
            return chat;
        }

        public AiFailure Classify(Exception exception) =>
            exception is HttpRequestException ? AiFailure.Offline : AiFailure.Invalid;
    }

    private sealed class FakeChatClient(string answer) : IChatClient
    {
        public List<ChatMessage> Messages { get; } = [];

        public string? Key { get; set; }

        public Exception? Throw { get; init; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Messages.AddRange(messages);
            return Throw is { } error
                ? Task.FromException<ChatResponse>(error)
                : Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class FakeHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public Uri? Url { get; private set; }

        public string? ApiKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Url = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("x-api-key", out var values)
                ? values.Single()
                : null;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
