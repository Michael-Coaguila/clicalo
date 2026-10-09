using Clicalo.Generators.Localization;
using Clicalo.TestKit;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Tests.Localization;

public sealed class LocalizationGeneratorTests
{
    [Fact]
    [Trait("Req", "IDI-004")]
    public void Valid_data_generates_typed_members_that_compile_without_diagnostics()
    {
        var output = GeneratorHarness.Run(TestData.Valid());

        output.Diagnostics.ShouldBeEmpty();
        output.CompilationErrors.ShouldBeEmpty();
        var messages = output.Source(".L.g.cs");
        messages.ShouldContain(
            "public static Message Search { get; } = new(new MessageKey(\"search\"));"
        );
        messages.ShouldContain("public static Message ComboN(long count) =>");
        messages.ShouldContain("public static Message Zoom(decimal ratio) =>");
        messages.ShouldContain("public static Message CreateFor(MessageText app) =>");
        output
            .Source(".MessageKey.g.cs")
            .ShouldContain("public readonly record struct MessageKey(string Value)");
        output
            .Source(".MessageCatalog.g.cs")
            .ShouldContain(
                "new(new MessageKey(\"comboN\"), true, new MessageParameter(\"count\", MessageArgumentType.WholeNumber)),"
            );
    }

    [Fact]
    public void Generated_members_document_the_default_language_text_and_the_expected_arguments()
    {
        var messages = GeneratorHarness.Run(TestData.Valid()).Source(".L.g.cs");

        messages.ShouldContain(
            """
                /// <summary>
                /// <para><c>one</c>: {count} tecla</para>
                /// <para><c>other</c>: {count} teclas</para>
                /// </summary>
                /// <remarks>Key <c>comboN</c>; plural family selected by <c>count</c>.</remarks>
                /// <param name="count">Cantidad.</param>
                public static Message ComboN(long count) =>
                    new(
                        new MessageKey("comboN"),
                        MessageArgument.WholeNumber("count", count)
                    );
            """.Replace("\r\n", "\n", StringComparison.Ordinal)
        );
        messages.ShouldContain("/// <summary>Importado: {profiles} &amp; &lt;más&gt;</summary>");
    }

    [Fact]
    public void Output_is_deterministic_and_independent_of_the_order_of_the_files()
    {
        var forward = GeneratorHarness.Run(TestData.Valid());
        var reversed = GeneratorHarness.Run(
            TestData.Valid().Reverse().ToDictionary(StringComparer.Ordinal)
        );

        var expected = forward
            .Sources.Select(static s => s.HintName + "\n" + s.SourceText)
            .ToList();
        reversed.Sources.Select(static s => s.HintName + "\n" + s.SourceText).ShouldBe(expected);
        forward.Sources.ShouldAllBe(s =>
            !s.SourceText.ToString().Contains('\r', StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Projects_without_the_Domain_profile_get_nothing()
    {
        var output = GeneratorHarness.Run(TestData.Valid(), profile: "UiWpf");

        output.Sources.ShouldBeEmpty();
        output.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Without_i18n_files_only_the_empty_types_are_generated()
    {
        var output = GeneratorHarness.Run(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["other.json"] = "{}" }
        );

        output.Diagnostics.ShouldBeEmpty();
        output.Sources.Count.ShouldBe(3);
        output.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    [Trait("Req", "IDI-004")]
    public void Repository_data_generates_every_key_without_diagnostics()
    {
        var files = Directory
            .EnumerateFiles(Path.Combine(RepoPaths.Data, "i18n"))
            .ToDictionary(
                static p => Path.GetFileName(p),
                File.ReadAllText,
                StringComparer.Ordinal
            );

        var output = GeneratorHarness.Run(files);

        output.Diagnostics.Select(GeneratorOutput.Describe).ShouldBeEmpty();
        output.CompilationErrors.ShouldBeEmpty();
        var messages = output.Source(".L.g.cs");
        CountOccurrences(messages, "    public static Message ").ShouldBe(831);
        messages.ShouldContain(
            "public static Message ProcessTaken(MessageText profile, MessageText process) =>"
        );
        messages.ShouldContain(
            "public static Message DupHead(long count, long index, long total) =>"
        );
        messages.ShouldContain("public static Message SugLine(MessageText app, long count) =>");
        messages.ShouldContain("public static Message PhStep(long index, long total) =>");
    }

    [Fact]
    public void Every_issue_id_has_an_error_descriptor_with_a_help_link()
    {
        var ids = typeof(LocalizationIds)
            .GetFields()
            .Select(static f => (string)f.GetRawConstantValue()!)
            .ToList();

        ids.Count.ShouldBe(15);
        foreach (var id in ids)
        {
            var descriptor = LocalizationDiagnostics.For(id);
            descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Error);
            descriptor.HelpLinkUri.ShouldEndWith("#" + id.ToLowerInvariant());
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (
            var i = text.IndexOf(value, StringComparison.Ordinal);
            i >= 0;
            i = text.IndexOf(value, i + 1, StringComparison.Ordinal)
        )
        {
            count++;
        }

        return count;
    }
}
