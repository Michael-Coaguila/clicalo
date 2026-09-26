using Clicalo.Application.Foreground;
using Clicalo.Domain.Touch;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>
/// One row of the manual results table of a spike script: what the maintainer does, how many repetitions it needs,
/// what counts one repetition and which automatic checks each repetition must pass.
/// </summary>
/// <param name="Id">The row number of the results table («1», «9a»…).</param>
/// <param name="Title">Short title, read aloud first (for example «Panel · dedo · Word»).</param>
/// <param name="Instruction">What to do, in large type on the guide strip.</param>
/// <param name="Required">Repetitions needed to pass (20, the criterion of blueprint §15.1, unless the row says otherwise).</param>
/// <param name="Trigger">What counts one repetition.</param>
/// <param name="Checks">Automatic checks of every repetition.</param>
internal sealed record ScriptStep(
    string Id,
    string Title,
    string Instruction,
    int Required,
    StepTrigger Trigger,
    EvidenceCheck Checks
)
{
    /// <summary>For <see cref="StepTrigger.SurfaceTap"/> and <see cref="StepTrigger.HandleDrag"/>: the surfaces that count.</summary>
    public SurfaceGroup Surface { get; init; } = SurfaceGroup.Any;

    /// <summary>The input device the row is about; taps with another device do not count. Null accepts any.</summary>
    public PointerKind? Pointer { get; init; }

    /// <summary>For <see cref="StepTrigger.UiaCommand"/>: the tile id that counts; null accepts any panel tile.</summary>
    public string? Tile { get; init; }

    /// <summary>For <see cref="StepTrigger.UiaCommand"/>: the pattern that counts; null accepts any.</summary>
    public CommandPattern? Pattern { get; init; }

    /// <summary>For <see cref="StepTrigger.LeaseCycle"/>: the lease kind that counts.</summary>
    public LeaseKind? Lease { get; init; }

    /// <summary>For <see cref="StepTrigger.LeaseCycle"/>: the origin that counts.</summary>
    public LeaseOrigin? Origin { get; init; }

    /// <summary>The row may be «no aplicable» (missing hardware or virtual machine) when skipped without repetitions.</summary>
    public bool Optional { get; init; }

    /// <summary>
    /// False when a failure of this row alone does not decide the spike (S1 row 32 passes to S3; S3 row 8 may be a
    /// Windows limitation). It is still reported.
    /// </summary>
    public bool Decisive { get; init; } = true;

    /// <summary>The extra button the guide strip shows while this step is current.</summary>
    public StepAction Action { get; init; }

    /// <summary>
    /// True for automatic steps: the laboratory counts the repetitions and the maintainer closes the cycle with
    /// «Funcionó» after the final check the laboratory cannot see (the dictated word reached the app).
    /// </summary>
    public bool NeedsConfirmation => Trigger != StepTrigger.Manual;
}
