using Clicalo.Domain.Migration.V1;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>What Macro Quick Access really sent (catalog §7.5): the shortcuts that never worked in v1 (MIG-007, PQ-40).</summary>
[Trait("Req", "MIG-007")]
public sealed class V1LegacyHotkeyTests
{
    [Theory]
    [InlineData("ctrl++")] // Firefox and Photoshop · Acercar: Ctrl alone.
    [InlineData("ctrl+num+")] // Excel · Inser. fila: nothing useful.
    [InlineData("ctrl+num-")] // Excel · Elim. fila.
    [InlineData("ctrl+k ctrl+c")] // VS Code and Antigravity · Comentar: Ctrl+C.
    [InlineData("ctrl+k ctrl+u")] // VS Code and Antigravity · Descomentar: Ctrl+U.
    [InlineData("ctrl+k z")] // Zen Mode of the English backup.
    [InlineData("+")]
    [InlineData("ctrl+ñ")]
    public void Combinations_v1_could_not_send(string hotkey) =>
        V1LegacyHotkey.NeverWorked(hotkey).ShouldBeTrue();

    [Theory]
    [InlineData("ctrl+c")]
    [InlineData("ctrl+plus")]
    [InlineData("ctrl+minus")]
    [InlineData("ctrl+-")]
    [InlineData("alt+=")]
    [InlineData("win+l")]
    [InlineData("ctrl+shift+esc")]
    [InlineData("altright+shiftright")]
    [InlineData(" ctrl + c ")]
    [InlineData("")]
    public void Combinations_v1_sent(string hotkey) =>
        V1LegacyHotkey.NeverWorked(hotkey).ShouldBeFalse();
}
