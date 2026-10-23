using System.Net;
using System.Text;
using System.Text.Json;
using Clicalo.Application.Ports;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Infrastructure.Ai;

namespace Clicalo.Infrastructure.Tests.Ai;

/// <summary>
/// Gemini with the person's own Google AI Studio key (ADR-0033, PLA-008): the request goes only to the fixed address,
/// carries the key only in its header and only the fixed instruction and the four values in its body; the answer passes
/// the same contract as Anthropic's and every refusal is an error card. Deterministic: a fake HTTP handler under the
/// real client, never the real API.
/// </summary>
[Trait("Req", "PLA-008")]
public sealed class GeminiProviderTests
{
    private const string Key = "AIzaSy-test-key";

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
    public async Task The_request_carries_only_the_instruction_and_the_four_values_and_the_key_only_in_its_header()
    {
        using var handler = new FakeHandler(HttpStatusCode.OK, Response(Answer));
        using var http = new HttpClient(handler);
        var generator = new ByoKeyTemplateGenerator(new Keys(Key), new GeminiProvider(http));

        var generation = await generator.GenerateAsync(Request, CancellationToken.None);

        generation.Failure.ShouldBe(AiFailure.None);
        generation.Proposal.ShouldNotBeNull().App.ShouldBe("WhatsApp");
        var url = handler.Url.ShouldNotBeNull();
        url.ShouldBe(GeminiProvider.Endpoint);
        url.Scheme.ShouldBe(Uri.UriSchemeHttps);
        url.Host.ShouldBe("generativelanguage.googleapis.com");
        url.AbsolutePath.ShouldBe("/v1beta/models/" + GeminiProvider.Model + ":generateContent");
        url.Query.ShouldBeEmpty("the key never goes in the address");
        handler.Method.ShouldBe(HttpMethod.Post);
        handler.KeyHeader.ShouldBe(Key);
        handler.Authorization.ShouldBeNull();
        var text = handler.Body.ShouldNotBeNull();
        text.ShouldNotContain(Key, Case.Sensitive, "the key never goes in the body");

        using var body = JsonDocument.Parse(text);
        var root = body.RootElement;
        Names(root).ShouldBe(["contents", "generationConfig", "systemInstruction"]);
        PartText(root.GetProperty("systemInstruction")).ShouldBe(AiPrompt.Instruction);
        var content = root.GetProperty("contents").EnumerateArray().ShouldHaveSingleItem();
        content.GetProperty("role").GetString().ShouldBe("user");
        UserValues(PartText(content))
            .ShouldBe(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["app"] = "WhatsApp",
                    ["layout"] = "es-LA",
                    ["programsLang"] = "en",
                    ["uiLang"] = "es",
                }
            );
        var config = root.GetProperty("generationConfig");
        Names(config).ShouldBe(["maxOutputTokens", "responseMimeType"]);
        config.GetProperty("responseMimeType").GetString().ShouldBe("application/json");

        (await generator.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(
            AiFailure.None,
            "disposing the client of one generation never breaks the shared HTTP client of the next"
        );
    }

