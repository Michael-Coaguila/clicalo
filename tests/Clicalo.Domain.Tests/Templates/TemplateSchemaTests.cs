using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Domain.Tests.Templates;

/// <summary>
/// The semantic check of an AI answer (blueprint §9.2, PLA-008, LOG-006): only taps of catalog keys with safe names
/// reach the preview; the process of an unknown program is never guessed (PLA-007).
/// </summary>
[Trait("Req", "PLA-008")]
[Trait("Req", "LOG-006")]
public sealed class TemplateSchemaTests
{
    private static readonly Func<KeyChord, bool> NeverBlocked = static _ => false;
    private static readonly Func<string, bool> AnyIcon = static _ => true;

    private static AiProposedShortcut Item(string name, params string[] keys) =>
        new(name, name + " (en)", "add", [.. keys], "file", 0.9);

    private static AiTemplateProposal Proposal(bool known, params AiProposedShortcut[] items) =>
        new(known, "WhatsApp", "WhatsApp.exe", "chat", [.. items]);

    [Fact]
    public void A_valid_proposal_becomes_a_template_of_taps_with_both_names()
    {
        var result = TemplateSchema
            .Validate(
                Proposal(true, Item("Nuevo chat", "ctrl", "n")),
                "whatsapp",
                NeverBlocked,
                AnyIcon
            )
            .ShouldNotBeNull();

        result.Known.ShouldBeTrue();
        result.Template.Processes.ShouldBe([new ProcessName("WhatsApp.exe")]);
        result.Template.Name.Get(LangCode.Es, LangCode.Es).ShouldBe("WhatsApp");
        var shortcut = result.Template.Shortcuts.ShouldHaveSingleItem();
        shortcut.Name.Get(LangCode.Es, LangCode.Es).ShouldBe("Nuevo chat");
        shortcut.Name.Get(LangCode.En, LangCode.Es).ShouldBe("Nuevo chat (en)");
        shortcut.Action.ShouldBe(new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N), []));
    }

    [Fact]
    [Trait("Req", "PLA-007")]
    public void An_unknown_program_gets_no_process()
    {
        var result = TemplateSchema
            .Validate(Proposal(false, Item("Guardar", "ctrl", "s")), "rara", NeverBlocked, AnyIcon)
            .ShouldNotBeNull();

        result.Known.ShouldBeFalse();
        result.Template.Processes.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "EJE-014")]
    public void Blocked_repeated_unknown_keys_and_unsafe_names_are_left_out()
    {
        var blocked = KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.Alt, KeyIds.Delete);
        var result = TemplateSchema
            .Validate(
                Proposal(
                    true,
                    Item("Bueno", "ctrl", "b"),
                    Item("Otra vez", "ctrl", "b"),
                    Item("Bloqueado", "ctrl", "alt", "delete"),
                    Item("Tecla rara", "ctrl", "hyper"),
                    Item("Solo modificador", "ctrl"),
                    Item("Nombre ‮trucado", "ctrl", "t"),
                    Item(new string('x', 40), "ctrl", "y")
                ),
                "app",
                chord => chord == blocked,
                AnyIcon
            )
            .ShouldNotBeNull();

        result.Template.Shortcuts.ShouldHaveSingleItem().ItemId.ShouldBe("ai1");
    }

    [Fact]
    public void Nothing_valid_left_or_too_many_shortcuts_is_invalid()
    {
        TemplateSchema
            .Validate(Proposal(true, Item("Mal", "nope")), "app", NeverBlocked, AnyIcon)
            .ShouldBeNull();
        TemplateSchema
            .Validate(
                Proposal(
                    true,
                    [.. Enumerable.Range(0, 25).Select(i => Item("A" + i, "ctrl", "a"))]
                ),
                "app",
                NeverBlocked,
                AnyIcon
            )
            .ShouldBeNull();
    }

    [Fact]
    public void Unknown_icons_and_categories_get_the_defaults_and_a_bad_process_is_dropped()
    {
        var proposal = new AiTemplateProposal(
            true,
            "App",
            "..\\evil.cmd",
            "no-such",
            [new AiProposedShortcut("Copiar", "Copy", "nope", ["ctrl", "c"], "weird", 1)]
        );

        var result = TemplateSchema
            .Validate(
                proposal,
                "App",
                NeverBlocked,
                icon => string.Equals(icon, "chat", StringComparison.Ordinal)
            )
            .ShouldNotBeNull();

        result.Template.Icon.Name.ShouldBe("apps");
        result.Template.Processes.ShouldBeEmpty();
        var shortcut = result.Template.Shortcuts.ShouldHaveSingleItem();
        shortcut.Icon.Name.ShouldBe("bolt");
        shortcut.Category.Value.ShouldBe("edit");
    }

    [Theory]
    [InlineData(true, "alt", "f4")]
    [InlineData(true, "lctrl", "w")]
    [InlineData(true, "delete")]
    [InlineData(false, "ctrl", "s")]
    public void Dangerous_combinations_are_recognised_whatever_the_side(
        bool dangerous,
        params string[] keys
    )
    {
        var chord = KeyChord.Create(keys.Select(k => new KeyStroke(new KeyId(k))));

        TemplateSchema.IsDangerous(chord).ShouldBe(dangerous);
    }

    [Theory]
    [InlineData("  Guardar  ", "Guardar")]
    [InlineData("", null)]
    [InlineData("a\u0007b", null)]
    [InlineData("a‏b", null)]
    public void Safe_names_are_trimmed_and_reject_control_and_bidi_characters(
        string text,
        string? expected
    ) => TemplateSchema.SafeName(text).ShouldBe(expected);
}
