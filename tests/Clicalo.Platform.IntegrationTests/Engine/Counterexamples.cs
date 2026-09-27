using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// Runs a seeded property over many cases and, when one fails, reduces it before failing (blueprint §7.10, item 1: «Los
/// contraejemplos reducidos se guardan como regresiones»). The reduction is greedy, like CsCheck's: it keeps taking the
/// first smaller candidate that still fails until none does. The failure names the reduced case, ready to be added to
/// the regression theory of the test. CsCheck itself is not a dependency this project may take yet
/// (architecture/allowed-dependencies.json); the cases are the same deterministic seeds the property always ran.
/// </summary>
internal static class Counterexamples
{
    /// <summary>
    /// Checks <paramref name="property"/> for every case of <paramref name="cases"/> in parallel; on a failure, reduces
    /// the first failing case with <paramref name="smaller"/> and throws <see cref="CounterexampleException"/>.
    /// </summary>
    /// <typeparam name="T">A case.</typeparam>
    /// <param name="cases">How many cases; case <c>i</c> is <paramref name="generate"/>(<c>i</c>).</param>
    /// <param name="generate">The case of an index.</param>
    /// <param name="property">Throws when the case breaks the property.</param>
    /// <param name="smaller">Candidates strictly smaller than a case, the most promising first.</param>
    /// <param name="describe">The case as the regression data to paste.</param>
    public static void ForAll<T>(
        int cases,
        Func<int, T> generate,
        Action<T> property,
        Func<T, IEnumerable<T>> smaller,
        Func<T, string> describe
    )
    {
        ArgumentNullException.ThrowIfNull(generate);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(smaller);
        ArgumentNullException.ThrowIfNull(describe);
        var firstFailure = -1;
        var gate = new Lock();
        Parallel.For(
            0,
            cases,
            (index, loop) =>
            {
                if (Fails(property, generate(index)) is null)
                {
                    return;
                }

                lock (gate)
                {
                    if (firstFailure < 0 || index < firstFailure)
                    {
                        firstFailure = index;
                    }
                }

                // Break, not Stop: every lower index still runs, so the first failure is the lowest one.
                loop.Break();
            }
        );

        if (firstFailure < 0)
        {
            return;
        }

        var original = generate(firstFailure);
        var (reduced, failure, steps) = Reduce(original, property, smaller);
        throw new CounterexampleException(
            "Case "
                + describe(original)
                + " fails; reduced to "
                + describe(reduced)
                + " after "
                + steps.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " reductions"
                + ". Keep it as a regression: add it to the InlineData of the test. "
                + failure.Message,
            failure
        );
    }

    /// <summary>Greedily reduces a failing case; returns the reduced case, its failure and how many reductions it took.</summary>
    /// <typeparam name="T">A case.</typeparam>
    /// <param name="failing">A case that fails.</param>
    /// <param name="property">Throws when the case breaks the property.</param>
    /// <param name="smaller">Candidates strictly smaller than a case, the most promising first.</param>
    public static (T Reduced, Exception Failure, int Steps) Reduce<T>(
        T failing,
        Action<T> property,
        Func<T, IEnumerable<T>> smaller
    )
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(smaller);
        var current = failing;
        var failure =
            Fails(property, current)
            ?? throw new ArgumentException("The case to reduce does not fail.", nameof(failing));
        var steps = 0;
        var reduced = true;
        while (reduced)
        {
            reduced = false;
            foreach (var candidate in smaller(current))
            {
                if (Fails(property, candidate) is { } candidateFailure)
                {
                    current = candidate;
                    failure = candidateFailure;
                    steps++;
                    reduced = true;
                    break;
                }
            }
        }

        return (current, failure, steps);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Any exception is a failing case: the assertions of the property and its crashes alike."
    )]
    private static Exception? Fails<T>(Action<T> property, T value)
    {
        try
        {
            property(value);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
