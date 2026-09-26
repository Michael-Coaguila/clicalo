using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Clicalo.DevCli.AnonymizeV1;
using Clicalo.TestKit;

namespace Clicalo.DevCli.Tests.AnonymizeV1;

/// <summary>The <c>anonymize-v1</c> verb: files, zips, the console and the committed fixtures.</summary>
[Trait("Req", "MIG-004")]
public sealed class AnonymizeV1CommandTests
{
    [Fact]
    public void The_verb_writes_the_fixture_and_prints_only_counts()
    {
        using var folder = new TemporaryRepository();
        var input = folder.Write("real/profiles.json", V1AnonymizerTests.Personal);
        var output = Path.Combine(folder.Root, "fixtures", "profiles.json");
        using var console = new StringWriter();

        var code = AnonymizeV1Command.Run(input, output, console, V1AnonymizerTests.Names);

        code.ShouldBe(ExitCodes.Success);
        File.Exists(output).ShouldBeTrue();
        File.ReadAllText(input).ShouldBe(V1AnonymizerTests.Personal);
        console.ToString().ShouldContain("3 profiles, 10 buttons");
        console.ToString().ShouldNotContain("Juan");
        console.ToString().ShouldNotContain(folder.Root);
    }

    [Fact]
    public void The_input_is_never_the_output()
    {
        using var folder = new TemporaryRepository();
        var input = folder.Write("profiles.json", V1AnonymizerTests.Personal);
        using var console = new StringWriter();

        AnonymizeV1Command
            .Run(input, input, console, V1AnonymizerTests.Names)
            .ShouldBe(ExitCodes.Usage);
        File.ReadAllText(input).ShouldBe(V1AnonymizerTests.Personal);
    }

    [Fact]
    public void A_file_that_is_not_v1_writes_nothing()
    {
        using var folder = new TemporaryRepository();
        var input = folder.Write("profiles.json", "not json");
        var output = Path.Combine(folder.Root, "out.json");
        using var console = new StringWriter();

        AnonymizeV1Command
            .Run(input, output, console, V1AnonymizerTests.Names)
            .ShouldBe(ExitCodes.Failure);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public void A_zip_backup_is_anonymized_entry_by_entry()
    {
        using var folder = new TemporaryRepository();
        var input = Path.Combine(folder.Root, "profiles.backup.zip");
        File.WriteAllBytes(
            input,
            Zip(
                ("profiles.json", V1AnonymizerTests.Personal),
                ("notes.txt", "Juan"),
                ("juan-profiles.json", V1AnonymizerTests.Personal)
            )
        );
        var output = Path.Combine(folder.Root, "out.zip");
        using var console = new StringWriter();

        AnonymizeV1Command
            .Run(input, output, console, V1AnonymizerTests.Names)
            .ShouldBe(ExitCodes.Success);

        using var archive = ZipFile.OpenRead(output);
        archive.Entries.Select(static e => e.FullName).ShouldBe(["profiles.json", "entry-2.json"]);
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            reader.ReadToEnd().ShouldNotContain("Juan");
        }

        console.ToString().ShouldContain("1 other entries dropped");
    }

    [Theory]
    [InlineData("anonymize-v1")]
    [InlineData("anonymize-v1", "--in", "a.json")]
    [InlineData("anonymize-v1", "--out", "b.json")]
    public void The_verb_needs_both_files(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        Cli.Run(args, RepoPaths.Root, output, error).ShouldBe(ExitCodes.Usage);
        error.ToString().ShouldContain("anonymize-v1 needs --in <file> and --out <file>.");
    }

    [Fact]
    public void The_help_lists_the_verb()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        Cli.Run(["help"], RepoPaths.Root, output, error);

        output.ToString().ShouldContain("anonymize-v1");
    }

    [Fact]
    public void The_embedded_list_of_public_names_loads()
    {
        PublicNames.Embedded.Names.Contains("General").ShouldBeTrue();
        PublicNames.Embedded.IsPublicProgram("chrome.exe").ShouldBeTrue();
        PublicNames.Embedded.IsPublicProgram("CMD").ShouldBeTrue();
        PublicNames.Embedded.IsPublicProgram("juanapp.exe").ShouldBeFalse();
    }

    /// <summary>
    /// Privacy guard: every profile and button name in the committed v1 fixtures is a reviewed public name, so a
    /// fixture written without <c>anonymize-v1</c> (or with a private name added to it) fails here.
    /// </summary>
    [Fact]
    public void The_committed_fixtures_only_carry_public_names()
    {
        var folder = RepoPaths.Combine("tests", "Clicalo.Infrastructure.Tests", "Fixtures", "v1");
        var documents = Directory
            .EnumerateFiles(folder, "*.json")
            .Select(File.ReadAllBytes)
            .Concat(ZipEntries(Path.Combine(folder, "profiles.backup.zip")))
            .ToList();
        documents.Count.ShouldBe(8);

        foreach (var bytes in documents)
        {
            using var json = JsonDocument.Parse(bytes);
            foreach (var profile in json.RootElement.GetProperty("profiles").EnumerateObject())
            {
                PublicNames.Embedded.Names.Contains(profile.Name).ShouldBeTrue(profile.Name);
                var process = profile.Value.GetProperty("process").GetString()!;
                (process.Length == 0 || PublicNames.Embedded.IsPublicProgram(process)).ShouldBeTrue(
                    profile.Name
                );
                foreach (var button in profile.Value.GetProperty("buttons").EnumerateArray())
                {
                    var label = button.GetProperty("label").GetString()!;
                    PublicNames.Embedded.Names.Contains(label).ShouldBeTrue(label);
                }
            }
        }
    }

    private static IEnumerable<byte[]> ZipEntries(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            yield return copy.ToArray();
        }
    }

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return buffer.ToArray();
    }
}
