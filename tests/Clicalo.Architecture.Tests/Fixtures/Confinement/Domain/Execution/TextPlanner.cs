using Clicalo.Architecture.Tests.Fixtures.Confinement.Domain.Library;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Domain.Execution;

/// <summary>Fixture: the planner, allowed to reveal secrets.</summary>
public static class TextPlanner
{
    public static int Plan(SecretText text)
    {
        var count = 0;
        text.WithRevealed(0, (span, _) => count = span.Length);
        return count;
    }
}
