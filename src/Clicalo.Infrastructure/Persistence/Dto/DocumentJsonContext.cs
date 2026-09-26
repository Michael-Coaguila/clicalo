using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clicalo.Infrastructure.Persistence.Dto;

/// <summary>
/// System.Text.Json source generation for the persisted DTOs (blueprint §6.5): no reflection, trimming-safe, camelCase
/// names and absent members for null values.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.Strict,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    AllowTrailingCommas = false,
    MaxDepth = 64
)]
[JsonSerializable(typeof(PayloadDto))]
[JsonSerializable(typeof(UsagePayloadDto))]
[JsonSerializable(typeof(ProfileDto))]
internal sealed partial class DocumentJsonContext : JsonSerializerContext;
