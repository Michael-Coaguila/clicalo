using Clicalo.Infrastructure.Persistence;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// NFR-010 (proposal P6, ADR-0029) and REG-08: uninstalling keeps the data unless the person asked to delete it. The
/// uninstaller's hook only deletes the data folders when it finds the marker, and never anything else. Everything
/// runs in a temporary folder.
/// </summary>
[Trait("Req", "NFR-010")]
[Trait("Req", "REG-08")]
public sealed class UninstallDataWipeTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "clicalo-wipe-tests",
        Guid.NewGuid().ToString("N")
    );

    private readonly DataLocations _locations;

    public UninstallDataWipeTests()
    {
        _locations = new DataLocations(Path.Combine(_root, "roaming", "Clicalo"))
        {
            LocalRoot = Path.Combine(_root, "local", "Clicalo"),
        };
        Directory.CreateDirectory(Path.Combine(_locations.Backups, "manual"));
        Directory.CreateDirectory(_locations.Logs);
        Directory.CreateDirectory(_locations.Pending);
        File.WriteAllText(_locations.Document, "{}");
        File.WriteAllText(Path.Combine(_locations.Backups, "manual", "copia.json"), "{}");
        File.WriteAllText(_locations.LogFile, "log");
        File.WriteAllText(_locations.PendingDocument, "{}");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // The temporary folder is cleaned by the system.
        }
    }

    [Fact]
    public void Without_the_request_uninstalling_touches_nothing()
    {
        UninstallDataWipe.IsRequested(_locations).ShouldBeFalse();

        UninstallDataWipe.RunIfRequested(_locations).ShouldBeFalse();

        File.Exists(_locations.Document).ShouldBeTrue();
        File.Exists(_locations.LogFile).ShouldBeTrue();
        File.Exists(_locations.PendingDocument).ShouldBeTrue();
        Directory.GetFiles(_locations.Backups, "*", SearchOption.AllDirectories).Length.ShouldBe(1);
    }

    [Fact]
    public void With_the_request_the_data_folders_are_deleted_and_nothing_else()
    {
        var neighbour = Path.Combine(_root, "roaming", "Otra app", "datos.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(neighbour)!);
        File.WriteAllText(neighbour, "ajeno");
        Request();

        UninstallDataWipe.RunIfRequested(_locations).ShouldBeTrue();

        Directory.Exists(_locations.Root).ShouldBeFalse();
        Directory.Exists(_locations.LocalRoot!).ShouldBeFalse();
        File.ReadAllText(neighbour).ShouldBe("ajeno");
        UninstallDataWipe.IsRequested(_locations).ShouldBeFalse();
    }

    [Fact]
    public void A_request_taken_back_keeps_the_data_again()
    {
        Request();
        UninstallDataWipe.IsRequested(_locations).ShouldBeTrue();

        UninstallDataWipe.Withdraw(_locations);
        UninstallDataWipe.Withdraw(_locations);

        UninstallDataWipe.RunIfRequested(_locations).ShouldBeFalse();
        File.Exists(_locations.Document).ShouldBeTrue();
    }

    [Fact]
    public void A_data_folder_that_is_a_link_to_another_folder_is_never_followed()
    {
        var elsewhere = Path.Combine(_root, "elsewhere", "deep");
        Directory.CreateDirectory(elsewhere);
        File.WriteAllText(Path.Combine(elsewhere, "ajeno.txt"), "ajeno");
        var linked = new DataLocations(Path.Combine(_root, "linked", "Clicalo"))
        {
            LocalRoot = _locations.LocalRoot,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(linked.Root)!);
        try
        {
            _ = Directory.CreateSymbolicLink(linked.Root, elsewhere);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Creating links needs a privilege this user may not have: nothing to check then.
            return;
        }

        Request();

        UninstallDataWipe.RunIfRequested(linked).ShouldBeTrue();

        File.Exists(Path.Combine(elsewhere, "ajeno.txt")).ShouldBeTrue();
    }

    [Fact]
    public void A_copy_inside_the_data_folders_would_be_deleted_with_them()
    {
        UninstallDataWipe
            .WouldDelete(_locations, Path.Combine(_locations.Backups, "manual", "copia.json"))
            .ShouldBeTrue();
        UninstallDataWipe
            .WouldDelete(_locations, Path.Combine(_locations.Root.ToUpperInvariant(), "c.json"))
            .ShouldBeTrue("Windows paths do not tell case apart");
        UninstallDataWipe
            .WouldDelete(_locations, Path.Combine(_locations.LocalRoot!, "copia.json"))
            .ShouldBeTrue();
        UninstallDataWipe
            .WouldDelete(_locations, Path.Combine(_locations.Root, "..", "Clicalo", "c.json"))
            .ShouldBeTrue("the path is resolved first");

        UninstallDataWipe
            .WouldDelete(_locations, Path.Combine(_root, "roaming", "copia.json"))
            .ShouldBeFalse();
        UninstallDataWipe
            .WouldDelete(_locations, Path.Combine(_root, "roaming", "Clicalo-copias", "c.json"))
            .ShouldBeFalse("a sibling folder whose name only starts the same is another folder");
    }

    [Fact]
    public void The_marker_lives_in_the_local_folder_with_a_fixed_name()
    {
        UninstallDataWipe
            .MarkerPath(_locations)
            .ShouldBe(Path.Combine(_locations.LocalRoot!, "delete-data-on-uninstall"));
        UninstallDataWipe
            .MarkerPath(new DataLocations(_locations.Root))
            .ShouldBe(Path.Combine(_locations.Root, "delete-data-on-uninstall"));
        UninstallDataWipe.Marker.Length.ShouldBeGreaterThan(0);
    }

    private void Request() =>
        File.WriteAllBytes(
            UninstallDataWipe.MarkerPath(_locations),
            UninstallDataWipe.Marker.ToArray()
        );
}
