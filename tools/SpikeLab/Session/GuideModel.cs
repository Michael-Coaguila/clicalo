using System.Collections.Immutable;

namespace Clicalo.Tools.SpikeLab.Session;

/// <summary>What the guide strip shows, already worded (<see cref="GuideModelBuilder"/>).</summary>
/// <param name="Heading">«S1 · paso 7 de 32 (fila 7)».</param>
/// <param name="Title">The step title.</param>
/// <param name="Instruction">What to do, in large type.</param>
/// <param name="Progress">«Repeticiones: 7 de 20 · fallos: 0 · comprobación final: pendiente».</param>
/// <param name="IsGreen">Green: nothing went wrong in this step so far; red otherwise.</param>
/// <param name="StatusWord">The state in words, so it never depends on color alone.</param>
/// <param name="Measurements">The automatic measurements, one line each.</param>
/// <param name="Notice">The last notice for the maintainer, or null.</param>
/// <param name="StepActionName">The name of the step action button, or null to hide it.</param>
internal sealed record GuideModel(
    string Heading,
    string Title,
    string Instruction,
    string Progress,
    bool IsGreen,
    string StatusWord,
    ImmutableArray<string> Measurements,
    string? Notice,
    string? StepActionName
);
