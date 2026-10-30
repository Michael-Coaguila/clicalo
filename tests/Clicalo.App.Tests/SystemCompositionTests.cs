using System.IO;
using Clicalo.App.Composition;
using Clicalo.App.Lifecycle;
using Clicalo.Domain.Timing;

namespace Clicalo.App.Tests;

/// <summary>
/// The composition of «Sistema» that needs no desktop: the elevated instance reads which process it replaces
/// (<c>--handover</c>, user decision D7), and Importar refuses a file before decoding it when it is too large or
/// unreadable (COP-002, LOG-006).
/// </summary>
public sealed class SystemCompositionTests
{
    [Theory]
    [InlineData(new string[0], null)]
    [InlineData(new[] { "--handover=4242" }, 4242)]
    [InlineData(new[] { "--no-input", "--HANDOVER=17" }, 17)]
    [InlineData(new[] { "--handover=" }, null)]
    [InlineData(new[] { "--handover=-3" }, null)]
    [InlineData(new[] { "--handover=abc" }, null)]
    [InlineData(new[] { "--handover", "12" }, null)]
    [Trait("Req", "SIS-002")]
    public void The_handover_names_the_process_it_waits_for(string[] arguments, int? expected) =>
        ElevationHandover.ProcessId(arguments).ShouldBe(expected);

    [Fact]
    [Trait("Req", "COP-002")]
    [Trait("Req", "LOG-006")]
    public void Importing_refuses_a_file_that_is_too_large_or_not_a_backup()
    {
        var folder = Path.Combine(Path.GetTempPath(), "clicalo-import-" + Path.GetRandomFileName());
        Directory.CreateDirectory(folder);
        try
        {
            var large = Path.Combine(folder, "large.json");
            using (var stream = new FileStream(large, FileMode.CreateNew))
            {
                stream.SetLength(Timings.Import.ShareMaxBytes + 1);
            }

            var text = Path.Combine(folder, "notes.json");
            File.WriteAllText(text, "{\"hello\":1}");

            SystemBackups.Read(large).IsFailure.ShouldBeTrue();
            SystemBackups.Read(text).IsFailure.ShouldBeTrue();
            SystemBackups.Read(Path.Combine(folder, "missing.json")).IsFailure.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
