using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Clicalo.Domain.Privacy;
using Clicalo.Infrastructure.Persistence.Mappers;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>Texts at rest with DPAPI (blueprint §6.7, ADR-0008).</summary>
[Trait("Req", "LOG-003")]
public sealed class SecretTextCodecTests
{
    [Fact]
    public void A_text_is_encrypted_for_this_user_and_decrypts_back()
    {
        var node = SecretTextCodec
            .Protect(SecretText.From("Mi firma: Ñandú 42"), isPrivate: true)
            .AsObject();

        node["enc"]!.GetValue<string>().ShouldBe("dpapi.v1");
        node["len"]!.GetValue<int>().ShouldBe(18);
        node["private"]!.GetValue<bool>().ShouldBeTrue();
        node.ToJsonString().ShouldNotContain("firma");
        SecretTextCodec.Unprotect(node).ShouldBe(SecretText.From("Mi firma: Ñandú 42"));
    }

    [Fact]
    public void The_blob_is_bound_to_the_clicalo_entropy()
    {
        var blob = ProtectedData.Protect(
            Encoding.UTF8.GetBytes("secreto"),
            Encoding.UTF8.GetBytes("otra-app"),
            DataProtectionScope.CurrentUser
        );
        var node = new JsonObject
        {
            ["enc"] = "dpapi.v1",
            ["blob"] = Convert.ToBase64String(blob),
            ["len"] = 7,
        };

        SecretTextCodec.Unprotect(node).IsAvailable.ShouldBeFalse();
    }

    [Theory]
    [Trait("Req", "COP-005")]
    [InlineData("{\"enc\":\"dpapi.v1\",\"blob\":\"AAAA\",\"len\":3}")]
    [InlineData("{\"enc\":\"dpapi.v1\",\"blob\":\"not base64!\",\"len\":3}")]
    [InlineData("{\"enc\":\"dpapi.v1\",\"len\":3}")]
    [InlineData("{\"enc\":\"dpapi.v9\",\"blob\":\"AAAA\"}")]
    [InlineData("{\"enc\":\"unavailable\",\"len\":0}")]
    [InlineData("{\"enc\":\"excluded\",\"len\":5}")]
    [InlineData("42")]
    public void A_text_that_cannot_be_decrypted_here_is_unavailable(string json) =>
        SecretTextCodec.Unprotect(JsonNode.Parse(json)).IsAvailable.ShouldBeFalse();

    [Fact]
    public void A_plain_string_is_read_as_the_text_and_nothing_is_an_empty_text()
    {
        SecretTextCodec.Unprotect(JsonValue.Create("hola")).ShouldBe(SecretText.From("hola"));
        SecretTextCodec.Unprotect(null).ShouldBe(SecretText.Empty);
    }

    [Fact]
    public void An_empty_text_has_nothing_to_hide()
    {
        SecretTextCodec
            .Protect(SecretText.Empty, isPrivate: false)
            .GetValue<string>()
            .ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "DAT-007")]
    public void A_shared_text_is_left_out_unless_included_in_clear()
    {
        var text = SecretText.From("dirección");

        var excluded = SecretTextCodec.Exclude(text, isPrivate: false).AsObject();
        excluded["enc"]!.GetValue<string>().ShouldBe("excluded");
        excluded.ToJsonString().ShouldNotContain("dirección");
        SecretTextCodec.Unprotect(excluded).IsAvailable.ShouldBeFalse();
        SecretTextCodec.Reveal(text, isPrivate: false).GetValue<string>().ShouldBe("dirección");
    }

    [Fact]
    [Trait("Req", "REG-08")]
    public void An_unavailable_text_without_its_original_blob_is_written_as_a_marker()
    {
        var node = SecretTextCodec.Protect(SecretText.Unavailable, isPrivate: true).AsObject();

        node["enc"]!.GetValue<string>().ShouldBe("unavailable");
        SecretTextCodec.IsEncrypted(node).ShouldBeFalse();
    }
}
