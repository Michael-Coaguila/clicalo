using Clicalo.Infrastructure.Ai;
using Clicalo.TestKit;

namespace Clicalo.Infrastructure.Tests.Ai;

/// <summary>
/// The structural check of an AI answer against the contract <c>data/schemas/ai-template.v1.schema.json</c>
/// (PLA-008, LOG-006): the fixture that the schema accepts is read, and anything outside the contract is invalid.
/// </summary>
[Trait("Req", "PLA-008")]
[Trait("Req", "LOG-006")]
public sealed class AiResponseReaderTests
{
    private static string Fixture() =>
        File.ReadAllText(
            RepoPaths.Combine(
                "tests",
                "Clicalo.Infrastructure.Tests",
                "Fixtures",
                "schema",
                "1.0",
                "ai-template.json"
            )
        );

    [Fact]
    public void The_contract_fixture_is_read()
    {
        var proposal = AiResponseReader.Read(Fixture()).ShouldNotBeNull();

        proposal.Known.ShouldBeTrue();
        proposal.App.ShouldBe("WhatsApp");
        proposal.Process.ShouldBe("WhatsApp.exe");
        proposal.Shortcuts.Count.ShouldBe(3);
        proposal.Shortcuts[0].Keys.ShouldBe(["ctrl", "n"]);
        proposal.Shortcuts[0].NameEn.ShouldBe("New chat");
    }

    [Fact]
    public void An_answer_wrapped_in_a_code_fence_is_read()
    {
        AiResponseReader.Read("Aquí tienes:\n```json\n" + Fixture() + "\n```").ShouldNotBeNull();
    }

    [Theory]
    [InlineData("\"known\": true,", "\"known\": true, \"url\": \"https://x\",")]
    [InlineData("\"cat\": \"file\",", "\"cat\": \"file\", \"type\": \"url\",")]
    [InlineData("\"keys\": [\"ctrl\", \"n\"]", "\"keys\": [\"Ctrl\", \"N\"]")]
    [InlineData("\"cat\": \"file\"", "\"cat\": \"macro\"")]
    [InlineData("\"confidence\": 0.9", "\"confidence\": 7")]
    [InlineData("\"known\": true", "\"known\": \"yes\"")]
    [InlineData("\"icon\": \"chat\"", "\"icon\": \"Chat Icon\"")]
    [InlineData(
        "\"es\": \"Nuevo chat\"",
        "\"es\": \"Un nombre larguísimo que no cabe en una ficha\""
    )]
    [InlineData("\"known\": true,", "\"known\": true, \"known\": false,")]
    public void Anything_outside_the_contract_is_invalid(string original, string replacement)
    {
        var json = Fixture();
        json.ShouldContain(original);

        AiResponseReader
            .Read(json.Replace(original, replacement, StringComparison.Ordinal))
            .ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no hay JSON")]
    [InlineData("{ \"known\": true, ")]
    [InlineData("{ /* c */ \"known\": true }")]
    public void Text_that_is_not_a_proposal_is_invalid(string? answer) =>
        AiResponseReader.Read(answer).ShouldBeNull();

    [Fact]
    public void More_buttons_than_allowed_is_invalid()
    {
        var button =
            "{\"name\":{\"es\":\"A\",\"en\":\"A\"},\"icon\":\"add\",\"keys\":[\"ctrl\",\"a\"],\"cat\":\"edit\"}";
        var json =
            "{\"known\":true,\"app\":\"X\",\"icon\":\"apps\",\"buttons\":["
            + string.Join(",", Enumerable.Repeat(button, 25))
            + "]}";

        AiResponseReader.Read(json).ShouldBeNull();
    }
}
