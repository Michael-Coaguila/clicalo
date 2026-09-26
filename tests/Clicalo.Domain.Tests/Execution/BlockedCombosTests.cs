using System.Text.Json;
using Clicalo.Domain.Execution.Internal;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Tests.Execution.Support;
using Clicalo.TestKit;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>EJE-014: the engine blocks exactly the «blocked» combinations of blocked-combos.json, whatever the order or side.</summary>
[Trait("Req", "EJE-014")]
public sealed class BlockedCombosTests
{
    [Fact]
    public void The_engine_list_is_the_blocked_entries_of_the_catalog()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "blocked-combos.json"))
        );
        var blocked = document
            .RootElement.GetProperty("combos")
            .EnumerateArray()
            .Where(static c =>
                string.Equals(
                    c.GetProperty("level").GetString(),
                    "blocked",
                    StringComparison.Ordinal
                )
            )
            .Select(static c =>
                string.Join(
                    '+',
                    c.GetProperty("keys")
                        .EnumerateArray()
                        .Select(static k => k.GetString())
                        .Order(StringComparer.Ordinal)
                )
            )
            .Order(StringComparer.Ordinal)
            .ToList();

        BlockedCombos
            .All.Select(static set => string.Join('+', set.Order(StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ShouldBe(blocked);
    }

    [Theory]
    [InlineData("ctrl", "alt", "delete")]
    [InlineData("delete", "alt", "ctrl")]
    [InlineData("rctrl", "alt", "delete")]
    [InlineData("win", "l")]
    [InlineData("rwin", "l")]
    public void A_blocked_combination_is_recognized_in_any_order_and_side(params string[] keys) =>
        BlockedCombos.IsBlocked(Chords.Of(keys)).ShouldBeTrue();

    [Theory]
    [InlineData("ctrl", "alt")]
    [InlineData("ctrl", "alt", "delete", "shift")]
    [InlineData("win", "g")]
    [InlineData("alt", "tab")]
    public void Special_and_partial_combinations_are_not_blocked(params string[] keys) =>
        BlockedCombos.IsBlocked(Chords.Of(keys)).ShouldBeFalse();

    [Fact]
    public void A_sided_stroke_counts_as_its_base_key() =>
        BlockedCombos
            .IsBlocked(
                Chords.Of(
                    new KeyStroke(KeyIds.Ctrl, KeySide.Right),
                    new KeyStroke(KeyIds.Alt),
                    new KeyStroke(KeyIds.Delete)
                )
            )
            .ShouldBeTrue();
}
