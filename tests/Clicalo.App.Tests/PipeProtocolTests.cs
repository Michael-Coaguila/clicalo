using System.Security.Principal;
using System.Text;
using Clicalo.App.SingleInstance;
using Clicalo.Domain.Timing;

namespace Clicalo.App.Tests;

/// <summary>
/// The show-only pipe of the running instance (SIS-003, blueprint §3.4, ADR-0010): the server accepts exactly one
/// well-formed request and refuses everything else without acting on it.
/// </summary>
[Trait("Req", "SIS-003")]
public sealed class PipeProtocolTests
{
    [Fact]
    public void The_show_request_of_this_version_is_accepted() =>
        PipeProtocol.ReadRequest(PipeProtocol.ShowRequest()).ShouldBe(PipeStatus.Ok);

    [Theory]
    [InlineData("""{"t":"open","v":1}""")]
    [InlineData("""{"t":"show","v":2}""")]
    [InlineData("""{"v":1,"t":"inject"}""")]
    public void Another_verb_or_version_is_unsupported(string request) =>
        PipeProtocol.ReadRequest(Encoding.UTF8.GetBytes(request)).ShouldBe(PipeStatus.Unsupported);

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"t":"show"}""")]
    [InlineData("""{"v":1}""")]
    [InlineData("""{"t":"show","v":1,"x":0}""")]
    [InlineData("""{"t":"show","t":"show","v":1}""")]
    [InlineData("""{"t":"show","v":"1"}""")]
    [InlineData("""{"t":1,"v":1}""")]
    [InlineData("""{"t":"show","v":1.5}""")]
    [InlineData("""{"t":"show","v":1} {}""")]
    [InlineData("""{"t":"show","v":1,}""")]
    [InlineData("""{"t":"show",/*x*/"v":1}""")]
    [InlineData("""{"t":{"a":1},"v":1}""")]
    public void Anything_malformed_is_rejected(string request) =>
        PipeProtocol.ReadRequest(Encoding.UTF8.GetBytes(request)).ShouldBe(PipeStatus.Rejected);

    [Fact]
    public void An_oversized_request_is_rejected_before_it_is_parsed()
    {
        var padding = new string(' ', (int)Timings.Ipc.IpcMaxMessageBytes);
        var request = Encoding.UTF8.GetBytes("""{"t":"show","v":1}""" + padding);

        PipeProtocol.ReadRequest(request).ShouldBe(PipeStatus.Rejected);
    }

    [Fact]
    public void Every_response_reads_back()
    {
        foreach (var status in Enum.GetValues<PipeStatus>())
        {
            PipeProtocol.ReadResponse(PipeProtocol.Response(status)).ShouldBe(status);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("""{"status":"ok","x":1}""")]
    [InlineData("""{"status":"maybe"}""")]
    [InlineData("""{"state":"ok"}""")]
    public void A_malformed_response_is_unknown(string response) =>
        PipeProtocol.ReadResponse(Encoding.UTF8.GetBytes(response)).ShouldBeNull();

    [Fact]
    public void The_names_of_the_instance_derive_from_the_binary_sid_and_never_show_it()
    {
        var sid = new SecurityIdentifier("S-1-5-21-1004336348-1177238915-682003330-1001");

        var hash = InstanceIdentity.HashOf(sid);
        var identity = new InstanceIdentity(sid, hash, 3);

        hash.Length.ShouldBe(16);
        hash.ShouldAllBe(c => char.IsAsciiHexDigitLower(c) || char.IsAsciiDigit(c));
        InstanceIdentity.HashOf(new SecurityIdentifier(sid.Value)).ShouldBe(hash);
        var other = InstanceIdentity.HashOf(
            new SecurityIdentifier("S-1-5-21-1004336348-1177238915-682003330-1002")
        );
        string.Equals(other, hash, StringComparison.Ordinal).ShouldBeFalse();
        identity.MutexName.ShouldBe(@"Local\Clicalo." + hash + ".Instance");
        identity.PipeName.ShouldBe("Clicalo." + hash + ".3");
        identity.PipeName.Contains("1001", StringComparison.Ordinal).ShouldBeFalse();
    }
}
