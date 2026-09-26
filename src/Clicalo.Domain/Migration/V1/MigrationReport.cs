using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>The visible report of a v1 import (MIG-004): the counts must match (210 → 210).</summary>
/// <param name="Input">Counts read.</param>
/// <param name="Output">Counts written.</param>
/// <param name="Notes">What has no direct equivalent.</param>
public sealed record MigrationReport(
    V1Counts Input,
    V1Counts Output,
    ValueList<MigrationNote> Notes
);
