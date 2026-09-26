namespace Clicalo.Build.Tests;

public sealed class MarkdownTests
{
    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData(
        "List<int> is *bold* `code` [link] a|b",
        "List\\<int\\> is \\*bold\\* \\`code\\` \\[link\\] a\\|b"
    )]
    [InlineData("C:\\dev", "C:\\\\dev")]
    [InlineData("two\r\nlines\nhere", "two lines here")]
    public void Text_escapes_only_what_changes_the_rendering(string input, string expected) =>
        Markdown.Text(input).ShouldBe(expected);

    [Theory]
    [InlineData("Fails_when_empty", "Fails_when_empty")]
    [InlineData("_leading", "\\_leading")]
    [InlineData("trailing_", "trailing\\_")]
    [InlineData("a _b_ c", "a \\_b\\_ c")]
    public void Underscores_inside_words_stay_readable(string input, string expected) =>
        Markdown.Text(input).ShouldBe(expected);

    [Fact]
    public void Code_blocks_use_a_fence_longer_than_any_backtick_run()
    {
        var block = Markdown.CodeBlock("a ``` b\r\n");

        block.ShouldBe("````text\na ``` b\n````");
    }

    [Theory]
    [InlineData("dotnet build", "`dotnet build`")]
    [InlineData("a `b` c", "``a `b` c``")]
    [InlineData("`edge", "`` `edge ``")]
    public void Inline_code_is_safe_for_any_content(string input, string expected) =>
        Markdown.InlineCode(input).ShouldBe(expected);

    [Fact]
    public void Links_keep_paths_with_spaces_valid() =>
        Markdown
            .Link("docs/a b.md", "..\\..\\docs\\a b.md#L3")
            .ShouldBe("[docs/a b.md](<../../docs/a b.md#L3>)");
}
