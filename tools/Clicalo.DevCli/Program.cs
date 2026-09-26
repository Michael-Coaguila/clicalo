namespace Clicalo.DevCli;

/// <summary>Developer command line behind <c>cl</c>; verbs are added as each milestone needs them.</summary>
internal static class Program
{
    private static int Main(string[] args) =>
        Cli.Run(args, Directory.GetCurrentDirectory(), Console.Out, Console.Error);
}
