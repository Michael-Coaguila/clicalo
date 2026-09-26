using System.Text.RegularExpressions;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Tests.Views;

/// <summary>
/// Voice never has to choose: every UI Automation name of SpikeLab is unique across all its windows (only «Dictar»
/// repeats, next to each free text field, as UIA010 requires), every «clic X» of the scripts reaches exactly one
/// element whose name no other element contains, and every phrase the scripts ask to say is a command, so nothing is
/// dictated into the app under test by accident.
/// </summary>
public sealed class UiaNameUniquenessTests
{
    private const RegexOptions Options =
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex ClickOrder = new("«clic (?<order>[^»]+)»", Options, MatchTimeout);

    private static readonly Regex SaidPhrase = new(
        "(?<!\\p{L})[Dd]i( con Acceso por voz)? «(?<phrase>[^»]+)»",
        Options,
        MatchTimeout
    );

    private static readonly string[] Commands =
    [
        "clic",
        "mostrar números",
        "mostrar números en todas partes",
        "ocultar números",
    ];

    public static TheoryData<string> Spikes => ["S1", "S3", "S4"];

    [Fact]
    public void Every_interactive_name_is_unique_across_the_windows_of_SpikeLab() =>
        WpfThread.Invoke(() =>
        {
            var names = SpikeLabNames.Collect();

            names
                .Where(entry =>
                    !string.Equals(entry.Name, SpikeLabNames.Dictation, StringComparison.Ordinal)
                )
                .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group =>
                    group.Key + " en " + string.Join(", ", group.Select(entry => entry.Window))
                )
                .ShouldBeEmpty();
            names.Count.ShouldBeGreaterThan(40, "the walk reached the tiles and the buttons");
        });

    [Fact]
    public void Only_the_dictation_buttons_repeat_and_only_next_to_their_fields() =>
        WpfThread.Invoke(() =>
            SpikeLabNames
                .Collect()
                .Where(entry =>
                    string.Equals(entry.Name, SpikeLabNames.Dictation, StringComparison.Ordinal)
                )
                .Select(entry => entry.Window)
                .ShouldBe(
                    [
                        "SideWindow#2",
                        "Centro de control de laboratorio",
                        "Centro de control de laboratorio",
                        "Centro de control de laboratorio",
                    ],
                    ignoreOrder: true
                )
        );

    [Theory]
    [MemberData(nameof(Spikes))]
    public void Every_click_order_of_a_script_reaches_exactly_one_element(string spike) =>
        WpfThread.Invoke(() =>
        {
            var id = Enum.Parse<SpikeId>(spike);
            var names = SpikeLabNames.Collect(id).Select(entry => entry.Name).ToArray();
            var orders = Orders(id).ToArray();

            orders.ShouldNotBeEmpty();
            foreach (var order in orders)
            {
                names
                    .Count(name => string.Equals(name, order, StringComparison.OrdinalIgnoreCase))
                    .ShouldBe(1, spike + ": «clic " + order + "»");
                names
                    .Where(name =>
                        !string.Equals(name, order, StringComparison.OrdinalIgnoreCase)
                        && name.Contains(order, StringComparison.OrdinalIgnoreCase)
                    )
                    .ShouldBeEmpty(spike + ": another name contains «" + order + "»");
            }
        });

    [Theory]
    [MemberData(nameof(Spikes))]
    public void Every_phrase_a_script_asks_to_say_is_a_voice_command(string spike)
    {
        foreach (var step in SpikeScripts.For(Enum.Parse<SpikeId>(spike)).Steps)
        {
            foreach (Match said in SaidPhrase.Matches(step.Instruction))
            {
                var phrase = said.Groups["phrase"].Value;
                (
                    Commands.Contains(phrase, StringComparer.Ordinal)
                    || phrase.StartsWith("clic ", StringComparison.Ordinal)
                    || phrase.StartsWith("pulsa ", StringComparison.Ordinal)
                ).ShouldBeTrue(spike + " row " + step.Id + ": «" + phrase + "» would be dictated");
            }
        }
    }

    [Fact]
    public void Soltar_todo_is_never_said_without_clic()
    {
        foreach (var script in SpikeScripts.All)
        {
            foreach (var step in script.Steps)
            {
                step.Instruction.ShouldNotContain("di «Soltar", Case.Insensitive);
            }
        }
    }

    private static IEnumerable<string> Orders(SpikeId spike) =>
        SpikeScripts
            .For(spike)
            .Steps.SelectMany(step => ClickOrder.Matches(step.Instruction))
            .Select(match => match.Groups["order"].Value)
            .Where(order => !char.IsDigit(order[0]))
            .Distinct(StringComparer.Ordinal);
}
