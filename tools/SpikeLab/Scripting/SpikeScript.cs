using System.Collections.Immutable;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>The manual part of one spike document, step by step (<see cref="SpikeScripts"/>).</summary>
/// <param name="Id">The spike.</param>
/// <param name="Title">Its title, as in its document.</param>
/// <param name="Document">The script it mirrors, relative to the repository root.</param>
/// <param name="Steps">The rows of its manual results table, in the recommended order.</param>
internal sealed record SpikeScript(
    SpikeId Id,
    string Title,
    string Document,
    ImmutableArray<ScriptStep> Steps
);
