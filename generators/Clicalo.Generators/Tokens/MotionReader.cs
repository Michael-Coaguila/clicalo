using System.Collections.Generic;
using Clicalo.Generators.Common;

namespace Clicalo.Generators.Tokens;

/// <summary>Reads <c>motion.json</c>: every duration and its reduced-motion value (TEM-006).</summary>
internal static class MotionReader
{
    /// <summary>Longest accepted duration; anything longer is a typo, not a transition.</summary>
    private const long MaxMilliseconds = 10_000;

    public static IReadOnlyList<MotionModel> Read(TokenDocument document, TokenIssues issues)
    {
        var result = new List<MotionModel>();
        var durations = JsonShape.Required(
            document,
            document.Root,
            "durations",
            JsonKind.Object,
            issues
        );
        if (durations is null)
        {
            return result;
        }

        foreach (var member in JsonShape.DataMembers(durations))
        {
            if (!JsonShape.IsIdentifier(member.Key))
            {
                issues.Malformed(
                    document,
                    member.Value,
                    "the duration '" + member.Key + "' must be a camelCase identifier."
                );
                continue;
            }

            if (
                !JsonShape.Expect(
                    document,
                    member.Value,
                    JsonKind.Object,
                    "The duration '" + member.Key + "'",
                    issues
                )
            )
            {
                continue;
            }

            JsonShape.AllowOnly(document, member.Value, issues, "ms", "reducedMs", "use");
            var ms = Milliseconds(document, member.Value, "ms", issues);
            var reduced = Milliseconds(document, member.Value, "reducedMs", issues);
            var use = JsonShape.Required(document, member.Value, "use", JsonKind.String, issues);
            if (ms is null || reduced is null || use is null)
            {
                continue;
            }

            if (reduced.Value > ms.Value)
            {
                issues.Malformed(
                    document,
                    member.Value["reducedMs"]!,
                    "'reducedMs' of '"
                        + member.Key
                        + "' cannot be longer than 'ms': reduced motion never slows anything down."
                );
                continue;
            }

            result.Add(
                new MotionModel(member.Key, (int)ms.Value, (int)reduced.Value, use.StringValue!)
            );
        }

        return result;
    }

    private static long? Milliseconds(
        TokenDocument document,
        JsonNode owner,
        string name,
        TokenIssues issues
    )
    {
        var node = JsonShape.Required(document, owner, name, JsonKind.Number, issues);
        if (node is null)
        {
            return null;
        }

        if (!node.TryGetInt64(out var value) || value < 0 || value > MaxMilliseconds)
        {
            issues.Malformed(
                document,
                node,
                "'" + name + "' must be a whole number of milliseconds from 0 to 10000."
            );
            return null;
        }

        return value;
    }
}
