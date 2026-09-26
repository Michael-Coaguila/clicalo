using System.Diagnostics.CodeAnalysis;
using Clicalo.Tools.SpikeLab.Composition;
using Microsoft.Extensions.Logging;

namespace Clicalo.Tools.SpikeLab.Tests.Composition;

public sealed class ConstructorBinderTests
{
    [Fact]
    public void The_largest_satisfiable_constructor_wins_and_services_match_by_type()
    {
        var clock = TimeProvider.System;
        var registry = new Registry();

        var result = ConstructorBinder.Create(typeof(Orchestrator), [registry, clock]);

        var orchestrator = result.Instance.ShouldBeOfType<Orchestrator>();
        result.Problem.ShouldBeNull();
        orchestrator.Style.ShouldBeSameAs(registry);
        orchestrator.Lookup.ShouldBeSameAs(registry);
        orchestrator.Clock.ShouldBeSameAs(clock);
        orchestrator.Logger.ShouldNotBeNull();
    }

    [Fact]
    public void A_missing_service_falls_back_to_a_smaller_constructor()
    {
        var result = ConstructorBinder.Create(typeof(Orchestrator), [new Registry()]);

        result.Instance.ShouldBeOfType<Orchestrator>().Clock.ShouldBeNull();
    }

    [Fact]
    public void An_unsatisfiable_type_names_the_first_missing_parameter()
    {
        var result = ConstructorBinder.Create(typeof(NeedsClock), []);

        result.Instance.ShouldBeNull();
        result.Problem.ShouldBe("No service for parameter «clock» (TimeProvider) of NeedsClock.");
    }

    [Fact]
    public void Optional_parameters_take_their_default()
    {
        var result = ConstructorBinder.Create(typeof(WithDefault), []);

        result.Instance.ShouldBeOfType<WithDefault>().Retries.ShouldBe(1);
    }

    [Fact]
    public void An_exception_of_the_constructor_is_not_wrapped() =>
        Should.Throw<NotImplementedException>(() => ConstructorBinder.Create(typeof(Pending), []));

    private interface IStyle;

    private interface ILookup;

    private sealed class Registry : IStyle, ILookup;

    private sealed class Orchestrator
    {
        public Orchestrator(
            IStyle style,
            ILookup lookup,
            TimeProvider clock,
            ILogger<Orchestrator> logger
        )
        {
            Style = style;
            Lookup = lookup;
            Clock = clock;
            Logger = logger;
        }

        public Orchestrator(IStyle style, ILookup lookup)
        {
            Style = style;
            Lookup = lookup;
        }

        public IStyle Style { get; }

        public ILookup Lookup { get; }

        public TimeProvider? Clock { get; }

        public ILogger? Logger { get; }
    }

    private sealed class NeedsClock(TimeProvider clock)
    {
        public TimeProvider Clock { get; } = clock;
    }

    private sealed class WithDefault(int retries = 1)
    {
        public int Retries { get; } = retries;
    }

    [SuppressMessage(
        "Design",
        "MA0025:Implement the functionality",
        Justification = "Simulates an M1 contract stub, which throws NotImplementedException until its package is merged."
    )]
    private sealed class Pending
    {
        public Pending() => throw new NotImplementedException("M1 foreground package.");
    }
}
