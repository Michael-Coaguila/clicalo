namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>The reduction of <see cref="Counterexamples"/>: a failing case is reported at its smallest.</summary>
public sealed class CounterexamplesTests
{
    [Fact]
    public void A_failure_is_reduced_to_the_smallest_case_that_still_fails()
    {
        // Fails from 7 steps on: the reduction of 150 must stop at 7.
        static void Property(int steps) => steps.ShouldBeLessThan(7);

        var (reduced, failure, reductions) = Counterexamples.Reduce(
            150,
            Property,
            static steps => Enumerable.Range(1, steps - 1)
        );

        reduced.ShouldBe(7);
        reductions.ShouldBe(1);
        failure.ShouldBeOfType<ShouldAssertException>();
    }

    [Fact]
    public void A_failing_property_names_its_reduced_case_and_a_passing_one_is_quiet()
    {
        var failure = Should.Throw<CounterexampleException>(() =>
            Counterexamples.ForAll(
                500,
                static index => (Seed: index, Steps: 199 - (index % 50)),
                static c => (c.Seed < 300 || c.Steps < 120).ShouldBeTrue(),
                static c =>
                    Enumerable.Range(1, c.Steps - 1).Select(steps => c with { Steps = steps }),
                static c =>
                    $"({c.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {c.Steps.ToString(System.Globalization.CultureInfo.InvariantCulture)})"
            )
        );

        failure.Message.ShouldStartWith(
            "Case (300, 199) fails; reduced to (300, 120) after 1 reductions."
        );
        Should.NotThrow(() =>
            Counterexamples.ForAll(
                100,
                static index => index,
                static index => index.ShouldBeLessThan(100),
                static index => Enumerable.Range(0, index),
                static index => index.ToString(System.Globalization.CultureInfo.InvariantCulture)
            )
        );
    }
}
