using System.Text;
using Clicalo.TestKit;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Tests.Scripting;

/// <summary>
/// The scripts S1.md, S3.md and S4.md say exactly what the guide strip says: their «Recorrido» is the list of the rows
/// of <see cref="SpikeScripts"/>, title and instruction, between two markers, and their results table has the same
/// rows. With <c>CLICALO_UPDATE_SPIKE_ROUTES=1</c> the test writes the list into the document instead of comparing it
/// (the documents are generated from the same source as the laboratory).
/// </summary>
public sealed class SpikeScriptDocumentTests
{
    /// <summary>First line of the generated block (its comment says where it comes from).</summary>
    public const string StartMarker = "<!-- spikelab:recorrido";

    /// <summary>Last line of the generated block.</summary>
    public const string EndMarker = "<!-- /spikelab:recorrido -->";

    private const string UpdateVariable = "CLICALO_UPDATE_SPIKE_ROUTES";

    public static TheoryData<string> Spikes => ["S1", "S3", "S4"];

    [Theory]
    [MemberData(nameof(Spikes))]
    public void The_route_of_the_document_is_the_text_of_the_guide_strip(string spike)
    {
        var script = SpikeScripts.For(Enum.Parse<SpikeId>(spike));
        var path = RepoPaths.Combine(script.Document);
        var document = File.ReadAllText(path).ReplaceLineEndings("\n");
        var start = document.IndexOf(StartMarker, StringComparison.Ordinal);
        var end = document.IndexOf(EndMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(
            0,
            script.Document + " has no «" + StartMarker + "» marker"
        );
        end.ShouldBeGreaterThan(start, script.Document + " has no «" + EndMarker + "» marker");

        var blockStart = document.IndexOf('\n', start) + 1;
        var expected = Route(script);
        if (
            string.Equals(
                Environment.GetEnvironmentVariable(UpdateVariable),
                "1",
                StringComparison.Ordinal
            )
        )
        {
            File.WriteAllText(
                path,
                document[..blockStart] + expected + document[end..],
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );
            return;
        }

        document[blockStart..end]
            .ShouldBe(
                expected,
                "The «Recorrido» of "
                    + script.Document
                    + " differs from SpikeScripts.cs. Run the tests with "
                    + UpdateVariable
                    + "=1 to write it, or paste:\n"
                    + expected
            );
    }

    [Theory]
    [MemberData(nameof(Spikes))]
    public void The_results_table_of_the_document_has_one_row_per_step(string spike)
    {
        var script = SpikeScripts.For(Enum.Parse<SpikeId>(spike));
        var lines = File.ReadAllLines(RepoPaths.Combine(script.Document));
        var manual = Array.FindIndex(
            lines,
            line => line.StartsWith("### Manuales", StringComparison.Ordinal)
        );
        manual.ShouldBeGreaterThan(0);

        var rows = lines
            .Skip(manual)
            .SkipWhile(line => !line.StartsWith("| # |", StringComparison.Ordinal))
            .Skip(2)
            .TakeWhile(line => line.StartsWith('|'))
            .Select(line => line.Split('|')[1].Trim());

        rows.ShouldBe(script.Steps.Select(step => step.Id));
    }

    /// <summary>The block between the markers: one line per row, then a blank line.</summary>
    internal static string Route(SpikeScript script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var text = new StringBuilder("\n");
        foreach (var step in script.Steps)
        {
            text.Append("- **Fila ")
                .Append(step.Id)
                .Append(" · ")
                .Append(step.Title)
                .Append(".** ")
                .Append(step.Instruction)
                .Append('\n');
        }

        return text.Append('\n').ToString();
    }
}
