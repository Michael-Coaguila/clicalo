using Clicalo.Platform.Core.Guardian;
using Clicalo.TestKit;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// The command line Clícalo writes for Sentinel and for the main process Sentinel relaunches (ADR-0032): split by
/// Windows, it gives back exactly the arguments, a data folder with spaces, accents or a final backslash included, and
/// nothing in an argument can add another one.
/// </summary>
[Trait("Req", "SIS-004")]
[Trait("Req", "REG-08")]
public sealed class CommandLineArgumentsTests
{
    private const string Program =
        @"C:\Users\Ana\AppData\Local\Clicalo.App\current\Clicalo.Sentinel.exe";

    [Fact]
    public void Simple_tokens_are_written_as_they_are() =>
        CommandLineArguments
            .Join(["--protocol=4", "--parent=0x1A4", "--retry-ms=1000", "--crash-loop=3/600000"])
            .ShouldBe("--protocol=4 --parent=0x1A4 --retry-ms=1000 --crash-loop=3/600000");

    [Theory]
    [InlineData(@"C:\Users\Ana\Mis datos\Clícalo")]
    [InlineData(@"C:\Users\Ana\Mis datos\Clícalo\")]
    [InlineData(@"C:\Users\Ana\Mis datos\Clícalo\\")]
    [InlineData(@"D:\año 2026\pruebas  con dos espacios")]
    [InlineData(@"\\servidor\compartida\datos de José\")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\sin-espacios\ñandú\")]
    [InlineData("--data=C:\\Users\\Ana\\Mis datos\\Clícalo\\")]
    [InlineData("")]
    public void Windows_splits_it_back_into_the_same_arguments(string folder)
    {
        string[] arguments = ["--after-crash=1790000000000", "--data", folder, "--safe-mode"];

        var split = WindowsCommandLine.Split(
            '"' + Program + "\" " + CommandLineArguments.Join(arguments)
        );

        split.ShouldBe([Program, .. arguments]);
    }

    [Fact]
    public void A_folder_with_a_space_and_a_final_backslash_doubles_only_the_final_backslash() =>
        CommandLineArguments
            .Join(["--data", @"C:\Mis datos\"])
            .ShouldBe("--data \"C:\\Mis datos\\\\\"");

    [Theory]
    [InlineData("C:\\Mis datos\" --safe-mode")]
    [InlineData("C:\\a\tb")]
    [InlineData("C:\\a\nb")]
    [InlineData("C:\\a\0b")]
    public void An_argument_with_a_quote_or_a_control_character_is_refused(string argument)
    {
        CommandLineArguments.CanPass(argument).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => CommandLineArguments.Join(["--data", argument]));
    }

    [Fact]
    public void Starting_a_process_refuses_such_an_argument_before_anything_starts() =>
        Should.Throw<ArgumentException>(() =>
            GuardianProcess.Start(Program, ["--data", "C:\\a\" --safe-mode"], [])
        );
}
