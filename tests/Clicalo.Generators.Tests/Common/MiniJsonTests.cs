using Clicalo.Generators.Common;

namespace Clicalo.Generators.Tests.Common;

public sealed class MiniJsonTests
{
    [Fact]
    public void Parses_nested_values_and_keeps_member_order()
    {
        var root = MiniJson.Parse("""{ "b": [1, 2.5, -3e2], "a": { "x": "ñá 😀", "y": true, "z": null } }""");

        root.Kind.ShouldBe(JsonKind.Object);
        root.Members.Select(m => m.Key).ShouldBe(["b", "a"]);
        root["b"]!.Items.Select(i => i.NumberValue).ShouldBe([1d, 2.5d, -300d]);
        root["b"]!.Items[0].TryGetInt64(out var one).ShouldBeTrue();
        one.ShouldBe(1);
        root["a"]!["x"]!.StringValue.ShouldBe("ñá 😀");
        root["a"]!["y"]!.BooleanValue.ShouldBeTrue();
        root["a"]!["z"]!.Kind.ShouldBe(JsonKind.Null);
    }

    [Fact]
    public void Reports_line_and_column_of_values()
    {
        var root = MiniJson.Parse("{\n  \"k\": 42\n}");

        root["k"]!.Line.ShouldBe(2);
        root["k"]!.Column.ShouldBe(8);
    }

    [Theory]
    [InlineData("{ \"a\": 1, \"a\": 2 }", 1, 11)]
    [InlineData("[1, 2", 1, 6)]
    [InlineData("{\n\"a\": tru }", 2, 6)]
    [InlineData("01", 1, 2)]
    public void Rejects_invalid_documents_with_position(string json, int line, int column)
    {
        var ex = Should.Throw<JsonParseException>(() => MiniJson.Parse(json));

        ex.Line.ShouldBe(line);
        ex.Column.ShouldBe(column);
    }
}
