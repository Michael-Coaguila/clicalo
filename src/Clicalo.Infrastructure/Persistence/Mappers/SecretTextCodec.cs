using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Clicalo.Domain.Privacy;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>
/// Texts at rest (blueprint §6.7, ADR-0008, LOG-003): <c>{"enc":"dpapi.v1","blob":"…","len":42,"private":true}</c>,
/// <c>CryptProtectData</c> with CurrentUser scope and the entropy <c>Clicalo.Text.v1</c>. A text that cannot be
/// decrypted here (another machine or user) becomes <see cref="SecretText.Unavailable"/> and its shortcut is incomplete
/// (COP-005); the caller keeps the original blob so rewriting never destroys it (REG-08).
/// </summary>
/// <remarks>
/// Plain intermediate buffers are rented and wiped with <see cref="CryptographicOperations.ZeroMemory"/>. A plain JSON
/// string is accepted on read (fixtures and shared profiles exported in clear, DAT-007) and written only when a shared
/// profile is exported in clear by the user's choice.
/// </remarks>
internal static class SecretTextCodec
{
    /// <summary>The <c>enc</c> of a DPAPI text.</summary>
    public const string Dpapi = "dpapi.v1";

    /// <summary>The <c>enc</c> of a text that was unavailable when written (its original blob was not known).</summary>
    public const string Unavailable = "unavailable";

    /// <summary>The <c>enc</c> of a text left out of a shared profile (DAT-007).</summary>
    public const string Excluded = "excluded";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Clicalo.Text.v1");

    /// <summary>The persisted form of <paramref name="text"/>: DPAPI, or the empty string when there is nothing to hide.</summary>
    /// <param name="text">The text.</param>
    /// <param name="isPrivate">Whether its shortcut is private (LOG-004), recorded next to the blob.</param>
    public static JsonNode Protect(SecretText text, bool isPrivate)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.IsAvailable)
        {
            return Marker(Unavailable, 0, isPrivate);
        }

        if (text.Length == 0)
        {
            return JsonValue.Create(string.Empty);
        }

        var blob = new StrongBox<byte[]?>();
        text.WithRevealed(blob, static (chars, box) => box.Value = ProtectChars(chars));
        return new JsonObject
        {
            ["enc"] = Dpapi,
            ["blob"] = Convert.ToBase64String(blob.Value!),
            ["len"] = text.Length,
            ["private"] = isPrivate,
        };
    }

    /// <summary>The plain form of <paramref name="text"/> for a shared profile exported in clear (DAT-007).</summary>
    /// <param name="text">The text.</param>
    /// <param name="isPrivate">Whether its shortcut is private.</param>
    public static JsonNode Reveal(SecretText text, bool isPrivate)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.IsAvailable)
        {
            return Marker(Unavailable, 0, isPrivate);
        }

        var plain = new StrongBox<string?>();
        text.WithRevealed(plain, static (chars, box) => box.Value = new string(chars));
        return JsonValue.Create(plain.Value!);
    }

    /// <summary>The marker of a text left out of a shared profile: imported as unavailable (COP-005).</summary>
    /// <param name="text">The text left out.</param>
    /// <param name="isPrivate">Whether its shortcut is private.</param>
    public static JsonNode Exclude(SecretText text, bool isPrivate)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.IsAvailable && text.Length == 0
            ? JsonValue.Create(string.Empty)
            : Marker(Excluded, text.Length, isPrivate);
    }

    /// <summary>Reads a persisted text.</summary>
    /// <param name="node">The <c>text</c> member: absent, a plain string or an encrypted object.</param>
    /// <returns>The text, or <see cref="SecretText.Unavailable"/> when it cannot be decrypted here.</returns>
    public static SecretText Unprotect(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return SecretText.Empty;
            case JsonValue value
                when value.GetValueKind() == JsonValueKind.String
                    && value.TryGetValue<string>(out var plain):
                return SecretText.From(plain);
            case JsonObject encrypted:
                return string.Equals(Enc(encrypted), Dpapi, StringComparison.Ordinal)
                    ? Decrypt(encrypted)
                    : SecretText.Unavailable;
            default:
                return SecretText.Unavailable;
        }
    }

    /// <summary>Whether <paramref name="node"/> is an encrypted text (its blob is worth keeping when unavailable).</summary>
    /// <param name="node">A <c>text</c> member.</param>
    public static bool IsEncrypted(JsonNode? node) =>
        node is JsonObject encrypted
        && string.Equals(Enc(encrypted), Dpapi, StringComparison.Ordinal);

    private static string? Enc(JsonObject node) =>
        node["enc"] is JsonValue value && value.TryGetValue<string>(out var enc) ? enc : null;

    private static JsonObject Marker(string enc, int length, bool isPrivate) =>
        new()
        {
            ["enc"] = enc,
            ["len"] = length,
            ["private"] = isPrivate,
        };

    private static byte[] ProtectChars(ReadOnlySpan<char> chars)
    {
        var size = Encoding.UTF8.GetByteCount(chars);
        var rented = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            var written = Encoding.UTF8.GetBytes(chars, rented);
            return ProtectedData.Protect(
                rented.AsSpan(0, written),
                DataProtectionScope.CurrentUser,
                Entropy
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rented.AsSpan(0, size));
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static SecretText Decrypt(JsonObject encrypted)
    {
        if (
            encrypted["blob"] is not JsonValue blobValue
            || !blobValue.TryGetValue<string>(out var base64)
        )
        {
            return SecretText.Unavailable;
        }

        byte[] blob;
        try
        {
            blob = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return SecretText.Unavailable;
        }

        var plain = ArrayPool<byte>.Shared.Rent(Math.Max(blob.Length, 1));
        char[]? chars = null;
        var charCount = 0;
        try
        {
            if (
                !ProtectedData.TryUnprotect(
                    blob,
                    DataProtectionScope.CurrentUser,
                    plain,
                    out var written,
                    Entropy
                )
            )
            {
                return SecretText.Unavailable;
            }

            charCount = Encoding.UTF8.GetCharCount(plain.AsSpan(0, written));
            chars = ArrayPool<char>.Shared.Rent(Math.Max(charCount, 1));
            _ = Encoding.UTF8.GetChars(plain.AsSpan(0, written), chars);
            return SecretText.From(chars.AsSpan(0, charCount));
        }
        catch (CryptographicException)
        {
            return SecretText.Unavailable;
        }
        catch (DecoderFallbackException)
        {
            return SecretText.Unavailable;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            ArrayPool<byte>.Shared.Return(plain);
            if (chars is not null)
            {
                CryptographicOperations.ZeroMemory(
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(chars.AsSpan())
                );
                ArrayPool<char>.Shared.Return(chars);
            }
        }
    }
}
