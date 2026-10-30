using Clicalo.Application.Ports;
using Clicalo.Application.Tests.Store;
using Clicalo.Application.UseCases.Ai;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Tests.Generators;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.Tests.UseCases.Ai;

/// <summary>
/// «Crear con IA» with the person's own key (PLA-002 to PLA-008, ADR-0014, user decision D5): the order of PLA-005,
/// the four values of the request, the time limit and the error cards.
/// </summary>
[Trait("Req", "PLA-005")]
public sealed class AiAssistantTests
{
    private static readonly AiTemplateProposal Proposal = new(
        true,
        "WhatsApp",
        "WhatsApp.exe",
        "chat",
        [new AiProposedShortcut("Nuevo chat", "New chat", "add", ["ctrl", "n"], "file", 0.9)]
    );

    private static (
        AiAssistant Ai,
        StoreHarness Harness,
        CannedGenerator Generator,
        MemoryKeys Keys
    ) Create(bool consent = true, bool disabled = false, bool key = true)
    {
        var defaults = SettingsSchema.Defaults;
        var settings = defaults with
        {
            Ai = defaults.Ai with { Consent = consent, Disabled = disabled },
            Keyboard = defaults.Keyboard with { AppsLanguage = LangCode.En },
        };
        var harness = new StoreHarness(
            UserDocument.Create(
                ShortcutLibrary.CreateValidated([], [DomainGen.General()]).Value,
                settings
            )
        );
        var generator = new CannedGenerator();
        var keys = new MemoryKeys();
        if (key)
        {
            _ = keys.Save(new Sensitive<string>("sk-test", RedactionKind.Secret));
        }

        return (
            new AiAssistant(harness.Store, generator, keys, harness.Time),
            harness,
            generator,
            keys
        );
    }

    private static Task<AiTemplate?> Generate(AiAssistant ai, string app = "WhatsApp") =>
        ai.GenerateAsync(
            app,
            "es-LA",
            LangCode.Es,
            static _ => false,
            static _ => true,
            CancellationToken.None
        );

