using System.IO.Packaging;
using System.Windows.Media;

namespace Clicalo.UI.Wpf.Resources;

/// <summary>
/// The fonts bundled in this assembly (TEM-005, docs/07 «Tipografía»): Atkinson Hyperlegible 400/700 for the
/// interface, JetBrains Mono 500 for keys and processes, and Material Symbols Rounded (FILL 0 and FILL 1) for icons.
/// They are static fonts built by <c>tools/fonts/build_fonts.py</c> into <c>assets/fonts</c> and compiled in as WPF
/// resources: nothing is read from the network or from the installed fonts.
/// </summary>
/// <remarks>
/// A <see cref="FontFamily"/> is immutable and not tied to a dispatcher, so these instances serve every UI thread.
/// Draw icons with <see cref="Controls.SymbolIcon"/> and <see cref="MaterialSymbols"/>, by code point.
/// </remarks>
public static class AppFonts
{
    /// <summary>Family name of the interface font.</summary>
    public const string UiFamilyName = "Atkinson Hyperlegible";

    /// <summary>Family name of the monospaced font of keys and processes (weight 500, <see cref="MonoWeight"/>).</summary>
    public const string MonoFamilyName = "JetBrains Mono";

    /// <summary>Family name of the outlined icons (FILL 0).</summary>
    public const string SymbolsFamilyName = "Material Symbols Rounded";

    /// <summary>Family name of the filled icons (FILL 1), for active states.</summary>
    public const string SymbolsFilledFamilyName = "Material Symbols Rounded Filled";

    /// <summary>The folder of the font resources, as a pack URI of this assembly.</summary>
    public static Uri FolderUri { get; } = CreateFolderUri();

    /// <summary>Atkinson Hyperlegible (Regular and Bold).</summary>
    public static FontFamily Ui { get; } = Family(UiFamilyName);

    /// <summary>JetBrains Mono (Medium only: use it with <see cref="MonoWeight"/>).</summary>
    public static FontFamily Mono { get; } = Family(MonoFamilyName);

    /// <summary>Material Symbols Rounded, FILL 0, weight 400, optical size 24.</summary>
    public static FontFamily Symbols { get; } = Family(SymbolsFamilyName);

    /// <summary>Material Symbols Rounded, FILL 1, weight 400, optical size 24.</summary>
    public static FontFamily SymbolsFilled { get; } = Family(SymbolsFilledFamilyName);

    /// <summary>The only weight bundled for <see cref="Mono"/> (500).</summary>
    public static System.Windows.FontWeight MonoWeight => System.Windows.FontWeights.Medium;

    /// <summary>The font files compiled into the assembly, under <see cref="FolderUri"/>.</summary>
    public static IReadOnlyList<string> FileNames { get; } =
    [
        "AtkinsonHyperlegible-Regular.ttf",
        "AtkinsonHyperlegible-Bold.ttf",
        "JetBrainsMono-Medium.ttf",
        "MaterialSymbolsRounded-Regular.ttf",
        "MaterialSymbolsRoundedFilled-Regular.ttf",
    ];

    private static Uri CreateFolderUri()
    {
        // Fonts in pack: URIs are read through the package of this application's resources, which WPF registers in
        // the type initializer of Application. Without an Application (tests, the first instants of start-up)
        // nothing may have run it yet, and a font looked up before it is not found (and the miss is cached).
        _ = PackUriHelper.UriSchemePack;
        _ = System.Windows.Application.Current;
        return new Uri(
            "pack://application:,,,/Clicalo.UI.Wpf;component/Resources/Fonts/",
            UriKind.Absolute
        );
    }

    private static FontFamily Family(string name) => new(FolderUri, "./#" + name);
}
