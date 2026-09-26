using System.Collections.Immutable;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>The expectations of one window in one state, keyed by AutomationId.</summary>
public sealed record UiaExpectations
{
    /// <summary>Per-element expectations.</summary>
    public ImmutableDictionary<string, UiaExpectation> Elements { get; init; } =
        ImmutableDictionary.Create<string, UiaExpectation>(StringComparer.Ordinal);

    /// <summary>
    /// Accepted names of the dictation button next to a free text field (UIA010): the localized «Dictar» (and
    /// «Pegar» for the AI key field).
    /// </summary>
    public ImmutableArray<string> DictationNames { get; init; } = [];

    /// <summary>Smallest touch target side, in logical pixels (UIA005, REG-02).</summary>
    public double MinimumTargetSize { get; init; } = 44;

    /// <summary>Expectations built from <paramref name="elements"/>.</summary>
    public static UiaExpectations For(IEnumerable<UiaExpectation> elements) =>
        new()
        {
            Elements = elements.ToImmutableDictionary(
                element => element.AutomationId,
                StringComparer.Ordinal
            ),
        };

    /// <summary>The expectation of <paramref name="node"/>, if any.</summary>
    public UiaExpectation? Of(UiaNode node) =>
        node.AutomationId.Length > 0 && Elements.TryGetValue(node.AutomationId, out var expected)
            ? expected
            : null;
}
