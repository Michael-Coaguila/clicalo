namespace Clicalo.Data.Tests.Catalogs;

/// <summary>Ordinal comparison of catalog identifiers, which are never culture-sensitive.</summary>
internal static class Ordinal
{
    public static bool Is(string? value, string? expected) =>
        string.Equals(value, expected, StringComparison.Ordinal);

    public static bool IsLowerCase(string text) => !text.Any(char.IsUpper);
}
