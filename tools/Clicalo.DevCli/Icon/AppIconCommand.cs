using Clicalo.Design.Math;
using Clicalo.Generators.Tokens;

namespace Clicalo.DevCli.Icon;

/// <summary>
/// <c>app-icon</c>: draws the icon of Clícalo (BUR-003) from the logo the app already draws and the colors of
/// <c>data/tokens</c>, and writes it as two icon files: <see cref="IconPath"/>, the icon of the executable and of the
/// tray, and <see cref="DimIconPath"/>, the same at 55 % for the tray while the panel is hidden or Clícalo is paused.
/// With <c>--check</c> nothing is written and it fails when a file is not what it would draw. No external tool and no
/// download. Output ends with one line Narrator can read aloud.
/// </summary>
internal static class AppIconCommand
{
    /// <summary>The icon, relative to the repository root.</summary>
    public const string IconPath = "assets/icons/clicalo.ico";

    /// <summary>The icon of the tray while the panel is hidden or Clícalo is paused (BUR-003, BUR-004).</summary>
    public const string DimIconPath = "assets/icons/clicalo-dim.ico";

    /// <summary>BUR-003: the tray icon shows the hidden state at 55 %.</summary>
    public const double DimOpacity = 0.55;

    /// <summary>The theme whose accent the icon uses: the light one, white strokes on the dark accent.</summary>
    public const string ThemeKey = "light";

    private const string FillToken = "accent";
    private const string InkToken = "onAccent";

    /// <summary>The sizes Windows asks for, from the tray at 100 % to the largest view of the Explorer.</summary>
    public static IReadOnlyList<int> Sizes { get; } = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    /// <summary>The sizes of the tray, from 100 % to 400 %.</summary>
    public static IReadOnlyList<int> TraySizes { get; } = [16, 20, 24, 32, 40, 48, 64];

    public static int Run(string root, bool check, TextWriter output)
    {
        if (!TryReadColors(root, output, out var fill, out var ink))
        {
            output.WriteLine("app-icon: the colors of data/tokens could not be read.");
            return ExitCodes.Failure;
        }

        (string Path, IReadOnlyList<IcoImage> Images)[] files =
        [
            (IconPath, Render(Sizes, fill, ink, 1)),
            (DimIconPath, Render(TraySizes, fill, ink, DimOpacity)),
        ];
        var stale = files.Where(file => !IsCurrent(root, file.Path, file.Images)).ToList();
        if (check)
        {
            foreach (var (path, _) in stale)
            {
                output.WriteLine(
                    "error: "
                        + path
                        + " is not what app-icon draws; run "
                        + "`dotnet run --project tools/Clicalo.DevCli -- app-icon`."
                );
            }

            output.WriteLine(
                stale.Count == 0
                    ? "app-icon: the " + files.Length + " icon files are up to date."
                    : "app-icon: " + stale.Count + " icon file(s) out of date."
            );
            return stale.Count == 0 ? ExitCodes.Success : ExitCodes.Failure;
        }

        foreach (var (path, images) in stale)
        {
            var target = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, IcoFile.Write(images));
        }

        output.WriteLine(
            stale.Count == 0
                ? "app-icon: the " + files.Length + " icon files were already up to date."
                : "app-icon: wrote " + stale.Count + " icon file(s) in assets/icons."
        );
        return ExitCodes.Success;
    }

    /// <summary>The logo at every size of <paramref name="sizes"/>.</summary>
    public static IReadOnlyList<IcoImage> Render(
        IReadOnlyList<int> sizes,
        Rgba8 fill,
        Rgba8 ink,
        double opacity
    ) => [.. sizes.Select(size => LogoRaster.Render(size, fill, ink, opacity))];

    /// <summary>The accent and the color written over it, from <c>data/tokens</c> as the app renders them.</summary>
    public static bool TryReadColors(string root, TextWriter output, out Rgba8 fill, out Rgba8 ink)
    {
        fill = default;
        ink = default;
        List<TokenSourceFile> sources;
        try
        {
            sources =
            [
                .. TokenFiles.All.Select(name =>
                {
                    var path = Path.Combine(
                        root,
                        TokenFiles.Directory.Replace('/', Path.DirectorySeparatorChar),
                        name
                    );
                    return new TokenSourceFile(path, File.ReadAllText(path));
                }),
            ];
        }
        catch (IOException ex)
        {
            output.WriteLine("error: " + ex.Message);
            return false;
        }

        var model = TokenModelBuilder.Build(sources);
        var theme = model.Themes.Find(static t =>
            string.Equals(t.Key, ThemeKey, StringComparison.Ordinal)
        );
        if (
            theme is null
            || !theme.Colors.TryGetValue(FillToken, out var accent)
            || !theme.Colors.TryGetValue(InkToken, out var onAccent)
        )
        {
            foreach (var issue in model.Issues)
            {
                output.WriteLine("error " + issue.Id + ": " + issue.Message);
            }

            return false;
        }

        fill = accent.Value;
        ink = onAccent.Value;
        return true;
    }

    /// <summary>
    /// Whether the file at <paramref name="path"/> already holds <paramref name="images"/>. The pixels are compared,
    /// not the bytes: the compression of the 256 pixel image may differ between versions of .NET.
    /// </summary>
    public static bool IsCurrent(string root, string path, IReadOnlyList<IcoImage> images)
    {
        IReadOnlyList<IcoImage> existing;
        try
        {
            existing = IcoFile.Read(File.ReadAllBytes(Path.Combine(root, path)));
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }

        return existing.Count == images.Count
            && existing
                .Zip(images)
                .All(static pair =>
                    pair.First.Size == pair.Second.Size
                    && pair.First.Bgra.AsSpan().SequenceEqual(pair.Second.Bgra)
                );
    }
}
