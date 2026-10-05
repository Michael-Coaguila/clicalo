using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// The <c>major.minor</c> version of a persisted format (blueprint §6.5, ADR-0007): a minor is additive (an older
/// version with the same major reads it and keeps unknown fields); a major needs a migration, and a version that finds
/// a greater major than its own never writes.
/// </summary>
/// <param name="Major">Incompatible changes.</param>
/// <param name="Minor">Additive changes.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SchemaVersion(int Major, int Minor) : IComparable<SchemaVersion>
{
    /// <summary>Whether <paramref name="left"/> is older.</summary>
    /// <param name="left">First version.</param>
    /// <param name="right">Second version.</param>
    public static bool operator <(SchemaVersion left, SchemaVersion right) =>
        left.CompareTo(right) < 0;

    /// <summary>Whether <paramref name="left"/> is newer.</summary>
    /// <param name="left">First version.</param>
    /// <param name="right">Second version.</param>
    public static bool operator >(SchemaVersion left, SchemaVersion right) =>
        left.CompareTo(right) > 0;

    /// <summary>Whether <paramref name="left"/> is not newer.</summary>
    /// <param name="left">First version.</param>
    /// <param name="right">Second version.</param>
    public static bool operator <=(SchemaVersion left, SchemaVersion right) =>
        left.CompareTo(right) <= 0;

    /// <summary>Whether <paramref name="left"/> is not older.</summary>
    /// <param name="left">First version.</param>
    /// <param name="right">Second version.</param>
    public static bool operator >=(SchemaVersion left, SchemaVersion right) =>
        left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public int CompareTo(SchemaVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);

    /// <summary><c>major.minor</c>.</summary>
    public override string ToString() =>
        Major.ToString(CultureInfo.InvariantCulture)
        + "."
        + Minor.ToString(CultureInfo.InvariantCulture);
}
