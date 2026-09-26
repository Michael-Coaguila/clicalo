namespace Clicalo.Build.Tests;

/// <summary>The one line every run ends with is read aloud by Narrator (blueprint §13).</summary>
public sealed class FinalLineTests
{
    [Fact]
    public void Success_says_the_verb_and_the_duration() =>
        Messages.Success("check", "1 min 12 s").ShouldBe("cl check: correcto en 1 min 12 s");

    [Fact]
    public void Failure_says_the_step_and_where_the_details_are() =>
        Messages
            .Failure("check", "build", Path.Combine("artifacts", "cl", "last-error.md"))
            .ShouldBe(
                "cl check: falló en build; detalle en "
                    + Path.Combine("artifacts", "cl", "last-error.md")
            );

    [Fact]
    public void Future_verbs_say_their_milestone() =>
        Messages.NotYetAvailable("run", "M2").ShouldBe("cl run: disponible en M2");

    [Fact]
    public void Notes_follow_the_line_after_semicolons() =>
        Messages
            .WithNotes("cl test: correcto en 9 s", ["12 pruebas", "superó el objetivo de 45 s"])
            .ShouldBe("cl test: correcto en 9 s; 12 pruebas; superó el objetivo de 45 s");

    [Fact]
    public void Without_notes_the_line_is_unchanged() =>
        Messages.WithNotes("cl build: correcto en 16 s", []).ShouldBe("cl build: correcto en 16 s");

    [Theory]
    [InlineData(0, "ninguna prueba ejecutada")]
    [InlineData(1, "1 prueba")]
    [InlineData(12, "12 pruebas")]
    public void Test_counts_agree_in_number(int count, string expected) =>
        Messages.TestCount(count).ShouldBe(expected);

    [Theory]
    [InlineData(new string[0], "")]
    [InlineData(new[] { "a" }, "a")]
    [InlineData(new[] { "a", "b" }, "a y b")]
    [InlineData(new[] { "a", "b", "c" }, "a, b y c")]
    public void Lists_are_joined_as_spoken_in_Spanish(string[] items, string expected) =>
        Messages.JoinList(items).ShouldBe(expected);

    [Fact]
    public void Final_lines_have_no_symbols_that_screen_readers_spell_out()
    {
        string[] lines =
        [
            Messages.Success("check", "1 min 12 s"),
            Messages.Failure("check", "build", "artifacts/cl/last-error.md"),
            Messages.NotYetAvailable("run", "M2"),
            Messages.UnknownVerb("foo", "setup y build"),
            Messages.VerbList("setup y build"),
        ];

        foreach (var line in lines)
        {
            line.ShouldNotContain("✓");
            line.ShouldNotContain("✗");
            line.ShouldNotContain("─");
            line.ShouldNotContain("\n");
            line.ShouldStartWith("cl");
        }
    }
}
