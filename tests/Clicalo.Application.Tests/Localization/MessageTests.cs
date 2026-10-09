using Clicalo.Domain.Messages;

namespace Clicalo.Application.Tests.Localization;

/// <summary>The Domain message value types that the generated <c>L</c> members build.</summary>
public sealed class MessageTests
{
    [Fact]
    public void Messages_have_value_equality_including_nested_messages()
    {
        L.ProcessTaken(L.ComboN(2), "winword.exe")
            .ShouldBe(L.ProcessTaken(L.ComboN(2), "winword.exe"));
        L.ProcessTaken(L.ComboN(2), "winword.exe")
            .GetHashCode()
            .ShouldBe(L.ProcessTaken(L.ComboN(2), "winword.exe").GetHashCode());
        L.ProcessTaken(L.ComboN(2), "winword.exe")
            .ShouldNotBe(L.ProcessTaken(L.ComboN(3), "winword.exe"));
        (L.ComboN(1) == L.ComboN(1)).ShouldBeTrue();
        (L.ComboN(1) != L.ComboN(2)).ShouldBeTrue();
    }

    [Fact]
    public void Generated_members_carry_the_key_and_the_named_arguments()
    {
        var message = L.DupHead(3, 2, 5);

        message.Key.ShouldBe(new MessageKey("dupHead"));
        message.Arguments.Select(static a => a.Name).ShouldBe(["count", "index", "total"]);
        message.TryGetArgument("index", out var index).ShouldBeTrue();
        index.TryGetWholeNumber(out var value).ShouldBeTrue();
        value.ShouldBe(2);
        message.ToString().ShouldBe("dupHead(count=3, index=2, total=5)");
    }

    [Fact]
    public void Arguments_must_have_distinct_non_empty_names()
    {
        var key = new MessageKey("k");

        Should.Throw<ArgumentException>(() =>
            new Message(
                key,
                MessageArgument.WholeNumber("a", 1),
                MessageArgument.WholeNumber("a", 2)
            )
        );
        Should.Throw<ArgumentException>(() => new Message(key, MessageArgument.WholeNumber("", 1)));
        Should.Throw<ArgumentException>(() => new Message(default));
    }

    [Fact]
    public void Text_values_hold_either_verbatim_text_or_a_message()
    {
        MessageValue.FromText("Word").TryGetText(out var text).ShouldBeTrue();
        text.ShouldBe("Word");
        MessageValue.FromText(L.Always).TryGetMessage(out var nested).ShouldBeTrue();
        nested.ShouldBe(L.Always);
        MessageValue.FromText(default).TryGetText(out var empty).ShouldBeTrue();
        empty.ShouldBeEmpty();
        Should.Throw<ArgumentNullException>(() => MessageText.FromString(null!));
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    public void The_catalog_lists_every_key_with_its_expected_arguments()
    {
        MessageCatalog.All.Length.ShouldBe(765);
        MessageCatalog.TryGet("migT", out _).ShouldBeFalse("retired by the user (ADR-0020)");
        MessageCatalog.TryGet("processTaken", out var processTaken).ShouldBeTrue();
        processTaken.Parameters.ShouldBe([
            new MessageParameter("profile", MessageArgumentType.Text),
            new MessageParameter("process", MessageArgumentType.Text),
        ]);
        MessageCatalog.TryGet("comboN", out var comboN).ShouldBeTrue();
        comboN.IsPlural.ShouldBeTrue();
        MessageCatalog.TryGet("comboN_one", out _).ShouldBeFalse();
        MessageCatalog.TryGet("ComboN", out _).ShouldBeFalse();
    }
}
