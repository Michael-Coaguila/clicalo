using System.IO;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>Finds the InputProbe executable.</summary>
public static class InputProbeLocator
{
    /// <summary>Overrides the location, for example to run the tests against a published probe.</summary>
    public const string PathVariable = "CLICALO_INPUTPROBE_PATH";

    /// <summary>File name of the probe's application host.</summary>
    public const string ExecutableName = "InputProbe.exe";

    /// <summary>
    /// <see cref="PathVariable"/> when set; otherwise <c>InputProbe\InputProbe.exe</c> in the test output, where
    /// every project that references <c>Clicalo.TestKit.Windows</c> receives a copy at build time.
    /// </summary>
    public static string Resolve()
    {
        var overridden = Environment.GetEnvironmentVariable(PathVariable);
        var path = string.IsNullOrWhiteSpace(overridden)
            ? Path.Combine(AppContext.BaseDirectory, "InputProbe", ExecutableName)
            : Path.GetFullPath(overridden);
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException(
                "InputProbe was not found. Build the test project (it copies tools/InputProbe into its output) or set "
                    + PathVariable
                    + ".",
                path
            );
    }
}
