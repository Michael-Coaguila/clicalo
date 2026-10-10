using Clicalo.Platform.Windows.Startup;

namespace Clicalo.Platform.IntegrationTests.SystemSection;

/// <summary>
/// «Iniciar con Windows» over a fake <c>Run</c> key (SIS-002, ADR-0027): the installed executable, quoted and without
/// arguments, never elevated; a copy that is not installed never registers itself; a refusal is reported, not thrown.
/// Headless and deterministic: the real registry is never touched.
/// </summary>
[Trait("Req", "SIS-002")]
public sealed class StartupRegistrationTests
{
    private const string Installed = @"C:\Users\Ana\AppData\Local\Clicalo.App\current\Clicalo.exe";

    [Fact]
    public void Turning_it_on_writes_the_installed_executable_and_off_removes_it()
    {
        var key = new FakeRunKey();
        var startup = new StartupRegistration(Installed, key);

        startup.IsAvailable.ShouldBeTrue();
        startup.IsEnabled.ShouldBeFalse();
        startup.TrySetEnabled(true).ShouldBeTrue();
        key.Values[StartupRegistration.ValueName].ShouldBe("\"" + Installed + "\"");
        startup.IsEnabled.ShouldBeTrue();
        startup.TrySetEnabled(false).ShouldBeTrue();

        key.Values.ShouldNotContainKey(StartupRegistration.ValueName);
        startup.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void An_entry_that_points_elsewhere_is_not_ours()
    {
        var key = new FakeRunKey();
        key.Values[StartupRegistration.ValueName] = @"""C:\Temp\Clicalo.exe"" --elevated";

        new StartupRegistration(Installed, key).IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void A_copy_that_is_not_installed_never_registers_itself()
    {
        var key = new FakeRunKey();
        var startup = new StartupRegistration(null, key);

        startup.IsAvailable.ShouldBeFalse();
        startup.TrySetEnabled(true).ShouldBeFalse();
        key.Values.ShouldBeEmpty();
    }

    [Fact]
    public void A_refusal_of_Windows_is_reported()
    {
        var startup = new StartupRegistration(Installed, new FakeRunKey { Refuse = true });

        startup.TrySetEnabled(true).ShouldBeFalse();
        startup.IsEnabled.ShouldBeFalse();
    }

    private sealed class FakeRunKey : IRunKey
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Refuse { get; init; }

        public string? Read(string name)
        {
            Check();
            return Values.TryGetValue(name, out var value) ? value : null;
        }

        public void Write(string name, string command)
        {
            Check();
            Values[name] = command;
        }

        public void Delete(string name)
        {
            Check();
            _ = Values.Remove(name);
        }

        private void Check()
        {
            if (Refuse)
            {
                throw new UnauthorizedAccessException();
            }
        }
    }
}
