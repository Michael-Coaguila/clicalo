using Clicalo.DevCli.I18n;

namespace Clicalo.DevCli.Tests.I18n;

/// <summary>Which keys the product uses: C# members of L, the XAML markup extension and data «…Key» properties.</summary>
public sealed class KeyUsageScannerTests
{
    private static readonly Dictionary<string, string> Members = new(StringComparer.Ordinal)
    {
        ["ImpT"] = "impT",
        ["ComboN"] = "comboN",
        ["RSingle"] = "rSingle",
        ["Search"] = "search",
    };

    [Fact]
    public void Finds_CSharp_XAML_and_data_usages_with_their_position()
    {
        using var repo = new TemporaryRepository();
        repo.Write("src/Clicalo.Presentation/Vm.cs", "class Vm\n{\n    object M => L.ImpT;\n}\n");
        repo.Write(
            "src/Clicalo.UI.Wpf/View.xaml",
            "<Grid>\n  <TextBlock Text=\"{loc:T comboN}\" />\n</Grid>\n"
        );
        repo.Write("data/catalogs/sample.json", "{\n  \"labelKey\": \"search\"\n}\n");

        var usages = KeyUsageScanner.Scan(repo.Root, Members);

        usages.Keys.Order(StringComparer.Ordinal).ShouldBe(["comboN", "impT", "search"]);
        usages["impT"].ShouldBe(new KeyUsage("impT", usages["impT"].Path, 3, 17));
        usages["comboN"].Line.ShouldBe(2);
        usages["comboN"].Column.ShouldBe(20);
        usages["search"].Line.ShouldBe(2);
        usages["search"].Column.ShouldBe(15);
    }

    [Fact]
    public void A_data_file_saved_with_a_byte_order_mark_is_still_scanned()
    {
        using var repo = new TemporaryRepository();
        repo.Write("data/tmpx.json", "{ \"labelKey\": \"rSingle\" }\n", bom: true);

        var usages = KeyUsageScanner.Scan(repo.Root, Members);

        usages["rSingle"].Line.ShouldBe(1);
        usages["rSingle"].Column.ShouldBe(15);
    }

    [Fact]
    public void Only_properties_named_Key_count_and_i18n_data_bin_and_obj_are_skipped()
    {
        using var repo = new TemporaryRepository();
        repo.Write("data/catalogs/sample.json", "{ \"label\": \"search\", \"id\": \"impT\" }");
        repo.Write("data/i18n/strings.es.json", "{ \"searchKey\": \"search\" }");
        repo.Write("src/Clicalo.App/obj/Generated.cs", "var x = L.Search;");
        repo.Write("src/Clicalo.App/bin/Copy.xaml", "{loc:T search}");

        KeyUsageScanner.Scan(repo.Root, Members).ShouldBeEmpty();
    }

    [Fact]
    public void Malformed_data_is_left_to_its_own_validation()
    {
        using var repo = new TemporaryRepository();
        repo.Write("data/broken.json", "{ \"labelKey\": ");

        KeyUsageScanner.Scan(repo.Root, Members).ShouldBeEmpty();
    }
}
