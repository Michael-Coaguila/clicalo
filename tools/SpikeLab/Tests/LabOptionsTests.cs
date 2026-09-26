using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Tests;

public sealed class LabOptionsTests
{
    [Fact]
    public void No_arguments_let_the_control_window_choose()
    {
        var options = LabOptions.Parse([], out var error);

        options.ShouldBe(LabOptions.Default);
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData("--spike", "S1", "S1")]
    [InlineData("--spike", "s3", "S3")]
    [InlineData("-s", "S4", "S4")]
    public void The_spike_is_read_from_the_next_argument(string name, string value, string expected)
    {
        LabOptions
            .Parse([name, value], out var error)
            .Spike.ShouldBe(Enum.Parse<SpikeId>(expected));
        error.ShouldBeNull();
    }

    [Fact]
    public void Values_may_also_follow_an_equals_sign()
    {
        var options = LabOptions.Parse(
            ["--spike=S4", "--reports=C:\\temp\\lab", "--check"],
            out var error
        );

        options.ShouldBe(new LabOptions(SpikeId.S4, "C:\\temp\\lab", CheckOnly: true));
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData("S2")]
    [InlineData("10")]
    [InlineData("S")]
    public void An_unknown_spike_is_explained_and_ignored(string value)
    {
        var options = LabOptions.Parse(["--spike", value], out var error);

        options.Spike.ShouldBeNull();
        error.ShouldNotBeNull().ShouldContain(value);
    }

    [Fact]
    public void An_unknown_argument_is_explained()
    {
        LabOptions.Parse(["--fast"], out var error);

        error.ShouldBe("Argumento desconocido: «--fast».");
    }

    [Fact]
    public void A_missing_folder_is_explained()
    {
        LabOptions.Parse(["--reports"], out var error).ReportDirectory.ShouldBeNull();

        error.ShouldBe("Falta la carpeta después de --reports.");
    }
}
