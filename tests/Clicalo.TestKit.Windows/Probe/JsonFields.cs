using System.Text.Json;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>Typed reads of probe event fields; a missing or mistyped field is a protocol error.</summary>
internal static class JsonFields
{
    public static long Int64(JsonElement json, string name) =>
        Property(json, name).TryGetInt64(out var value) ? value : throw Mistyped(name);

    public static ulong UInt64(JsonElement json, string name) =>
        Property(json, name).TryGetUInt64(out var value) ? value : throw Mistyped(name);

    public static int Int32(JsonElement json, string name) =>
        Property(json, name).TryGetInt32(out var value) ? value : throw Mistyped(name);

    public static uint UInt32(JsonElement json, string name) =>
        Property(json, name).TryGetUInt32(out var value) ? value : throw Mistyped(name);

    public static ushort UInt16(JsonElement json, string name) =>
        Property(json, name).TryGetUInt16(out var value) ? value : throw Mistyped(name);

    public static nint Handle(JsonElement json, string name) => (nint)Int64(json, name);

    public static bool Boolean(JsonElement json, string name) =>
        Property(json, name).ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Mistyped(name),
        };

    public static string String(JsonElement json, string name) =>
        Property(json, name).GetString() ?? throw Mistyped(name);

    public static string? OptionalString(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) ? value.GetString() : null;

    public static long? OptionalInt64(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) && value.TryGetInt64(out var number)
            ? number
            : null;

    private static JsonElement Property(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value)
            ? value
            : throw new FormatException("The probe event has no '" + name + "' field.");

    private static FormatException Mistyped(string name) =>
        new("The probe event field '" + name + "' has an unexpected type.");
}
