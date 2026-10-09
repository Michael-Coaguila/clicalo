using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Dimming;

/// <summary>
/// The opacity of the panel, the bar, its handle and the bubble, and their automatic dimming (GEN-009, docs/04
/// «Opacidad y atenuado», blueprint §6.4). Pure: the same inputs give the same decision.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>With «Dim when not in use» on, a surface dims <c>Timings.Dimming.DimDelay</c> (2.5 s) after the finger or
/// the pointer leaves it, and wakes as soon as one is on it again.</item>
/// <item>Dimmed, the opacity is min(dimTo, opacity) (GEN-009).</item>
/// <item>Nothing dims while any <see cref="DimExceptions"/> is active: panic, quick settings, context menu, profile
/// grid, search, edit mode, windows beside the bar, Control Center or welcome (SEG-002).</item>
/// <item>The bubble and the handle never go below <c>Timings.Dimming.BubbleMinOpacity</c> (55 %), dimmed or not
/// (BUR-002, PES-004); with panic the bubble is at 100 %.</item>
/// <item>A contrast theme has no translucency: everything is at 100 % (PAN-003, TEM-004).</item>
/// <item>The change lasts <c>Timings.Dimming.DimTransition</c> (350 ms), zero with reduce motion (TEM-006).</item>
/// </list>
/// Dimming is only visual: no input layer uses it, so the first touch on a dimmed surface wakes it and acts (EJE-017).
/// </remarks>
public static class DimPolicy
{
    /// <summary>Decides the opacity of <see cref="DimInputs.Surface"/>.</summary>
    /// <param name="inputs">The settings, the surface and what is open.</param>
    public static DimDecision Evaluate(in DimInputs inputs)
    {
        var transition = inputs.ReduceMotion ? TimeSpan.Zero : Timings.Dimming.DimTransition;
        if (inputs.HighContrast)
        {
            return new DimDecision(1, transition, Dimmed: false, NextEvaluationAt: null);
        }

        if (inputs.Surface == DimSurface.Bubble && inputs.Active.HasFlag(DimExceptions.Panic))
        {
            return new DimDecision(1, transition, Dimmed: false, NextEvaluationAt: null);
        }

        var mayDim =
            inputs.AutoDim
            && inputs.Active == DimExceptions.None
            && !inputs.PointerInside
            && inputs.LastLeave is not null;
        var dimAt = inputs.LastLeave + Timings.Dimming.DimDelay;
        var dimmed = mayDim && inputs.Now >= dimAt;
        var opacity = dimmed ? Math.Min(inputs.DimTo, inputs.Opacity) : inputs.Opacity;
        if (inputs.Surface is DimSurface.Bubble or DimSurface.DockHandle)
        {
            opacity = Math.Max(opacity, Timings.Dimming.BubbleMinOpacity);
        }

        return new DimDecision(
            Math.Clamp(opacity, 0, 1),
            transition,
            dimmed,
            mayDim && !dimmed ? dimAt : null
        );
    }
}
