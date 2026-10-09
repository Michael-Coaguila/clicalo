using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The «Qué hace» grid of 4 x 2 (EDI-006).</summary>
/// <param name="Title">[type].</param>
/// <param name="Options">The eight kinds.</param>
/// <param name="Description">The [d*] of the kind.</param>
public sealed record KindsModel(string Title, ValueList<KindOption> Options, string Description);
