using System.Globalization;

namespace Clicalo.DevCli.AnonymizeV1;

/// <summary>
/// <c>anonymize-v1 --in &lt;file&gt; --out &lt;file&gt;</c>: writes a fixture from a real v1 <c>profiles.json</c>, language
/// backup or <c>.zip</c> backup. The input is only read, never changed; the output never equals the input; the console
/// only shows counts, never a label, a name or a path of the input (M2-ownership, «Privacidad de los fixtures»).
/// </summary>
internal static class AnonymizeV1Command
{
    private static ReadOnlySpan<byte> ZipSignature => "PK"u8;

    /// <summary>Runs the verb.</summary>
    /// <param name="input">The real file.</param>
    /// <param name="outputPath">The fixture to write.</param>
    /// <param name="output">Console output.</param>
    /// <param name="publicNames">What may be kept; the embedded list when <see langword="null"/>.</param>
    public static int Run(
        string input,
        string outputPath,
        TextWriter output,
        PublicNames? publicNames = null
    )
    {
        var source = Path.GetFullPath(input);
        var target = Path.GetFullPath(outputPath);
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            output.WriteLine(
                "anonymize-v1: the output must be a different file; the input is never changed."
            );
            return ExitCodes.Usage;
        }

        if (!File.Exists(source))
        {
            output.WriteLine("anonymize-v1: the input file does not exist.");
            return ExitCodes.Usage;
        }

        var names = publicNames ?? PublicNames.Embedded;
        var bytes = File.ReadAllBytes(source);
        byte[] result;
        string summary;
        try
        {
            if (bytes.AsSpan().StartsWith(ZipSignature))
            {
                var (zip, entries, dropped) = V1ZipAnonymizer.Anonymize(bytes, names);
                result = zip;
                summary = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{entries.Count} JSON entries, {entries.Sum(static e => e.Buttons)} buttons, {entries.Sum(static e => e.ReplacedTexts)} texts replaced, {dropped} other entries dropped"
                );
            }
            else
            {
                var (json, stats) = new V1Anonymizer(names).Anonymize(bytes);
                result = json;
                summary = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{stats.Profiles} profiles, {stats.Buttons} buttons, {stats.KeptNames} public names kept, {stats.ReplacedTexts} texts replaced"
                );
            }
        }
        catch (InvalidDataException ex)
        {
            output.WriteLine("anonymize-v1: the input is not a v1 file (" + ex.Message + ")");
            return ExitCodes.Failure;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, result);
        output.WriteLine("anonymize-v1: " + summary + "; fixture written.");
        return ExitCodes.Success;
    }
}
