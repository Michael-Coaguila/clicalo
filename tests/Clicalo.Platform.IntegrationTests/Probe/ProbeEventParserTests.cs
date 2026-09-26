using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Platform.IntegrationTests.Probe;

/// <summary>The client side of the InputProbe protocol, on lines recorded from a real probe.</summary>
public sealed class ProbeEventParserTests
{
    [Fact]
    public void A_ready_line_exposes_everything_needed_to_target_the_probe()
    {
        var ready = ProbeEventParser
            .Parse(
                """{"kind":"ready","seq":7,"qpc":428179970553,"fg":658920,"protocol":1,"hwnd":658920,"pid":31112,"tid":37072,"session":1,"qpcFrequency":10000000,"hkl":67699721}"""
            )
            .ShouldBeOfType<ProbeReadyEvent>();

        ready.Sequence.ShouldBe(7);
        ready.Timestamp.ShouldBe(428179970553);
        ready.ForegroundWindow.ShouldBe(658920);
        ready.Protocol.ShouldBe(1);
        ready.Window.ShouldBe(658920);
        ready.ProcessId.ShouldBe(31112);
        ready.ThreadId.ShouldBe(37072u);
        ready.SessionId.ShouldBe(1);
        ready.TimestampFrequency.ShouldBe(10_000_000);
        ready.KeyboardLayout.ShouldBe(0x0409_0409);
    }

    [Fact]
    public void A_key_line_decodes_the_virtual_key_its_side_and_the_keystroke_flags()
    {
        var key = ProbeEventParser
            .Parse(
                """{"kind":"key","seq":12,"qpc":5,"fg":658920,"msg":256,"name":"WM_KEYDOWN","time":42818015,"wParam":17,"lParam":18677761,"extra":1129073457,"vk":17,"vkEx":163,"scan":29,"ext":true,"repeat":1,"alt":false,"prev":false,"up":false,"mods":8}"""
            )
            .ShouldBeOfType<KeyMessageEvent>();

        key.MessageName.ShouldBe("WM_KEYDOWN");
        key.VirtualKey.ShouldBe(VirtualKeyCode.Control);
        key.SideVirtualKey.ShouldBe(VirtualKeyCode.RightControl);
        key.ScanCode.ShouldBe((byte)0x1D);
        key.IsExtended.ShouldBeTrue();
        key.IsPress.ShouldBeTrue();
        key.IsSystemKey.ShouldBeFalse();
        key.Modifiers.ShouldBe(SideModifiers.RightControl);
        key.ExtraInfo.ShouldBe(TestKeyboardInjector.ExtraInfoMarker);
    }

    [Fact]
    public void The_low_surrogate_of_an_emoji_carries_the_whole_character()
    {
        var high = ProbeEventParser
            .Parse(
                """{"kind":"char","seq":1,"qpc":1,"fg":1,"msg":258,"name":"WM_CHAR","time":0,"wParam":55357,"lParam":1,"extra":0,"unit":55357,"scan":0,"ext":false,"repeat":1,"alt":false,"prev":false,"up":false,"mods":0}"""
            )
            .ShouldBeOfType<CharMessageEvent>();
        var low = ProbeEventParser
            .Parse(
                """{"kind":"char","seq":2,"qpc":2,"fg":1,"msg":258,"name":"WM_CHAR","time":0,"wParam":56832,"lParam":1,"extra":0,"unit":56832,"text":"😀","scan":0,"ext":false,"repeat":1,"alt":false,"prev":false,"up":false,"mods":0}"""
            )
            .ShouldBeOfType<CharMessageEvent>();

        high.Text.ShouldBeNull();
        high.IsTyped.ShouldBeTrue();
        low.CodeUnit.ShouldBe('\uDE00');
        low.Text.ShouldBe("😀");
    }