    [Fact]
    public async Task The_reasoning_parts_are_left_out_of_the_answer()
    {
        var response = JsonSerializer.Serialize(
            new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            role = "model",
                            parts = new object[]
                            {
                                new { text = "Thinking about WhatsApp…", thought = true },
                                new { text = Answer },
                            },
                        },
                        finishReason = "STOP",
                    },
                },
            }
        );
        using var handler = new FakeHandler(HttpStatusCode.OK, response);
        using var http = new HttpClient(handler);
        var generator = new ByoKeyTemplateGenerator(new Keys(Key), new GeminiProvider(http));

        (await generator.GenerateAsync(Request, CancellationToken.None))
            .Proposal.ShouldNotBeNull()
            .Shortcuts.Items.ShouldHaveSingleItem();
    }

    [Theory]
    [Trait("Req", "PLA-006")]
    [InlineData(HttpStatusCode.BadRequest, "API_KEY_INVALID", AiFailure.BadKey)]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_ARGUMENT", AiFailure.Unavailable)]
    [InlineData(HttpStatusCode.Unauthorized, "UNAUTHENTICATED", AiFailure.BadKey)]
    [InlineData(HttpStatusCode.PaymentRequired, "payment_required", AiFailure.BadKey)]
    [InlineData(HttpStatusCode.Forbidden, "PERMISSION_DENIED", AiFailure.BadKey)]
    [InlineData(HttpStatusCode.TooManyRequests, "RESOURCE_EXHAUSTED", AiFailure.BadKey)]
    [InlineData(HttpStatusCode.NotFound, "NOT_FOUND", AiFailure.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, "INTERNAL", AiFailure.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "UNAVAILABLE", AiFailure.Unavailable)]
    public async Task Provider_errors_become_their_cards(
        HttpStatusCode status,
        string reason,
        AiFailure expected
    )
    {
        var error =
            "{\"error\":{\"code\":"
            + ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ",\"message\":\"x\",\"status\":\"x\",\"details\":[{\"reason\":\""
            + reason
            + "\"}]}}";
        using var handler = new FakeHandler(status, error);
        using var http = new HttpClient(handler);
        var generator = new ByoKeyTemplateGenerator(new Keys(Key), new GeminiProvider(http));

        (await generator.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(expected);
    }

    [Theory]
    [Trait("Req", "PLA-006")]
    [Trait("Req", "LOG-006")]
    [InlineData("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}")]
    [InlineData("{\"candidates\":[]}")]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"not a template\"}]}}]}")]
    [InlineData("not json")]
    public async Task A_blocked_empty_or_foreign_answer_is_invalid(string response)
    {
        using var handler = new FakeHandler(HttpStatusCode.OK, response);
        using var http = new HttpClient(handler);
        var generator = new ByoKeyTemplateGenerator(new Keys(Key), new GeminiProvider(http));

        (await generator.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(
            AiFailure.Invalid
        );
    }

    [Fact]
    [Trait("Req", "LOG-006")]
    public async Task An_answer_larger_than_the_limit_is_invalid()
    {
        var padding = new string(' ', 70 * 1024);
        using var handler = new FakeHandler(HttpStatusCode.OK, Response(Answer) + padding);
        using var http = new HttpClient(handler);
        var generator = new ByoKeyTemplateGenerator(new Keys(Key), new GeminiProvider(http));

        (await generator.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(
            AiFailure.Invalid
        );
    }

    [Fact]
    [Trait("Req", "PLA-006")]
    public async Task Without_network_it_is_offline()
    {
        using var handler = new FakeHandler(HttpStatusCode.OK, Response(Answer))
        {
            Throw = new HttpRequestException("no route"),
        };
        using var http = new HttpClient(handler);
        var generator = new ByoKeyTemplateGenerator(new Keys(Key), new GeminiProvider(http));

        (await generator.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(
            AiFailure.Offline
        );
    }

    [Theory]
    [Trait("Req", "PLA-006")]
    [InlineData("AIza key")]
    [InlineData("AIza\nkey")]
    [InlineData("AIzá")]
    public async Task A_key_that_is_not_printable_ascii_is_never_sent(string key)
    {
        using var handler = new FakeHandler(HttpStatusCode.OK, Response(Answer));
        using var http = new HttpClient(handler);
        var generator = new ByoKeyTemplateGenerator(new Keys(key), new GeminiProvider(http));

        (await generator.GenerateAsync(Request, CancellationToken.None)).Failure.ShouldBe(
            AiFailure.BadKey
        );
        handler.Url.ShouldBeNull();
    }

    private static string[] Names(JsonElement element) =>
        [.. element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];

    private static string PartText(JsonElement holder) =>
        holder
            .GetProperty("parts")
            .EnumerateArray()
            .ShouldHaveSingleItem()
            .GetProperty("text")
            .GetString()!;

    private static Dictionary<string, string> UserValues(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document
            .RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
    }

    private static string Response(string text) =>
        JsonSerializer.Serialize(
            new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new { role = "model", parts = new[] { new { text } } },
                        finishReason = "STOP",
                    },
                },
                usageMetadata = new { promptTokenCount = 10, candidatesTokenCount = 20 },
                modelVersion = GeminiProvider.Model,
            }
        );

    private sealed class Keys(string key) : IAiKeyStore
    {
        public string Target => "Clicalo/ai/gemini";

        public bool HasKey() => true;

        public bool Save(Sensitive<string> value) => false;

        public Sensitive<string>? Read() => new Sensitive<string>(key, RedactionKind.Secret);

        public bool Delete() => true;
    }

    private sealed class FakeHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public Exception? Throw { get; init; }

        public string? Body { get; private set; }

        public Uri? Url { get; private set; }

        public HttpMethod? Method { get; private set; }

        public string? KeyHeader { get; private set; }

        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Url = request.RequestUri;
            Method = request.Method;
            KeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var values)
                ? values.Single()
                : null;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (Throw is { } error)
            {
                throw error;
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
