using Clicalo.Design.Math;

namespace Clicalo.Data.Tests.Tokens;

/// <summary><c>contrast-pairs.json</c>, read independently of the generator, and its evaluation.</summary>
internal sealed class ContrastPairSet
{
    public const string CategoryTint = "categoryTint";
    public const string CategoryWash = "categoryWash";

    private ContrastPairSet(
        double textMinimum,
        double graphicMinimum,
        IReadOnlyList<Rgba8> backdrops,
        IReadOnlyList<PairSpec> pairs,
        IReadOnlyDictionary<string, string> decorative
    )
    {
        TextMinimum = textMinimum;
        GraphicMinimum = graphicMinimum;
        Backdrops = backdrops;
        Pairs = pairs;
        Decorative = decorative;
    }

    public double TextMinimum { get; }

    public double GraphicMinimum { get; }

    public IReadOnlyList<Rgba8> Backdrops { get; }

    public IReadOnlyList<PairSpec> Pairs { get; }

    public IReadOnlyDictionary<string, string> Decorative { get; }

    public static ContrastPairSet Load()
    {
        using var document = TokenDataSet.Read("contrast-pairs.json");
        var root = document.RootElement;
        var minimums = root.GetProperty("minimums");
        var backdrops = root.GetProperty("backdrops")
            .EnumerateArray()
            .Select(item => TokenDataSet.Parse(item.GetString()!).ToRgba8())
            .ToList();
        var pairs = root.GetProperty("pairs")
            .EnumerateArray()
            .Select(pair => new PairSpec(
                string.Equals(
                    pair.GetProperty("kind").GetString(),
                    "text",
                    StringComparison.Ordinal
                ),
                pair.GetProperty("foreground").GetString()!,
                [
                    .. pair.GetProperty("backgrounds")
                        .EnumerateArray()
                        .Select(background => background.GetString()!),
                ],
                pair.GetProperty("use").GetString()!
            ))
            .ToList();
        var decorative = TokenDataSet
            .DataMembers(root.GetProperty("decorative"))
            .ToDictionary(
                member => member.Name,
                member => member.Value.GetString()!,
                StringComparer.Ordinal
            );
        return new ContrastPairSet(
            minimums.GetProperty("text").GetDouble(),
            minimums.GetProperty("graphic").GetDouble(),
            backdrops,
            pairs,
            decorative
        );
    }

    public static IReadOnlyList<string> Layers(string background) =>
        [.. background.Split(" over ", StringSplitOptions.None).Select(layer => layer.Trim())];

    /// <summary>
    /// Measures every pair, background and category of <paramref name="theme"/> over the backdrops of the data,
    /// or over <paramref name="backdrops"/> when given.
    /// </summary>
    public IEnumerable<ContrastResult> Evaluate(
        TokenDataSet data,
        string theme,
        IReadOnlyList<Rgba8>? backdrops = null
    )
    {
        foreach (var pair in Pairs)
        {
            foreach (var background in pair.Backgrounds)
            {
                var layers = Layers(background);
                var perCategory = IsCategory(pair.Foreground) || layers.Any(IsCategory);
                IEnumerable<string> categories = perCategory
                    ? data.Categories.Hues.Keys
                    : [string.Empty];
                foreach (var category in categories)
                {
                    var measurement = ContrastEvaluator.Measure(
                        ColorOf(data, theme, pair.Foreground, category),
                        [.. layers.Select(layer => ColorOf(data, theme, layer, category))],
                        backdrops ?? Backdrops
                    );
                    yield return new ContrastResult(
                        theme,
                        pair,
                        background,
                        category,
                        measurement.Ratio,
                        pair.IsText ? TextMinimum : GraphicMinimum
                    );
                }
            }
        }
    }

    public IEnumerable<ContrastResult> EvaluateAll(TokenDataSet data) =>
        data.Themes.SelectMany(theme => Evaluate(data, theme));

    private static bool IsCategory(string token) => token is CategoryTint or CategoryWash;

    private static Rgba8 ColorOf(TokenDataSet data, string theme, string token, string category) =>
        token switch
        {
            CategoryTint => data.Tint(theme, category),
            CategoryWash => data.Wash(theme, category),
            _ => data.Color(theme, token),
        };
}