    [Fact]
    public void Activation_focus_and_command_answers_are_typed()
    {
        ProbeEventParser
            .Parse(
                """{"kind":"activate","seq":3,"qpc":3,"fg":658920,"msg":6,"name":"WM_ACTIVATE","time":0,"wParam":1,"lParam":0,"extra":0,"state":1,"minimized":false,"other":0}"""
            )
            .ShouldBeOfType<ActivateEvent>()
            .State.ShouldBe(ActivationState.Active);
        ProbeEventParser
            .Parse(
                """{"kind":"focus","seq":14,"qpc":14,"fg":918810,"msg":8,"name":"WM_KILLFOCUS","time":42818015,"wParam":0,"lParam":0,"extra":0,"gained":false,"other":0}"""
            )
            .ShouldBeOfType<FocusEvent>()
            .IsGained.ShouldBeFalse();
        ProbeEventParser
            .Parse(
                """{"kind":"foreground","seq":10,"qpc":10,"fg":658920,"id":8,"hwnd":658920,"ok":true}"""
            )
            .ShouldBeOfType<ProbeForegroundEvent>()
            .Succeeded.ShouldBeTrue();
        ProbeEventParser
            .Parse("""{"kind":"pong","seq":8,"qpc":8,"fg":658920,"id":7}""")
            .ShouldBeOfType<ProbePongEvent>()
            .Id.ShouldBe(7);
        ProbeEventParser
            .Parse(
                """{"kind":"message","seq":2,"qpc":2,"fg":658920,"msg":134,"name":"WM_NCACTIVATE","time":0,"wParam":1,"lParam":0,"extra":0}"""
            )
            .ShouldBeOfType<WindowMessageEvent>()
            .Message.ShouldBe(0x86u);
    }

    [Fact]
    public void Raw_input_exposes_the_prefix_and_break_flags()
    {
        var raw = ProbeEventParser
            .Parse(
                """{"kind":"rawKey","seq":4,"qpc":4,"fg":1,"msg":255,"name":"WM_INPUT","time":0,"wParam":0,"lParam":123,"extra":1129073457,"scan":75,"flags":3,"vk":37,"rawMsg":257,"device":0,"sink":false}"""
            )
            .ShouldBeOfType<RawKeyboardEvent>();

        raw.MakeCode.ShouldBe((ushort)0x4B);
        raw.IsE0.ShouldBeTrue();
        raw.IsBreak.ShouldBeTrue();
        raw.IsE1.ShouldBeFalse();
        raw.VirtualKey.ShouldBe(VirtualKeyCode.Left);
        raw.Device.ShouldBe(0);
    }

    [Fact]
    public void Unknown_kinds_are_kept_and_broken_lines_are_rejected()
    {
        ProbeEventParser
            .Parse("""{"kind":"future","seq":1,"qpc":1,"fg":0}""")
            .ShouldBeOfType<UnknownProbeEvent>();
        Should.Throw<FormatException>(() => ProbeEventParser.Parse("not json"));
        Should
            .Throw<FormatException>(() =>
                ProbeEventParser.Parse("""{"kind":"key","seq":1,"qpc":1,"fg":0}""")
            )
            .Message.ShouldContain("'msg'");
        Should.Throw<FormatException>(() => ProbeEventParser.Parse("[1,2]"));
    }

    [Theory]
    [InlineData("""{"kind":"pong","seq":"1","qpc":1,"fg":0}""", "'seq'")]
    [InlineData("""{"kind":7,"seq":1,"qpc":1,"fg":0}""", "'kind'")]
    [InlineData("""{"kind":"pong","seq":1,"qpc":1,"fg":0,"id":"7"}""", "'id'")]
    [InlineData("""{"kind":"error","seq":1,"qpc":1,"fg":0,"detail":null}""", "'detail'")]
    [InlineData("""{"kind":"ready","seq":1,"qpc":1,"fg":0,"protocol":1.5}""", "'protocol'")]
    public void A_field_of_the_wrong_type_is_a_protocol_error(string line, string field) =>
        Should
            .Throw<FormatException>(() => ProbeEventParser.Parse(line))
            .Message.ShouldContain(field);
}
