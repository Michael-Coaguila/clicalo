namespace Clicalo.Build;

/// <summary>A <c>cl</c> verb that is part of the plan (blueprint §13) but lands in a later milestone.</summary>
/// <param name="Name">The dictable verb.</param>
/// <param name="Milestone">The milestone of blueprint §14 that delivers it (for example <c>M2</c>).</param>
internal sealed record FutureVerb(string Name, string Milestone);
