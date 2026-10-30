using System.Globalization;
using System.IO;
using Clicalo.App.SingleInstance;

namespace Clicalo.App.Tests;

/// <summary>
/// A start with <c>--data</c> is its own instance (ADR-0030): its mutex and its pipe carry a suffix derived from the
/// full path of the folder, so a trial copy runs beside the person's Clícalo and never asks it to show itself. A
/// normal start keeps the names of ADR-0010.
/// </summary>
[Trait("Req", "SIS-003")]
public sealed class InstanceIdentityTests
{
    private const string Default = @"C:\Users\someone\AppData\Roaming\Clicalo";

    [Fact]
    public void A_normal_start_keeps_the_names_of_one_instance_per_user_and_session()
    {
        var identity = InstanceIdentity.Current(AppOptions.Parse([], Default));

        identity.DataScope.ShouldBeEmpty();
        identity.MutexName.ShouldBe(@"Local\Clicalo." + identity.SidHash + ".Instance");
        identity.PipeName.ShouldBe(
            "Clicalo."
                + identity.SidHash
                + "."
                + identity.SessionId.ToString(CultureInfo.InvariantCulture)
        );
    }

    [Fact]
    public void An_isolated_data_folder_gives_names_of_its_own()
    {
        var folder = @"C:\dev\clicalo-qa\live-data";
        var normal = InstanceIdentity.Current(AppOptions.Parse([], Default));

        var isolated = InstanceIdentity.Current(
            AppOptions.Parse(["--no-input", "--data", folder], Default)
        );

        isolated.DataScope.ShouldMatch("^\\.d[0-9a-f]{16}$");
        isolated.SidHash.ShouldBe(normal.SidHash);
        isolated.MutexName.ShouldBe(
            @"Local\Clicalo." + normal.SidHash + isolated.DataScope + ".Instance"
        );
        string.Equals(isolated.MutexName, normal.MutexName, StringComparison.Ordinal)
            .ShouldBeFalse();
        string.Equals(isolated.PipeName, normal.PipeName, StringComparison.Ordinal).ShouldBeFalse();
        isolated.PipeName.ShouldStartWith("Clicalo." + normal.SidHash + isolated.DataScope + ".");
        isolated
            .MutexName.Contains("clicalo-qa", StringComparison.OrdinalIgnoreCase)
            .ShouldBeFalse();
        isolated.MutexName.Contains("live", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    [Theory]
    [InlineData(@"C:\dev\clicalo-qa\live-data\")]
    [InlineData(@"c:\DEV\Clicalo-QA\Live-Data")]
    [InlineData("C:/dev/clicalo-qa/live-data")]
    [InlineData(@"C:\dev\clicalo-qa\other\..\live-data")]
    [InlineData(@"C:\dev\.\clicalo-qa\live-data")]
    public void The_same_folder_written_another_way_is_the_same_instance(string spelling) =>
        InstanceIdentity
            .ScopeOf(spelling)
            .ShouldBe(InstanceIdentity.ScopeOf(@"C:\dev\clicalo-qa\live-data"));

    [Fact]
    public void A_relative_folder_is_the_folder_it_names_from_the_current_one() =>
        InstanceIdentity
            .ScopeOf("clicalo-qa-relative")
            .ShouldBe(
                InstanceIdentity.ScopeOf(
                    Path.Combine(Environment.CurrentDirectory, "clicalo-qa-relative")
                )
            );

    [Fact]
    public void Two_folders_are_two_instances() =>
        string.Equals(
                InstanceIdentity.ScopeOf(@"C:\dev\clicalo-qa\live-data"),
                InstanceIdentity.ScopeOf(@"C:\dev\clicalo-qa\live-data2"),
                StringComparison.Ordinal
            )
            .ShouldBeFalse();

    [Fact]
    public void The_suffix_is_stable_across_versions()
    {
        // Fixed here so a change of the derivation is a decision (ADR-0030), not an accident: a tool or an old copy
        // that derives the names must keep finding the same instance.
        InstanceIdentity.ScopeOf(@"C:\dev\clicalo-qa\live-data").ShouldBe(".dfe8f6acf7d7db647");
    }

    [Fact]
    public void A_trial_copy_and_the_person_s_clicalo_are_both_first_instances()
    {
        // A made-up sidHash keeps the test away from the names of a Clícalo running on this machine.
        var sidHash = "t" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..15];
        var current = InstanceIdentity.Current(AppOptions.Parse([], Default));
        var person = current with { SidHash = sidHash };
        var trial = person with { DataScope = InstanceIdentity.ScopeOf(@"C:\dev\clicalo-qa\a") };
        var otherTrial = person with
        {
            DataScope = InstanceIdentity.ScopeOf(@"C:\dev\clicalo-qa\b"),
        };

        using var first = InstanceMutex.Open(person);
        using var second = InstanceMutex.Open(trial);
        using var third = InstanceMutex.Open(otherTrial);
        using var again = InstanceMutex.Open(trial with { });

        first.IsFirst.ShouldBeTrue();
        second.IsFirst.ShouldBeTrue("a trial copy never finds the person's instance");
        third.IsFirst.ShouldBeTrue("two trial folders are two instances");
        again.IsFirst.ShouldBeFalse("the same folder is still one instance");
    }
}
