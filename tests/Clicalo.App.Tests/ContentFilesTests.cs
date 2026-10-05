using System.IO;
using Clicalo.App.Composition;
using Clicalo.Infrastructure.Catalogs;
using Clicalo.TestKit;

namespace Clicalo.App.Tests;

/// <summary>
/// The <c>content</c> folder the build copies next to the executable (D17, user decision D2): the kit, «Basics» and
/// every template it offers, so a first start and the welcome find the same content as the repository.
/// </summary>
[Trait("Req", "CAT-003")]
[Trait("Req", "BIE-006")]
public sealed class ContentFilesTests
{
    [Fact]
    public void The_build_ships_the_kit_the_seed_and_every_template_next_to_the_executable()
    {
        var folder = ContentFiles.Find(AppContext.BaseDirectory).ShouldNotBeNull();

        var content = StarterContentFiles.Load(folder).ShouldNotBeNull();

        var templates = Directory
            .GetFiles(RepoPaths.Combine("data", "content", "templates"), "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);
        content
            .Templates.Select(t => t.Id)
            .Order(StringComparer.Ordinal)
            .ShouldBe(
                templates!,
                "a template missing from the build would vanish from the welcome"
            );
        content.Kit.Options.Count.ShouldBe(content.Templates.Count + 1);
    }

    [Fact]
    public void A_folder_without_the_kit_has_no_content()
    {
        ContentFiles.Find(Path.Combine(AppContext.BaseDirectory, "missing")).ShouldBeNull();
    }
}
