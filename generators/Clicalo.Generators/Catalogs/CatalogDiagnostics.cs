using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Catalogs;

/// <summary>
/// Data errors of the catalogs in <c>data/catalogs</c>. They are compile errors located at the exact line and
/// column of the JSON file, so a broken catalog never reaches a build (blueprint §1.1, idea 4).
/// </summary>
[SuppressMessage(
    "MicrosoftCodeAnalysisReleaseTracking",
    "RS2008:Enable analyzer release tracking",
    Justification = "Release tracking needs AnalyzerReleases.Shipped.md and AnalyzerReleases.Unshipped.md registered as AdditionalFiles in the shared Clicalo.Generators.csproj, which this package does not own; requested from the integrator."
)]
internal static class CatalogDiagnostics
{
    private const string Category = "Clicalo.Catalogs";

    public static readonly DiagnosticDescriptor InvalidJson = new(
        "CLCC001",
        "Invalid catalog JSON",
        "'{0}' is not valid JSON: {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A catalog must be strict RFC 8259 JSON without duplicate property names."
    );

    public static readonly DiagnosticDescriptor DuplicateId = new(
        "CLCC002",
        "Duplicate catalog identifier",
        "{0} '{1}' is defined more than once",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Identifiers are stable references stored in documents and generated as code, so they must be unique."
    );

    public static readonly DiagnosticDescriptor NonCanonicalId = new(
        "CLCC003",
        "Non-canonical catalog identifier",
        "{0} '{1}' is not canonical: {2}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Identifiers are persisted, so they have one canonical spelling (for example lower case, or char: plus one character)."
    );

    public static readonly DiagnosticDescriptor NegativeValue = new(
        "CLCC004",
        "Negative time or threshold",
        "'{0}' must not be negative",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Times, sizes and thresholds are non-negative (NFR-020)."
    );

    public static readonly DiagnosticDescriptor MissingUnit = new(
        "CLCC005",
        "Time or size without unit",
        "'{0}' needs an explicit unit ({1})",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every time is written with its unit (600ms, 2.5s, 10min, 24h, 7d) and every byte size too (16KiB), so no value is ambiguous."
    );

    public static readonly DiagnosticDescriptor KeyWithoutWin32Mapping = new(
        "CLCC006",
        "Key without Win32 mapping",
        "Key '{0}' has no entry in keys.win32.json",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every key of keys.json must be sendable: keys.win32.json gives its virtual key and scan code, or marks it as a layout character."
    );

    public static readonly DiagnosticDescriptor Win32MappingForUnknownKey = new(
        "CLCC007",
        "Win32 mapping for an unknown key",
        "keys.win32.json maps '{0}', which is not a key of keys.json",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The two key files must describe exactly the same keys."
    );

    public static readonly DiagnosticDescriptor InvalidCodeName = new(
        "CLCC008",
        "Invalid generated name",
        "'{0}' cannot be used as a generated C# name: {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Names that become C# members must be PascalCase identifiers that are not keywords."
    );

    public static readonly DiagnosticDescriptor InvalidStructure = new(
        "CLCC009",
        "Invalid catalog structure",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The catalog does not have the shape its JSON schema in data/schemas describes."
    );

    public static readonly DiagnosticDescriptor MissingCatalog = new(
        "CLCC010",
        "Missing catalog file",
        "The catalog 'data/catalogs/{0}' is not part of the compilation",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Projects with ClicaloGeneratorProfile=Domain must pass data/catalogs/*.json as AdditionalFiles."
    );
}
