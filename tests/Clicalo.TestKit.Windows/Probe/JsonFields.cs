using System.Text.Json;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>
/// Typed reads of probe event fields; a missing or mistyped field is a protocol error (<see cref="FormatException"/>).
/// The value kind is checked first because <see cref="JsonElement"/> getters throw
/// <see cref="InvalidOperationException"/> on a value of another kind.
/// </summary>
internal static class JsonFields
{
    public static long Int64(JsonElement json, string name) =>
        Number(json, name).TryGetInt64(out var value) ? value : throw Mistyped(name);

    public static ulong UInt64(JsonElement json, string name) =>
        Number(json, name).TryGetUInt64(out var value) ? value : throw Mistyped(name);

    public static int Int32(JsonElement json, string name) =>
        Number(json, name).TryGetInt32(out var value) ? value : throw Mistyped(name);

    public static uint UInt32(JsonElement json, string name) =>
        Number(json, name).TryGetUInt32(out var value) ? value : throw Mistyped(name);

    public static ushort UInt16(JsonElement json, string name) =>
        Number(json, name).TryGetUInt16(out var value) ? value : throw Mistyped(name);

    public static nint Handle(JsonElement json, string name) => (nint)Int64(json, name);

    public static bool Boolean(JsonElement json, string name) =>
        Property(json, name).ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Mistyped(name),
        };

    public static string String(JsonElement json, string name) =>
        AsString(Property(json, name), name);

    public static string? OptionalString(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) ? AsString(value, name) : null;

    public static long? OptionalInt64(JsonElement json, string name) =>
        json.TryGetProperty(name, out _) ? Int64(json, name) : null;

    private static JsonElement Property(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value)
            ? value
            : throw new FormatException("The probe event has no '" + name + "' field.");

    private static JsonElement Number(JsonElement json, string name)
    {
        var value = Property(json, name);
        return value.ValueKind == JsonValueKind.Number ? value : throw Mistyped(name);
    }

    private static string AsString(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Mistyped(name);

    private static FormatException Mistyped(string name) =>
        new("The probe event field '" + name + "' has an unexpected type.");
}
