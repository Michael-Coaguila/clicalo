using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Document;

/// <summary>
/// What the last welcome answered (BIE-010, ADR-0028, schema 1.1): a repeated welcome shows these answers preselected
/// and uses <see cref="Baseline"/> to apply the effects of step 1 only to the settings nobody changed by hand since.
/// </summary>
/// <param name="Uses">The marked options of step 1, in the order of <see cref="WelcomeAnswer"/>, without repeats.</param>
/// <param name="Kit">
/// The marked option ids of step 2 (<c>StarterKit.Options</c>: <c>basics</c> and template ids), in ordinal order,
/// without repeats or empty ids. Unmarking an installed option never uninstalls it (REG-08).
/// </param>
/// <param name="Baseline">The settings step 1 changes, as the welcome left them.</param>
public sealed record WelcomeAnswers(
    ValueList<WelcomeAnswer> Uses,
    ValueList<string> Kit,
    WelcomeBaseline Baseline
)
{
    /// <summary>
    /// The answers in canonical form: <paramref name="uses"/> defined, ordered and once each; <paramref name="kit"/>
    /// without empty ids, ordinally ordered and once each.
    /// </summary>
    /// <param name="uses">The marked options of step 1.</param>
    /// <param name="kit">The marked option ids of step 2.</param>
    /// <param name="baseline">The settings step 1 changes, as the welcome left them.</param>
    public static WelcomeAnswers Create(
        IEnumerable<WelcomeAnswer> uses,
        IEnumerable<string> kit,
        WelcomeBaseline baseline
    )
    {
        ArgumentNullException.ThrowIfNull(uses);
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentNullException.ThrowIfNull(baseline);
        return new(
            ValueListBuilder.From(uses.Where(Enum.IsDefined).Distinct().Order()),
            ValueListBuilder.From(
                kit.Where(static id => !string.IsNullOrEmpty(id))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
            ),
            baseline
        );
    }
}
