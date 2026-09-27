using System.Globalization;
using Clicalo.Architecture.Tests.Support;
using Clicalo.TestKit;

namespace Clicalo.Architecture.Tests;

/// <summary>
/// Every text file of the repository is UTF-8 and has no double-encoded text (mojibake): a comment whose section sign
/// reads as two characters, or a test datum meant to be «¡Hola, ñandú!» that checks a corrupt string instead of the
/// Spanish characters it means (IDI-001). Windows PowerShell 5.1 produces it when it reads a UTF-8 file without a byte
/// order mark. The examples below are written with escapes, so this file does not contain what it rejects.
/// </summary>
public sealed class TextEncodingTests
{
    /// <summary>The text files the repository versions and people or tools edit.</summary>
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".props",
        ".targets",
        ".slnx",
        ".slnf",
        ".json",
        ".md",
        ".yml",
        ".yaml",
        ".xaml",
        ".xml",
        ".txt",
        ".ps1",
        ".cmd",
        ".editorconfig",
        ".gitattributes",
        ".gitignore",
    };

    /// <summary>Folders that are generated, restored or not versioned.</summary>
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "artifacts",
        "bin",
        "obj",
        "node_modules",
        "TestResults",
    };

    /// <summary>
    /// Files outside this front's scope that still carry mojibake and are repaired by their owner. The test below fails
    /// as soon as one is repaired, so the entry is removed in the same change; nothing may be added here.
    /// </summary>
    private static readonly string[] PendingRepair =
    [
        "src/Clicalo.Application/Store/DocumentStore.cs",
    ];

    [Fact]
    [Trait("Req", "IDI-001")]
    public void Every_text_file_is_UTF_8_without_double_encoded_text()
    {
        var problems = new List<string>();
        foreach (var path in TextFiles())
        {
            var relative = Path.GetRelativePath(RepoPaths.Root, path).Replace('\\', '/');
            if (PendingRepair.Contains(relative, StringComparer.Ordinal))
            {
                continue;
            }

            problems.AddRange(Problems(path, relative));
        }

        problems.ShouldBeEmpty(
            "Save these files as UTF-8 with the characters they meant (the fix is shown after the arrow)"
        );
    }

    [Fact]
    public void The_files_pending_repair_still_need_it()
    {
        foreach (var relative in PendingRepair)
        {
            Problems(RepoPaths.Combine(relative.Split('/')), relative)
                .ShouldNotBeEmpty(relative + " is repaired: remove it from PendingRepair");
        }
    }

    [Theory]
    [InlineData("blueprint \u00C2\u00A76.4", "\u00C2\u00A7", "§")]
    [InlineData(
        "SecretText.From(\"\u00C2\u00A1Hola, \u00C3\u00B1and\u00C3\u00BA!\")",
        "\u00C2\u00A1",
        "¡"
    )]
    [InlineData("after the \u00C2\u00ABrestart\u00C2\u00BB", "\u00C2\u00AB", "«")]
    [InlineData("touch \u00E2\u2020\u2019 SendInput", "\u00E2\u2020\u2019", "→")]
    [InlineData("p95 \u00E2\u2030\u00A4 50 ms", "\u00E2\u2030\u00A4", "≤")]
    [InlineData("« \u00C2\u00A7", "\u00C2\u00A7", "§")]
    [InlineData("\u00EF\u00BB\u00BFusing System;", "\u00EF\u00BB\u00BF", "\uFEFF")]
    public void Double_encoded_text_is_found(string line, string found, string meant) =>
        DoubleEncoding.Find(line).First().ShouldBe((found, meant));

    [Fact]
    public void Every_double_encoded_character_of_a_line_is_found() =>
        DoubleEncoding
            .Find("\u00C2\u00A1Hola, \u00C3\u00B1and\u00C3\u00BA!")
            .Select(static found => found.Meant)
            .ShouldBe(["¡", "ñ", "ú"]);

    [Theory]
    [InlineData("¡Hola, ñandú!")]
    [InlineData("«Soltar todo» · §6.4 → ≤ 50 ms")]
    [InlineData("¿Qué? Ñandú, pingüino, acción, Über, ½, °C, ©")]
    [InlineData("la tecla «Ñ» y «Ç»")]
    [InlineData("plain ASCII")]
    public void Legitimate_text_is_not_flagged(string line) =>
        DoubleEncoding.Find(line).ShouldBeEmpty();

    private static IEnumerable<string> Problems(string path, string relative)
    {
        var bytes = File.ReadAllBytes(path);
        if (!DoubleEncoding.IsValidUtf8(bytes))
        {
            yield return relative + ": not valid UTF-8";
            yield break;
        }

        var lines = File.ReadAllLines(path);
        for (var number = 0; number < lines.Length; number++)
        {
            foreach (var (found, meant) in DoubleEncoding.Find(lines[number]))
            {
                yield return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{relative}:{number + 1}: «{found}» → «{meant}»"
                );
            }
        }
    }

    private static IEnumerable<string> TextFiles()
    {
        var pending = new Stack<string>([RepoPaths.Root]);
        while (pending.Count > 0)
        {
            var folder = pending.Pop();
            foreach (var child in Directory.EnumerateDirectories(folder))
            {
                if (!SkippedFolders.Contains(Path.GetFileName(child)))
                {
                    pending.Push(child);
                }
            }

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var name = Path.GetFileName(file);
                if (
                    TextExtensions.Contains(Path.GetExtension(file))
                    || TextExtensions.Contains(name)
                )
                {
                    yield return file;
                }
            }
        }
    }
}
