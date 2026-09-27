using System.IO;
using Clicalo.App.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace Clicalo.App.Tests;

/// <summary>
/// A start with <c>--no-input</c> can never reach the keyboard (ADR-0004; M2 safety on the maintainer's machine): the
/// injector only counts, the ledger is detached, internal chords never inject, nothing is released at start, no
/// guardian starts and there is no injection gate at all.
/// </summary>
public sealed class EngineAdaptersTests
{
    [Fact]
    [Trait("Req", "SEG-006")]
    public void Without_key_sending_every_adapter_is_inert()
    {
        using var services = new ServiceCollection()
            .AddSingleton(AppOptions.Parse(["--no-input"], Path.GetTempPath()))
            .BuildServiceProvider();

        var set = EngineAdapters.Create(services);

        set.Injector.ShouldBeOfType<DryRunInputInjector>();
        set.Ledger.ShouldBeOfType<DetachedKeyLedger>();
        set.KeyEffects.ShouldBeOfType<DryRunKeyEffects>();
        set.Guardian.ShouldBeOfType<NoGuardian>();
        set.StartupRelease.ShouldBeOfType<NoGuardian>();
        set.Gate.ShouldBeNull();
        set.StartupRelease.ReleaseStuckModifiers().ShouldBe(0);
    }

    [Fact]
    public void The_guardian_is_looked_for_next_to_clicalo_exe() =>
        EngineAdapters.SentinelFileName.ShouldBe("Clicalo.Sentinel.exe");
}
