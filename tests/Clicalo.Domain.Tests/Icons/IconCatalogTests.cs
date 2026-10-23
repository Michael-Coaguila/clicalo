using Clicalo.Domain.Catalog;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Tests.Icons;

/// <summary>
/// The names of the icons of the picker (EDI-004, REG-06): what UI Automation and voice access call each cell, in the
/// language of the interface.
/// </summary>
public sealed class IconCatalogTests
{
    private static readonly IconCatalog Catalog = new(
        [
            new IconEntry(
                new IconRef("format_bold"),
                ["negrita", "bold", "weight"],
                "text",
                new LocalizedText([new(LangCode.Es, "negrita"), new(LangCode.En, "bold")])
            ),
            new IconEntry(
                new IconRef("content_paste"),
                ["pegar"],
                "text",
                new LocalizedText([new(LangCode.Es, "pegar")])
            ),
            new IconEntry(new IconRef("blur_on"), ["blur", "filter"], "images"),
        ],
        [],
        []
    );

    [Fact]
    [Trait("Req", "REG-06")]
    [Trait("Req", "EDI-004")]
    public void An_icon_is_named_with_its_first_hand_written_word_in_the_language_of_the_interface()
    {
        Catalog.NameOf("format_bold", LangCode.Es).ShouldBe("negrita");
        Catalog.NameOf("format_bold", LangCode.En).ShouldBe("bold");
        Catalog
            .NameOf("content_paste", LangCode.En)
            .ShouldBe("pegar", "a missing language falls back to Spanish");
        Catalog
            .NameOf("blur_on", LangCode.Es)
            .ShouldBe("blur on", "without hand-written words, its name with spaces");
    }
}