    [Fact]
    [Trait("Req", "PLA-008")]
    public async Task A_generation_sends_exactly_the_four_values_and_gives_a_validated_proposal()
    {
        var (ai, _, generator, _) = Create();

        var template = (await Generate(ai, "  WhatsApp  ")).ShouldNotBeNull();

        generator
            .Requests.ShouldHaveSingleItem()
            .ShouldBe(new TemplateRequest("WhatsApp", "es-LA", LangCode.En, LangCode.Es));
        typeof(TemplateRequest)
            .GetProperties()
            .Length.ShouldBe(4, "no fifth value can leave the machine");
        template
            .Template.Shortcuts.ShouldHaveSingleItem()
            .Action.ShouldBe(new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N), []));
        ai.Error.ShouldBe(AiError.None);
        ai.LastApp.ShouldBe("WhatsApp");
    }

    [Fact]
    [Trait("Req", "PLA-004")]
    public async Task Off_comes_before_consent_and_consent_before_the_key()
    {
        var (off, _, offGenerator, _) = Create(consent: false, disabled: true, key: false);
        (await Generate(off)).ShouldBeNull();
        off.Error.ShouldBe(AiError.Off);
        off.AskingConsent.ShouldBeFalse();

        var (ask, _, askGenerator, _) = Create(consent: false, key: false);
        (await Generate(ask)).ShouldBeNull();
        ask.AskingConsent.ShouldBeTrue();
        ask.Error.ShouldBe(AiError.None);

        var (noKey, _, keyGenerator, _) = Create(key: false);
        (await Generate(noKey)).ShouldBeNull();
        noKey.Error.ShouldBe(AiError.NoKey);

        offGenerator.Requests.ShouldBeEmpty();
        askGenerator.Requests.ShouldBeEmpty();
        keyGenerator.Requests.ShouldBeEmpty("nothing leaves the machine before these checks");
    }

    [Fact]
    [Trait("Req", "PLA-004")]
    public async Task Consent_accepted_saves_it_and_declined_turns_the_ai_off_until_enabled()
    {
        var (ai, harness, _, _) = Create(consent: false);
        _ = await Generate(ai);
        ai.AcceptConsent();
        harness.Store.Current.Settings.Ai.Consent.ShouldBeTrue();
        (await Generate(ai)).ShouldNotBeNull();

        var (other, otherHarness, _, _) = Create(consent: false);
        _ = await Generate(other);
        other.Decline();
        otherHarness.Store.Current.Settings.Ai.Disabled.ShouldBeTrue();
        other.Error.ShouldBe(AiError.Off);
        other.Enable();
        otherHarness.Store.Current.Settings.Ai.Disabled.ShouldBeFalse();
        other.AskingConsent.ShouldBeTrue();
    }

    [Theory]
    [Trait("Req", "PLA-006")]
    [InlineData(AiFailure.Offline, AiError.Offline)]
    [InlineData(AiFailure.BadKey, AiError.BadKey)]
    [InlineData(AiFailure.Unavailable, AiError.Unavailable)]
    [InlineData(AiFailure.Invalid, AiError.Invalid)]
    [InlineData(AiFailure.NoKey, AiError.NoKey)]
    public async Task Each_failure_shows_its_card(AiFailure failure, AiError expected)
    {
        var (ai, _, generator, _) = Create();
        generator.Next = TemplateGeneration.Fail(failure);

        (await Generate(ai)).ShouldBeNull();

        ai.Error.ShouldBe(expected);
    }

    [Fact]
    [Trait("Req", "PLA-008")]
    public async Task A_proposal_without_valid_shortcuts_is_invalid()
    {
        var (ai, _, generator, _) = Create();
        generator.Next = TemplateGeneration.Ok(
            Proposal with
            {
                Shortcuts = [new AiProposedShortcut("x", "x", "add", ["hyper"], "file", 1)],
            }
        );

        (await Generate(ai)).ShouldBeNull();

        ai.Error.ShouldBe(AiError.Invalid);
    }

    [Fact]
    [Trait("Req", "NFR-020")]
    public async Task No_answer_within_the_time_limit_is_offline()
    {
        var (ai, harness, generator, _) = Create();
        generator.Hang = true;

        var generation = Generate(ai);
        ai.Generating.ShouldBeTrue();
        harness.Time.Advance(Timings.Ai.AiRequestTimeout);

        (await generation).ShouldBeNull();
        ai.Error.ShouldBe(AiError.Offline);
        ai.Generating.ShouldBeFalse();
        generator.Cancelled.ShouldBeTrue("the request is abandoned");
    }

    [Fact]
    [Trait("Req", "PLA-003")]
    public void Saving_a_key_keeps_only_its_reference_in_the_document()
    {
        var (ai, harness, _, keys) = Create(key: false);

        ai.SaveKey(new Sensitive<string>("  ", RedactionKind.Secret)).ShouldBeFalse();
        ai.SaveKey(new Sensitive<string>("sk-new", RedactionKind.Secret)).ShouldBeTrue();

        harness.Store.Current.Settings.Ai.ApiKeyRef.ShouldBe(keys.Target);
        ai.HasKey.ShouldBeTrue();
        ai.DeleteKey();
        ai.HasKey.ShouldBeFalse();
        harness.Store.Current.Settings.Ai.ApiKeyRef.ShouldBeNull();
    }

    [Fact]
    public async Task An_empty_name_does_nothing()
    {
        var (ai, _, generator, _) = Create();

        (await Generate(ai, "   ")).ShouldBeNull();

        generator.Requests.ShouldBeEmpty();
        ai.Error.ShouldBe(AiError.None);
    }

    /// <summary>A generator with canned answers (blueprint §10: CannedTemplateGenerator).</summary>
    private sealed class CannedGenerator : ITemplateGenerator
    {
        public List<TemplateRequest> Requests { get; } = [];

        public TemplateGeneration Next { get; set; } = TemplateGeneration.Ok(Proposal);

        public bool Hang { get; set; }

        public bool Cancelled { get; private set; }

        public async ValueTask<TemplateGeneration> GenerateAsync(
            TemplateRequest request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(request);
            if (!Hang)
            {
                return Next;
            }

            var never = new TaskCompletionSource();
            await using var registration = cancellationToken.Register(() =>
            {
                Cancelled = true;
                never.TrySetCanceled(cancellationToken);
            });
            await never.Task;
            return Next;
        }
    }

    private sealed class MemoryKeys : IAiKeyStore
    {
        private Sensitive<string>? _key;

        public string Target => "Clicalo/ai/test";

        public bool HasKey() => _key is not null;

        public bool Save(Sensitive<string> key)
        {
            _key = key;
            return true;
        }

        public Sensitive<string>? Read() => _key;

        public bool Delete()
        {
            _key = null;
            return true;
        }
    }
}
