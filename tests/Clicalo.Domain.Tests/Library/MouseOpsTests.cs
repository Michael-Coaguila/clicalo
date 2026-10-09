using Clicalo.Domain.Library;

namespace Clicalo.Domain.Tests.Library;

/// <summary><see cref="MouseOps"/>: which mouse actions repeat while the finger rests on their tile (EJE-009).</summary>
public sealed class MouseOpsTests
{
    [Theory]
    [InlineData(MouseOp.ScrollUp, true)]
    [InlineData(MouseOp.ScrollDown, true)]
    [InlineData(MouseOp.ScrollLeft, true)]
    [InlineData(MouseOp.ScrollRight, true)]
    [InlineData(MouseOp.RightClick, false)]
    [InlineData(MouseOp.DoubleClick, false)]
    [Trait("Req", "EJE-009")]
    public void Only_the_scrolls_repeat_while_held(MouseOp op, bool repeats) =>
        MouseOps.RepeatsWhileHeld(op).ShouldBe(repeats);
}
