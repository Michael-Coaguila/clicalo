using System.Text;
using Clicalo.TestKit.Snapshots;

namespace Clicalo.Platform.IntegrationTests.TestKit;

/// <summary>Golden-file text snapshots (Clicalo.TestKit): naming, normalization, failure artifacts and acceptance.</summary>
public sealed class TextSnapshotTests
{
    [Fact]
    public void A_verified_file_next_to_the_test_is_matched_byte_for_byte()
    {
        // Snapshots/TextSnapshotTests.A_verified_file_next_to_the_test_is_matched_byte_for_byte.unicode.verified.txt
        TextSnapshot.Match("Clícalo · ñ á €\r\nemoji 😀\r\n", "unicode");
    }

    [Fact]
    public void The_location_follows_the_test_file_method_and_name()
    {
        var location = SnapshotLocation.ForTest("menu");

        location.BaseName.ShouldBe(
            "TextSnapshotTests.The_location_follows_the_test_file_method_and_name.menu"
        );
        location.Directory.ShouldEndWith(Path.Combine("TestKit", SnapshotLocation.FolderName));
        location.VerifiedPath("txt").ShouldEndWith(".menu.verified.txt");
        location.ReceivedPath("diff", "png").ShouldEndWith(".menu.received.diff.png");
    }

    [Fact]
    public void A_missing_verified_file_fails_and_writes_the_received_text()
    {
        using var folder = new TemporaryDirectory();
        var location = new SnapshotLocation(folder.Path, "Sample.Test.first");

        var failure = Should.Throw<SnapshotMismatchException>(() =>
            TextSnapshot.Match("line 1\r\nline 2", location, SnapshotMode.Verify)
        );

        failure.Message.ShouldContain("has no verified file yet");
        failure.Message.ShouldContain(SnapshotSettings.AcceptVariable);
        File.ReadAllText(location.ReceivedPath("txt")).ShouldBe("line 1\nline 2\n");
        File.Exists(location.VerifiedPath("txt")).ShouldBeFalse();
    }

    [Fact]
    public void A_difference_fails_with_the_first_differing_line_and_visible_invisible_characters()
    {
        using var folder = new TemporaryDirectory();
        var location = new SnapshotLocation(folder.Path, "Sample.Test.diff");
        File.WriteAllText(location.VerifiedPath("txt"), "same\nprice 5 €\nend\n");

        var failure = Should.Throw<SnapshotMismatchException>(() =>
            TextSnapshot.Match("same\nprice 5\u00A0€\nend\n", location, SnapshotMode.Verify)
        );

        failure.Message.ShouldContain("does not match");
        failure.Message.ShouldContain("First difference at line 2");
        failure.Message.ShouldContain("  - price 5 €");
        failure.Message.ShouldContain("  + price 5\\u00A0€");
        File.Exists(location.ReceivedPath("txt")).ShouldBeTrue();
    }

    [Fact]
    public void Accepting_writes_the_verified_file_and_removes_stale_received_files()
    {
        using var folder = new TemporaryDirectory();
        var location = new SnapshotLocation(folder.Path, "Sample.Test.accept");
        Should.Throw<SnapshotMismatchException>(() =>
            TextSnapshot.Match("v1", location, SnapshotMode.Verify)
        );

        TextSnapshot.Match("v1", location, SnapshotMode.Accept);

        var bytes = File.ReadAllBytes(location.VerifiedPath("txt"));
        bytes.ShouldBe(Encoding.UTF8.GetBytes("v1\n"), "UTF-8 without BOM, LF, one final newline.");
        File.Exists(location.ReceivedPath("txt")).ShouldBeFalse();
        TextSnapshot.Match("v1\r\n", location, SnapshotMode.Verify);
    }

    [Fact]
    public void A_match_removes_the_received_file_of_an_earlier_failure()
    {
        using var folder = new TemporaryDirectory();
        var location = new SnapshotLocation(folder.Path, "Sample.Test.stale");
        File.WriteAllText(location.VerifiedPath("txt"), "ok\n");
        File.WriteAllText(location.ReceivedPath("txt"), "old\n");

        TextSnapshot.Match("ok", location, SnapshotMode.Verify);

        File.Exists(location.ReceivedPath("txt")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("a\r\nb", "a\nb\n")]
    [InlineData("a\rb\n\n\n", "a\nb\n")]
    [InlineData("", "")]
    [InlineData("\n", "")]
    [InlineData("a\u2028b\u0085c\fd", "a\u2028b\u0085c\fd\n")]
    public void Text_is_normalized_to_LF_with_one_final_newline(string input, string expected) =>
        TextSnapshot.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    public void Snapshot_names_cannot_leave_their_folder(string name) =>
        Should.Throw<ArgumentException>(() => new SnapshotLocation(Path.GetTempPath(), name));
}
