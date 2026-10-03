using Clicalo.Domain.Templates;

namespace Clicalo.Infrastructure.Catalogs;

/// <summary>
/// The starter content of the <c>content</c> folder that ships next to the executable (D17): <c>starter.json</c>,
/// <c>seed.json</c> and <c>templates/&lt;id&gt;.json</c> for every template the kit offers. A template that cannot be
/// read is dropped from the kit, so the welcome never offers it; without a readable kit or seed there is no content.
/// </summary>
public static class StarterContentFiles
{
    /// <summary>The kit file.</summary>
    public const string KitFileName = "starter.json";

    /// <summary>The «Basics» file.</summary>
    public const string SeedFileName = "seed.json";

    /// <summary>The folder of the templates, inside the content folder.</summary>
    public const string TemplatesFolder = "templates";

    /// <summary>Whether <paramref name="folder"/> holds the kit and the seed.</summary>
    /// <param name="folder">A content folder.</param>
    public static bool Exists(string folder) =>
        File.Exists(Path.Combine(folder, KitFileName))
        && File.Exists(Path.Combine(folder, SeedFileName));

    /// <summary>
    /// The starter content of <paramref name="folder"/>, or <see langword="null"/> when its kit or its seed cannot be
    /// read or does not validate.
    /// </summary>
    /// <param name="folder">The content folder.</param>
    public static StarterContent? Load(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (
            ReadFile(Path.Combine(folder, KitFileName)) is not { } kitBytes
            || StarterContentReader.ReadKit(kitBytes) is not { } kit
            || ReadFile(Path.Combine(folder, SeedFileName)) is not { } seedBytes
            || StarterContentReader.ReadSeed(seedBytes) is not { } seed
        )
        {
            return null;
        }

        var options = new List<StarterOption>();
        var templates = new List<ProfileTemplate>();
        foreach (var option in kit.Options)
        {
            if (option.Kind == StarterOptionKind.Template)
            {
                if (
                    !IsFileName(option.Id)
                    || ReadFile(Path.Combine(folder, TemplatesFolder, option.Id + ".json"))
                        is not { } bytes
                    || StarterContentReader.ReadTemplate(bytes, option.Id) is not { } template
                )
                {
                    continue;
                }

                templates.Add(template);
            }

            options.Add(option);
        }

        return new StarterContent(kit with { Options = [.. options] }, seed, [.. templates]);
    }

    private static bool IsFileName(string id) =>
        id.Length > 0
        && id.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && id.AsSpan().IndexOfAny(['.', '/', '\\']) < 0;

    private static byte[]? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
