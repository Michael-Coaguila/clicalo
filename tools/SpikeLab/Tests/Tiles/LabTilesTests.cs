using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;

namespace Clicalo.Tools.SpikeLab.Tests.Tiles;

public sealed class LabTilesTests
{
    [Fact]
    public void The_panel_order_fixes_the_voice_numbers_the_scripts_use()
    {
        LabTiles.VoiceNumberOf(LabTiles.Underline).ShouldBe(3, "S1 row 32: «clic 3»");
        LabTiles.VoiceNumberOf(LabTiles.Copy).ShouldBe(4, "S3 row 1: «clic 4»");
        LabTiles.VoiceNumberOf(LabTiles.Save).ShouldBe(7, "S3 row 6: «clic 7»");
        LabTiles.VoiceNumberOf("guide-worked").ShouldBeNull();
    }

    [Fact]
    public void The_panel_has_the_tiles_of_S3_and_S4_with_their_patterns()
    {
        var byName = LabTiles.Panel.ToDictionary(
            tile => tile.Name,
            tile => tile.Pattern,
            StringComparer.Ordinal
        );

        foreach (
            var name in (string[])
                [
                    "Negrita",
                    "Cursiva",
                    "Subrayado",
                    "Copiar",
                    "Pegar",
                    "Deshacer",
                    "Guardar",
                    "Buscar",
                    "Soltar todo",
                    "Centro de control",
                ]
        )
        {
            byName[name].ShouldBe(CommandPattern.Invoke, name);
        }

        byName["Mayús"].ShouldBe(CommandPattern.Toggle);
        byName["Mantener Ctrl"].ShouldBe(CommandPattern.Toggle);
        byName["Perfil"].ShouldBe(CommandPattern.ExpandCollapse);
    }

    [Fact]
    public void Names_are_unique_across_surfaces_so_voice_never_needs_to_disambiguate()
    {
        var all = LabTiles
            .Panel.Concat(LabTiles.Dock)
            .Concat(LabTiles.Side)
            .Concat(LabTiles.Bubble)
            .Concat(LabTiles.Profiles)
            .Concat(LabTiles.SearchControls)
            .Concat(LabTiles.Guide)
            .Append(LabTiles.GuideStepAction)
            .ToArray();

        all.Select(tile => tile.Name).ShouldBeUnique(StringComparer.Ordinal);
        all.Select(tile => tile.Id).ShouldBeUnique(StringComparer.Ordinal);
        all.ShouldAllBe(tile => tile.Name.Any(char.IsLetter), "UIA008: no glyph is a name");
    }

    [Fact]
    public void Chord_tiles_have_a_chord_and_instruments_are_not_test_targets()
    {
        LabTiles
            .Panel.Where(tile => tile.Action == Clicalo.Tools.SpikeLab.Tiles.LabAction.SendChord)
            .ShouldAllBe(tile => tile.Chord != null);
        LabTiles.Guide.ShouldAllBe(tile => !tile.IsTestTarget);
        LabTiles.SearchControls.ShouldAllBe(tile => !tile.IsTestTarget);
        LabTiles.Panel.ShouldAllBe(tile => tile.IsTestTarget);
    }

    [Fact]
    public void A_chord_tile_describes_its_chord() =>
        LabTiles.Panel[0].Describe().ShouldBe("Negrita (Ctrl+B)");
}
