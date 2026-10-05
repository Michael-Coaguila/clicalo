using Clicalo.Domain.Dimming;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Tests.Dimming;

/// <summary>
/// <see cref="DimPolicy"/> with the full table of exceptions of docs/04 «Opacidad y atenuado» (blueprint §6.4).
/// </summary>
public sealed class DimPolicyTests
{
    private const double Opacity = 0.92;
    private const double DimTo = 0.35;

    private static readonly DateTimeOffset Left = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Every exception of docs/04, one by one.</summary>
    public static TheoryData<DimExceptions> Exceptions =>
        [
            DimExceptions.Panic,
            DimExceptions.QuickSettings,
            DimExceptions.ContextMenu,
            DimExceptions.ProfileGrid,
            DimExceptions.Search,
            DimExceptions.EditMode,
            DimExceptions.DockSideWindows,
            DimExceptions.ControlCenterOpen,
            DimExceptions.WelcomeOpen,
        ];

    /// <summary>Every surface that dims.</summary>
    public static TheoryData<DimSurface> Surfaces =>
        [DimSurface.Panel, DimSurface.Dock, DimSurface.DockHandle, DimSurface.Bubble];

    [Fact]
    [Trait("Req", "GEN-009")]
    public void A_surface_dims_two_and_a_half_seconds_after_the_finger_leaves_it()
    {
        var before = DimPolicy.Evaluate(
            Inputs(now: Left + Timings.Dimming.DimDelay - TimeSpan.FromMilliseconds(1))
        );
        before.Dimmed.ShouldBeFalse();
        before.TargetOpacity.ShouldBe(Opacity);
        before.NextEvaluationAt.ShouldBe(Left + TimeSpan.FromSeconds(2.5));

        var after = DimPolicy.Evaluate(Inputs(now: Left + Timings.Dimming.DimDelay));
        after.Dimmed.ShouldBeTrue();
        after.TargetOpacity.ShouldBe(DimTo);
        after.NextEvaluationAt.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    public void A_finger_or_the_pointer_on_the_surface_wakes_it()
    {
        var decision = DimPolicy.Evaluate(Inputs(pointerInside: true));

        decision.Dimmed.ShouldBeFalse();
        decision.TargetOpacity.ShouldBe(Opacity);
        decision.NextEvaluationAt.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    public void Without_auto_dim_or_before_any_leave_nothing_dims()
    {
        DimPolicy.Evaluate(Inputs(autoDim: false)).TargetOpacity.ShouldBe(Opacity);
        var neverLeft = DimPolicy.Evaluate(Inputs() with { LastLeave = null });
        neverLeft.Dimmed.ShouldBeFalse();
        neverLeft.NextEvaluationAt.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    public void The_dimmed_opacity_is_the_lower_of_dim_to_and_the_opacity()
    {
        DimPolicy
            .Evaluate(Inputs() with { Opacity = 0.30, DimTo = 0.80 })
            .TargetOpacity.ShouldBe(0.30);
    }

    [Theory]
    [MemberData(nameof(Exceptions))]
    [Trait("Req", "GEN-009")]
    [Trait("Req", "SEG-002")]
    public void No_surface_dims_while_an_exception_is_active(DimExceptions exception)
    {
        foreach (var surface in new[] { DimSurface.Panel, DimSurface.Dock, DimSurface.DockHandle })
        {
            var decision = DimPolicy.Evaluate(Inputs(surface: surface, active: exception));
            decision.Dimmed.ShouldBeFalse($"{surface} with {exception}");
            decision.TargetOpacity.ShouldBe(Opacity, $"{surface} with {exception}");
            decision.NextEvaluationAt.ShouldBeNull($"{surface} with {exception}");
        }

        var bubble = DimPolicy.Evaluate(Inputs(surface: DimSurface.Bubble, active: exception));
        bubble.Dimmed.ShouldBeFalse();
        bubble.TargetOpacity.ShouldBe(exception == DimExceptions.Panic ? 1 : Opacity);
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    public void Several_exceptions_together_keep_every_surface_awake()
    {
        var all = DimExceptions.None;
        foreach (var exception in Exceptions)
        {
            all |= exception;
        }

        DimPolicy.Evaluate(Inputs(active: all & ~DimExceptions.Panic)).Dimmed.ShouldBeFalse();
        DimPolicy.Evaluate(Inputs(active: all)).Dimmed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(DimSurface.Bubble)]
    [InlineData(DimSurface.DockHandle)]
    [Trait("Req", "BUR-002")]
    [Trait("Req", "PES-004")]
    public void The_bubble_and_the_handle_never_go_below_55_percent(DimSurface surface)
    {
        var low = Inputs(surface: surface) with { Opacity = 0.30, DimTo = 0.10 };

        DimPolicy.Evaluate(low).TargetOpacity.ShouldBe(0.55, "Dimmed.");
        DimPolicy
            .Evaluate(low with { PointerInside = true })
            .TargetOpacity.ShouldBe(0.55, "Awake at 30 %.");
        DimPolicy
            .Evaluate(Inputs(surface: surface) with { Opacity = 0.80 })
            .TargetOpacity.ShouldBe(0.55, "Dimmed to 35 %.");
        DimPolicy
            .Evaluate(Inputs(surface: surface, pointerInside: true))
            .TargetOpacity.ShouldBe(Opacity, "Above the minimum, the opacity of the person.");
    }

    [Fact]
    [Trait("Req", "BUR-002")]
    public void With_panic_the_bubble_is_at_full_opacity()
    {
        DimPolicy
            .Evaluate(
                Inputs(surface: DimSurface.Bubble, active: DimExceptions.Panic) with
                {
                    Opacity = 0.30,
                }
            )
            .TargetOpacity.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    [Trait("Req", "PAN-003")]
    [Trait("Req", "TEM-004")]
    public void A_contrast_theme_has_no_translucency(DimSurface surface)
    {
        var decision = DimPolicy.Evaluate(
            Inputs(surface: surface) with
            {
                HighContrast = true,
                Opacity = 0.30,
            }
        );

        decision.TargetOpacity.ShouldBe(1);
        decision.Dimmed.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    [Trait("Req", "TEM-006")]
    public void The_change_lasts_350_ms_and_nothing_with_reduce_motion()
    {
        DimPolicy.Evaluate(Inputs()).Transition.ShouldBe(TimeSpan.FromMilliseconds(350));
        DimPolicy
            .Evaluate(Inputs() with { ReduceMotion = true })
            .Transition.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    [Trait("Req", "EJE-017")]
    public void Dimming_never_reaches_the_activation_decision()
    {
        // The first touch on a dimmed panel wakes it AND acts: nothing about dimming is an input of ActivationPolicy.
        var dimmingTypes = typeof(DimPolicy)
            .Assembly.GetTypes()
            .Where(type =>
                string.Equals(type.Namespace, typeof(DimPolicy).Namespace, StringComparison.Ordinal)
            )
            .ToHashSet();

        typeof(ActivationContext)
            .GetProperties()
            .Select(property =>
                Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType
            )
            .ShouldAllBe(type => !dimmingTypes.Contains(type));
    }

    private static DimInputs Inputs(
        DimSurface surface = DimSurface.Panel,
        DimExceptions active = DimExceptions.None,
        bool pointerInside = false,
        bool autoDim = true,
        DateTimeOffset? now = null
    ) =>
        new(
            autoDim,
            Opacity,
            DimTo,
            surface,
            pointerInside,
            Left,
            active,
            ReduceMotion: false,
            HighContrast: false,
            now ?? Left + TimeSpan.FromSeconds(10)
        );
}
